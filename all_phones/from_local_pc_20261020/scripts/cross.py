"""Run every deployed ONNX on images from each dataset; report how often each class fires.
Mirrors the C# letterbox (114 pad, RGB, /255) and per-anchor argmax."""
import ast, sys, json
from pathlib import Path
import numpy as np, cv2, onnxruntime as ort, onnx

ROOT = Path(sys.argv[1])
MODELS = sorted((ROOT / '04_Vision/models').rglob('*.onnx'))
EXT = {'.jpg', '.jpeg', '.png'}
SETS = {
    '12pro_silver(val)': 'imports/phone-models-20261020/iphone_12pro_silver/dataset/images/val',
    '14_white(val)': 'imports/phone-models-20261020/iphone_14_white/dataset/images/val',
    '16_white(val)': 'imports/phone-models-20261020/iphone16_white/dataset/images/val',
    '12promax_crack(val)': 'imports/phone-models-20261020/iphone_12_pro_max_Crack/dataset/images/val',
    'crack_phone(val)': 'imports/three-model-merge-20261019-145921/broken_phone/broken_phone/dataset/images/val',
    'conveyor_real(root images)': 'images',
}


def imread(p):
    return cv2.imdecode(np.fromfile(str(p), np.uint8), cv2.IMREAD_COLOR)


def prep(img, w, h):
    s = min(w / img.shape[1], h / img.shape[0])
    rw, rh = max(1, round(img.shape[1] * s)), max(1, round(img.shape[0] * s))
    canvas = np.full((h, w, 3), 114, np.uint8)
    l, t = int((w - rw) / 2), int((h - rh) / 2)
    canvas[t:t + rh, l:l + rw] = cv2.resize(img, (rw, rh), interpolation=cv2.INTER_LINEAR)
    return (cv2.cvtColor(canvas, cv2.COLOR_BGR2RGB).transpose(2, 0, 1)[None].astype(np.float32) / 255)


sessions = []
for m in MODELS:
    meta = {p.key: p.value for p in onnx.load(str(m), load_external_data=False).metadata_props}
    names = ast.literal_eval(meta['names'])
    names = [names[i] for i in sorted(names)]
    s = ort.InferenceSession(str(m), providers=['CPUExecutionProvider'])
    shp = s.get_inputs()[0].shape
    label = m.parent.name[:3] + ('/models' if m.parent.name == 'models' else '')
    if m.parent.name == 'models' and m.parent.parent.name == '04_Vision':
        label = 'MAIN'
    elif m.parent.name == 'models':
        label = m.parent.parent.name[:3] + 'new'
    sessions.append((label, names, s, shp[3], shp[2]))

images = {k: sorted(p for p in (ROOT / v).iterdir() if p.suffix.lower() in EXT) for k, v in SETS.items()}
out = {}
for label, names, s, w, h in sessions:
    row = {}
    for setname, files in images.items():
        hits = {}
        for f in files:
            img = imread(f)
            o = s.run(None, {s.get_inputs()[0].name: prep(img, w, h)})[0][0]
            if o.shape[0] != len(names) + 4:
                o = o.T
            scores = o[4:]
            cls = scores.argmax(0)
            conf = scores.max(0)
            for ci, n in enumerate(names):
                thr = 0.10 if n in ('crack', 'crack_phone') else 0.45
                if ((cls == ci) & (conf >= thr)).any():
                    hits[n] = hits.get(n, 0) + 1
        row[setname] = {k: f'{v}/{len(files)}' for k, v in hits.items() if k not in ('target_iphone', 'crack')}
        row[setname + '_internal'] = {k: f'{v}/{len(files)}' for k, v in hits.items() if k in ('target_iphone', 'crack')}
    out[label] = row
    print('==', label, names)
    for k, v in row.items():
        if v:
            print('   ', k, v)
    sys.stdout.flush()
