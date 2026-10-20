"""all_phones 모델 인식률 평가 (C# 앱과 같은 방식).

ONNX, 신뢰도 0.45, IoU 0.45, letterbox 114(회색).
판정: crack_iphone 박스가 하나라도 있으면 불량, 아니면 가장 큰 박스의 기종.
(from_local_pc_20261020/scripts/eval_allphones.py 의 detect/verdict 와 같은 코드)

시험 사진 (모두 학습·검증에 안 쓴 사진):
  A. split.txt 의 test 78장 (옛 데이터에서 묶음째로 떼어 둔 보류 사진)
  B. from_local_pc_20261020/conveyor_real_8 (실제 컨베이어 카메라, 모두 정상 폰, 라벨 없음)
  C. 검은 화면

사용 (저장소 최상위 폴더에서):
    py -3.13 -X utf8 all_phones/scripts/eval_v2.py
    py -3.13 -X utf8 all_phones/scripts/eval_v2.py --model 다른모델.onnx [--model 또다른.onnx]
필요 패키지: numpy, opencv-python, onnx, onnxruntime
"""
import argparse
import ast
from collections import Counter, defaultdict
from pathlib import Path

import cv2
import numpy as np
import onnx
import onnxruntime as ort

AP = Path(__file__).resolve().parent.parent  # all_phones
CONV = AP / "from_local_pc_20261020/conveyor_real_8/images"
CONV_TRUTH = {  # from_local_pc_20261020/HANDOFF.md 2번 표. None = 기종 미기록(정상인 것만 확실)
    "WIN_20261017_10_57_11_Pro.jpg": "iphone15pro_Natural_Titanium",
    "WIN_20261017_10_57_11_Pro_2.jpg": "iphone15pro_Natural_Titanium",
    "WIN_20261017_11_08_19_Pro.jpg": "iphone15pro_Natural_Titanium",
    "WIN_20261017_11_08_25_Pro.jpg": "iphone15pro_Natural_Titanium",
    "WIN_20261017_11_08_36_Pro.jpg": "iphon16_white",
    "WIN_20261017_11_08_42_Pro.jpg": "iphon16_white",
    "WIN_20261017_11_15_18_Pro.jpg": None,
    "WIN_20261017_11_15_24_Pro.jpg": None,
}
CLASSES = [c.strip() for c in (AP / "classes.txt").read_text(encoding="utf-8-sig").splitlines() if c.strip()]
CONF, IOU = 0.45, 0.45


def imread(p):
    return cv2.imdecode(np.fromfile(str(p), np.uint8), cv2.IMREAD_COLOR)


def iou(a, b):
    x1, y1, x2, y2 = max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3])
    inter = max(0, x2 - x1) * max(0, y2 - y1)
    u = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - inter
    return inter / u if u > 0 else 0


class Detector:
    def __init__(self, path):
        meta = {p.key: p.value for p in onnx.load(str(path), load_external_data=False).metadata_props}
        names = ast.literal_eval(meta["names"])
        self.names = [names[i] for i in sorted(names)]
        if self.names != CLASSES:
            raise SystemExit(f"모델 클래스가 classes.txt 와 다릅니다: {self.names}")
        self.s = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
        self.W = self.H = self.s.get_inputs()[0].shape[2]

    def __call__(self, img):
        W, H = self.W, self.H
        s = min(W / img.shape[1], H / img.shape[0])
        rw, rh = max(1, round(img.shape[1] * s)), max(1, round(img.shape[0] * s))
        c = np.full((H, W, 3), 114, np.uint8); l, t = int((W - rw) / 2), int((H - rh) / 2)
        c[t:t + rh, l:l + rw] = cv2.resize(img, (rw, rh))
        x = cv2.cvtColor(c, cv2.COLOR_BGR2RGB).transpose(2, 0, 1)[None].astype(np.float32) / 255
        o = self.s.run(None, {self.s.get_inputs()[0].name: x})[0][0]
        if o.shape[0] != len(self.names) + 4: o = o.T
        sc = o[4:]; cls = sc.argmax(0); conf = sc.max(0); keep = conf >= CONF
        b = o[:4, keep].T; cls = cls[keep]; conf = conf[keep]
        xyxy = np.stack([(b[:, 0] - b[:, 2] / 2 - l) / s, (b[:, 1] - b[:, 3] / 2 - t) / s,
                         (b[:, 0] + b[:, 2] / 2 - l) / s, (b[:, 1] + b[:, 3] / 2 - t) / s], 1) if len(b) else np.zeros((0, 4))
        out = []
        for i in np.argsort(-conf):
            bb = xyxy[i]
            if any(c2 == cls[i] and iou(bb, b2) > IOU for b2, c2, _ in out): continue
            out.append((bb, int(cls[i]), float(conf[i])))
        return out


def verdict(dets):
    if not dets: return "검출없음"
    if any(c == 0 for _, c, _ in dets): return CLASSES[0]
    return CLASSES[max(dets, key=lambda d: (d[0][2] - d[0][0]) * (d[0][3] - d[0][1]))[1]]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", action="append", help="평가할 ONNX (여러 번 가능). 기본: all_phones/models/best.onnx")
    models = [Path(m) for m in (ap.parse_args().model or [AP / "models/best.onnx"])]

    test = [l.rsplit("\t", 1)[0] for l in (AP / "split.txt").read_text(encoding="utf-8-sig").splitlines()
            if l.strip() and not l.startswith("#") and l.rstrip().endswith("\ttest")]
    truth = {n: CLASSES[int((AP / "labels" / f"{Path(n).stem}.txt").read_text().split()[0])] for n in test}
    source = lambda n: n.split("__")[0].replace("old_", "")

    for path in models:
        det = Detector(path)
        print(f"\n######## {path}")
        print(f"=== A. 보류 시험 사진 {len(test)}장 (학습·검증에 안 씀) ===")
        by = defaultdict(Counter)
        for n in test:
            by[(source(n), truth[n])][verdict(det(imread(AP / "images" / n)))] += 1
        tot = ok = 0
        for (src, tr), res in sorted(by.items()):
            k, m = res[tr], sum(res.values()); tot += m; ok += k
            print(f"  {src:28s} 정답 {tr:30s} {k:3d}/{m:3d} = {k / m:6.1%}  결과 {dict(res)}")
        print(f"  전체 {ok}/{tot} = {ok / tot:.1%}")
        print("=== B. 실제 컨베이어 사진 (모두 정상 폰) ===")
        hit = known = crack = 0
        files = sorted(CONV.iterdir())
        for p in files:
            d = det(imread(p)); v = verdict(d); exp = CONV_TRUTH.get(p.name)
            crack += v == CLASSES[0]
            if exp: known += 1; hit += v == exp
            print(f"   {p.name:34s} 정답 {exp or '(정상, 기종 미기록)':30s} -> {v:30s} {[(CLASSES[c], round(cf, 2)) for _, c, cf in d]}")
        print(f"  기종 맞음 {hit}/{known} | 정상인데 불량 판정 {crack}/{len(files)}")
        print("=== C. 검은 화면 ->", verdict(det(np.zeros((1080, 1920, 3), np.uint8))))


if __name__ == "__main__":
    main()
