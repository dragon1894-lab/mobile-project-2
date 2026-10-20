import ast, hashlib, sys
from collections import Counter, defaultdict
from pathlib import Path
import numpy as np, cv2, onnx, onnxruntime as ort

ROOT = Path(sys.argv[1])
AP = ROOT / 'training/all_phones_20261020'
EXT = {'.jpg', '.jpeg', '.png', '.bmp'}
CLASSES = [c.strip() for c in (AP / 'classes.txt').read_text(encoding='utf-8-sig').splitlines() if c.strip()]
CONF, IOU = 0.45, 0.45


def fnv(s):
    h = 2166136261
    for ch in s:
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def imread(p):
    return cv2.imdecode(np.fromfile(str(p), np.uint8), cv2.IMREAD_COLOR)


def dhash(img):
    g = cv2.resize(cv2.cvtColor(img, cv2.COLOR_BGR2GRAY), (9, 8), interpolation=cv2.INTER_AREA)
    return int(''.join('1' if b else '0' for b in (g[:, 1:] > g[:, :-1]).flatten()), 2)


# ---------- 1. data audit ----------
print('=== 1. 데이터 점검 ===')
labels = {}
issues = []
for img in sorted(p for p in (AP / 'images').iterdir() if p.suffix.lower() in EXT):
    lab = AP / 'labels' / (img.stem + '.txt')
    if not lab.exists():
        issues.append(f'라벨 없음 {img.name}'); continue
    rows = [l.split() for l in lab.read_text(encoding='utf-8-sig').splitlines() if l.strip()]
    for r in rows:
        if len(r) != 5 or not r[0].isdigit() or int(r[0]) >= len(CLASSES) or not all(0 <= float(v) <= 1 for v in r[1:]):
            issues.append(f'형식 오류 {lab.name}: {r}')
    if len({r[0] for r in rows}) > 1:
        issues.append(f'한 사진에 여러 클래스 {lab.name}')
    labels[img] = rows
orphans = {p.stem for p in (AP / 'labels').glob('*.txt')} - {p.stem for p in labels}
print('사진', len(labels), '| 문제', len(issues), issues[:8], '| 사진 없는 라벨', len(orphans))
empty = [p.name for p, r in labels.items() if not r]
print('빈 라벨', len(empty), empty[:6])

by_class = defaultdict(list)
for p, r in labels.items():
    if r: by_class[int(r[0][0])].append(p.name)
split = {}
for cid, names in by_class.items():
    order = sorted(names, key=fnv)
    nv = max(1, round(len(order) * 0.2))
    for i, n in enumerate(order): split[n] = 'val' if i < nv else 'train'
boxes_per = {c: Counter(len(labels[AP / 'images' / n]) for n in ns) for c, ns in by_class.items()}
for cid in sorted(by_class):
    ns = by_class[cid]
    print(f'  {cid} {CLASSES[cid]:30s} 사진 {len(ns):4d} (학습 {sum(split[n]=="train" for n in ns)}/검증 {sum(split[n]=="val" for n in ns)}) 박스수 분포 {dict(sorted(boxes_per[cid].items()))}')

hashes, dh, imgs = {}, {}, {}
for p in labels:
    hashes[p.name] = hashlib.sha256(p.read_bytes()).hexdigest()
    im = imread(p); imgs[p.name] = im; dh[p.name] = dhash(im)
dup = Counter(hashes.values())
print('완전히 같은 사진(중복) 그룹', sum(1 for v in dup.values() if v > 1), '/ 중복 파일', sum(v - 1 for v in dup.values() if v > 1))
tr = [n for n in split if split[n] == 'train']; va = [n for n in split if split[n] == 'val']
exact_leak = [n for n in va if any(hashes[n] == hashes[t] for t in tr)]
near_leak = [n for n in va if min(bin(dh[n] ^ dh[t]).count('1') for t in tr) <= 4]
print('검증 사진 중 학습과 완전히 같은 것', len(exact_leak), '| 거의 같은 것(dHash≤4)', len(near_leak), f'/ {len(va)}')
leak_by = Counter(CLASSES[int(labels[AP / 'images' / n][0][0])] for n in near_leak)
print('  거의 같은 것 클래스별', dict(leak_by))

# overlap with local old datasets
print('\n=== 2. 이 PC의 옛 데이터가 all_phones에 들어갔는지 ===')
ap_hash = set(hashes.values()); ap_dh = list(dh.values())
OLD = {
    '12Pro 실버 107장': 'imports/phone-models-20261020/iphone_12pro_silver/images',
    '14 화이트 50장(옛)': 'imports/phone-models-20261020/iphone_14_white/images',
    '14 화이트 159장(추가학습)': '04_Vision/models/alternatives/' + next(d.name for d in (ROOT / '04_Vision/models/alternatives').iterdir() if d.name.startswith('11_')) + '/dataset/images',
    '16 화이트 72장': 'imports/phone-models-20261020/iphone16_white/images',
    '파손 crack_phone 61장': 'imports/three-model-merge-20261019-145921/broken_phone/broken_phone/images',
    '12ProMax 파손 60장': 'imports/phone-models-20261020/iphone_12_pro_max_Crack/images',
    '컨베이어 실사진 8장': 'images',
}
old_files = {}
for k, rel in OLD.items():
    fs = [p for p in (ROOT / rel).rglob('*') if p.suffix.lower() in EXT]
    old_files[k] = fs
    same = sum(hashlib.sha256(p.read_bytes()).hexdigest() in ap_hash for p in fs)
    near = 0
    for p in fs:
        h = dhash(imread(p))
        near += min(bin(h ^ d).count('1') for d in ap_dh) <= 4
    print(f'  {k:24s} 파일 {len(fs):4d} | 똑같은 파일 포함 {same:4d} | 거의 같은 사진 포함 {near:4d}')

