import ast, csv, hashlib, json, sys
from collections import Counter, defaultdict
from pathlib import Path
from PIL import Image
import onnx

ROOT = Path(sys.argv[1])
EXT = {'.jpg', '.jpeg', '.png', '.bmp', '.webp'}


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def yaml_names(p):
    out = {}
    for line in p.read_text(encoding='utf-8-sig').splitlines():
        t = line.strip()
        if ':' in t:
            k, v = t.split(':', 1)
            if k.strip().isdigit():
                out[int(k)] = v.strip().strip("'\"")
    return out


PROJECTS = [
    'imports/phone-models-20261020/iphone_12pro_silver',
    'imports/phone-models-20261020/iphone_14_white',
    'imports/phone-models-20261020/iphone16_white',
    'imports/phone-models-20261020/iphone_12_pro_max_Crack',
    'imports/three-model-merge-20261019-145921/broken_phone/broken_phone',
    'imports/three-model-merge-20261019-145921/iphone16_white/iphone16_white',
    'training/iphone16_white_20261019',
    'training/broken_phone_20261019',
    'training/full_phone_20261019',
    'training/full_phone_detector_20261019',
    'imports/phone-merge-20261017-121058/labeled-project',
    '04_Vision/models/alternatives/11_아이폰14_화이트_추가학습_20261020',
    '.',
]