# ---------- 3. model ----------
model = AP / 'models/best.onnx'
meta = {p.key: p.value for p in onnx.load(str(model), load_external_data=False).metadata_props}
names = ast.literal_eval(meta['names']); names = [names[i] for i in sorted(names)]
sess = ort.InferenceSession(str(model), providers=['CPUExecutionProvider'])
W = H = sess.get_inputs()[0].shape[2]
print('\n=== 3. 모델 ===', names, '입력', W, '출력', sess.get_outputs()[0].shape, 'names==classes.txt', names == CLASSES)


def detect(img):
    s = min(W / img.shape[1], H / img.shape[0])
    rw, rh = max(1, round(img.shape[1] * s)), max(1, round(img.shape[0] * s))
    c = np.full((H, W, 3), 114, np.uint8); l, t = int((W - rw) / 2), int((H - rh) / 2)
    c[t:t + rh, l:l + rw] = cv2.resize(img, (rw, rh))
    x = cv2.cvtColor(c, cv2.COLOR_BGR2RGB).transpose(2, 0, 1)[None].astype(np.float32) / 255
    o = sess.run(None, {sess.get_inputs()[0].name: x})[0][0]
    if o.shape[0] != len(names) + 4: o = o.T
    sc = o[4:]; cls = sc.argmax(0); conf = sc.max(0); keep = conf >= CONF
    b = o[:4, keep].T; cls = cls[keep]; conf = conf[keep]
    xyxy = np.stack([(b[:, 0] - b[:, 2] / 2 - l) / s, (b[:, 1] - b[:, 3] / 2 - t) / s, (b[:, 0] + b[:, 2] / 2 - l) / s, (b[:, 1] + b[:, 3] / 2 - t) / s], 1) if len(b) else np.zeros((0, 4))
    out = []
    for i in np.argsort(-conf):
        bb = xyxy[i]
        if any(c2 == cls[i] and iou(bb, b2) > IOU for b2, c2, _ in out): continue
        out.append((bb, int(cls[i]), float(conf[i])))
    return out


def iou(a, b):
    x1, y1, x2, y2 = max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3])
    inter = max(0, x2 - x1) * max(0, y2 - y1)
    u = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - inter
    return inter / u if u > 0 else 0


def verdict(dets):
    """앱 판정 규칙(제안): crack_iphone 있으면 불량, 아니면 가장 큰 박스의 기종."""
    if not dets: return '검출없음'
    if any(c == 0 for _, c, _ in dets): return CLASSES[0]
    return CLASSES[max(dets, key=lambda d: (d[0][2] - d[0][0]) * (d[0][3] - d[0][1]))[1]]


print('\n=== 4. 검증 사진 인식률 (학습에 안 쓴 사진, 신뢰도 45%) ===')
conf_m = defaultdict(Counter)
for n in va:
    truth = CLASSES[int(labels[AP / 'images' / n][0][0])]
    conf_m[truth][verdict(detect(imgs[n]))] += 1
tot = ok = 0
for truth in CLASSES:
    row = conf_m[truth]; t = sum(row.values()); k = row[truth]; tot += t; ok += k
    print(f'  {truth:30s} {k:3d}/{t:3d} = {k / t:6.1%}  틀린것 {dict((a, b) for a, b in row.items() if a != truth)}')
print(f'  전체 {ok}/{tot} = {ok / tot:.1%}')
clean = [n for n in va if n not in near_leak]
ck = sum(verdict(detect(imgs[n])) == CLASSES[int(labels[AP / "images" / n][0][0])] for n in clean)
print(f'  학습 사진과 거의 같은 것을 뺀 검증 {len(clean)}장: {ck}/{len(clean)} = {ck / max(1, len(clean)):.1%}')

print('\n=== 5. all_phones 학습에 안 들어간 사진으로 시험 ===')
expect = {'12Pro 실버 107장': 'iphone_12_pro_silver', '14 화이트 50장(옛)': 'iphone_14_white', '16 화이트 72장': 'iphon16_white',
          '파손 crack_phone 61장': 'crack_iphone', '12ProMax 파손 60장': 'crack_iphone', '컨베이어 실사진 8장': 'iphone15pro_Natural_Titanium'}
for k, exp in expect.items():
    fs = [p for p in old_files[k] if hashlib.sha256(p.read_bytes()).hexdigest() not in ap_hash]
    if not fs: print(f'  {k}: 전부 all_phones에 포함 → 생략'); continue
    res = Counter(verdict(detect(imread(p))) for p in fs)
    print(f'  {k:24s} 대상 {len(fs):3d}장, 기대 {exp:28s} 정답 {res[exp]:3d} = {res[exp] / len(fs):6.1%}  결과 {dict(res)}')
bg = ROOT / '04_Vision/models/background/empty_belt_reference.jpg'
print('  빈 벨트 사진:', verdict(detect(imread(bg))), '| 검은 화면:', verdict(detect(np.zeros((1080, 1920, 3), np.uint8))))
for p in sorted(old_files['컨베이어 실사진 8장']):
    d = detect(imread(p))
    print('   ', p.name, [(CLASSES[c], round(cf, 2), int((b[2] - b[0]))) for b, c, cf in d])