global_hash = defaultdict(list)
report = {}
for proj in PROJECTS:
    base = ROOT / proj
    ds = base / 'dataset'
    if not (ds / 'data.yaml').exists():
        report[proj] = 'no dataset/data.yaml'
        continue
    names = yaml_names(ds / 'data.yaml')
    r = {'names': names, 'issues': [], 'split': {}, 'class_boxes': {}, 'class_images': {}}
    split_hash = defaultdict(dict)
    cls_boxes = defaultdict(Counter)
    cls_imgs = defaultdict(Counter)
    for split in ('train', 'val', 'test'):
        idir = ds / 'images' / split
        ldir = ds / 'labels' / split
        if not idir.exists():
            continue
        imgs = [p for p in idir.iterdir() if p.suffix.lower() in EXT]
        labels = {p.stem: p for p in ldir.glob('*.txt')} if ldir.exists() else {}
        stems = {p.stem for p in imgs}
        orphan = sorted(set(labels) - stems)
        if orphan:
            r['issues'].append(f'{split}: label without image: {orphan[:5]} ({len(orphan)})')
        empty = []
        for img in imgs:
            h = sha(img)
            split_hash[split][h] = img.name
            global_hash[h].append(f'{proj}:{split}/{img.name}')
            try:
                with Image.open(img) as im:
                    W, H = im.size
                    im.verify()
            except Exception as e:
                r['issues'].append(f'{split}/{img.name}: corrupt image {e}')
                continue
            lab = labels.get(img.stem)
            if lab is None:
                r['issues'].append(f'{split}/{img.name}: missing label')
                continue
            lines = [l.split() for l in lab.read_text(encoding='utf-8-sig').splitlines() if l.strip()]
            if not lines:
                empty.append(img.name)
            seen = set()
            for i, f in enumerate(lines):
                if len(f) != 5:
                    r['issues'].append(f'{split}/{lab.name}:{i+1}: {len(f)} fields (seg/poly?) {f[:6]}')
                    continue
                try:
                    c = int(f[0]); x, y, w, hh = map(float, f[1:])
                except ValueError:
                    r['issues'].append(f'{split}/{lab.name}:{i+1}: parse error {f}')
                    continue
                if c not in names:
                    r['issues'].append(f'{split}/{lab.name}:{i+1}: class id {c} not in data.yaml')
                if not all(0 <= v <= 1 for v in (x, y, w, hh)) or w <= 0 or hh <= 0:
                    r['issues'].append(f'{split}/{lab.name}:{i+1}: coords out of range {f}')
                if x - w / 2 < -0.01 or x + w / 2 > 1.01 or y - hh / 2 < -0.01 or y + hh / 2 > 1.01:
                    r['issues'].append(f'{split}/{lab.name}:{i+1}: box exceeds image {f}')
                if w * W < 8 or hh * H < 8:
                    r['issues'].append(f'{split}/{lab.name}:{i+1}: tiny box {w*W:.0f}x{hh*H:.0f}px')
                n = names.get(c, f'ID{c}')
                cls_boxes[n][split] += 1
                seen.add(n)
            for n in seen:
                cls_imgs[n][split] += 1
            if len(lines) > 1:
                cnt = Counter(l[0] for l in lines)
                r.setdefault('multi_box_images', []).append(f'{split}/{img.name}:{dict(cnt)}')
        r['split'][split] = {'images': len(imgs), 'labels': len(labels), 'unique': len(split_hash[split]), 'empty_labels': empty}
    ov = set(split_hash['train']) & set(split_hash['val'])
    if ov:
        r['issues'].append(f'train/val identical images: {[split_hash["val"][h] for h in ov]}')
    r['class_boxes'] = {k: dict(v) for k, v in cls_boxes.items()}
    r['class_images'] = {k: dict(v) for k, v in cls_imgs.items()}
    r['unused_classes'] = [n for n in names.values() if n not in cls_boxes]
    # root images/labels (raw labeling area) vs dataset split
    rimg = base / 'images'; rlab = base / 'labels'
    if rimg.exists() and proj != '.':
        rimgs = [p for p in rimg.iterdir() if p.suffix.lower() in EXT]
        rl = {p.stem for p in rlab.glob('*.txt')} if rlab.exists() else set()
        r['raw'] = {'images': len(rimgs), 'labels': len(rl),
                    'images_without_label': sorted(p.name for p in rimgs if p.stem not in rl)}
        ds_hashes = set(split_hash['train']) | set(split_hash['val'])
        r['raw']['raw_not_in_dataset'] = sorted(p.name for p in rimgs if sha(p) not in ds_hashes)
    # metrics
    for res in base.rglob('results.csv'):
        rows = list(csv.DictReader(res.open(encoding='utf-8')))
        if rows:
            rows = [{k.strip(): v for k, v in row.items()} for row in rows]
            best = max(rows, key=lambda x: float(x.get('metrics/mAP50-95(B)', 0) or 0))
            last = rows[-1]
            r.setdefault('metrics', {})[str(res.relative_to(base))] = {
                'epochs': len(rows),
                'last': {k: last[k] for k in ('metrics/precision(B)', 'metrics/recall(B)', 'metrics/mAP50(B)', 'metrics/mAP50-95(B)') if k in last},
                'best_mAP50-95': best.get('metrics/mAP50-95(B)'), 'best_epoch': best.get('epoch')}
    # onnx
    for o in base.rglob('*.onnx'):
        try:
            g = onnx.load(str(o), load_external_data=False)
            md = {i.key: i.value for i in g.metadata_props}
            inp = [d.dim_value for d in g.graph.input[0].type.tensor_type.shape.dim]
            r.setdefault('onnx', {})[str(o.relative_to(base))] = {'names': md.get('names'), 'imgsz': md.get('imgsz'), 'input': inp, 'sha': sha(o)[:16], 'data': md.get('description')}
        except Exception as e:
            r.setdefault('onnx', {})[str(o.relative_to(base))] = f'ERR {e}'
    report[proj] = r

# Cross-dataset dup summary
cross = defaultdict(Counter)
for h, locs in global_hash.items():
    projs = sorted({l.split(':')[0] for l in locs})
    if len(projs) > 1:
        cross[' & '.join(projs)][''] += 1
report['_cross_dataset_shared_images'] = {k: v[''] for k, v in cross.items()}

# deployed models
dep = {}
for o in sorted((ROOT / '04_Vision/models').rglob('*.onnx')):
    g = onnx.load(str(o), load_external_data=False)
    md = {i.key: i.value for i in g.metadata_props}
    inp = [d.dim_value for d in g.graph.input[0].type.tensor_type.shape.dim]
    out = [d.dim_value for d in g.graph.output[0].type.tensor_type.shape.dim]
    pkg = ROOT / 'output/ProductionLine_AdminStatus_AllModels_20261019/User/Models' / o.relative_to(ROOT / '04_Vision/models')
    dep[str(o.relative_to(ROOT))] = {'names': md.get('names'), 'in': inp, 'out': out, 'sha': sha(o)[:16],
                                      'pkg_match': pkg.exists() and sha(pkg) == sha(o), 'date': md.get('date')}
report['_deployed_onnx'] = dep
print(json.dumps(report, ensure_ascii=False, indent=1))
