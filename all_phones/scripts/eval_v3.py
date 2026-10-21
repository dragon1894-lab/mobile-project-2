"""all_phones 모델 인식률 비교 (C# 앱과 같은 판정, eval_v2.py 의 Detector·verdict 를 그대로 사용).

시험 사진 (모두 학습·검증에 안 쓴 사진):
  1. all_phones/test_conveyor/<클래스>/   우드락 컨베이어 환경 시험 사진 + 빈 벨트 6장 (empty_belt = 아무것도 안 잡아야 정답)
  2. split.txt 의 test 78장 (옛 데이터 출처)
  3. from_local_pc_20261020/conveyor_real_8 (우드락 전 컨베이어 사진)
  4. 검은 화면

사용 (저장소 최상위 폴더에서):
    py -3.13 -X utf8 all_phones/scripts/eval_v3.py
        → 기본: training_model/v2/v2.onnx 와 all_phones/models/best.onnx(v3) 비교
    py -3.13 -X utf8 all_phones/scripts/eval_v3.py --model a.onnx --model b.onnx
"""
import argparse
import importlib.util
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

AP = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("eval_v2", AP / "scripts/eval_v2.py")
ev = importlib.util.module_from_spec(spec); spec.loader.exec_module(ev)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", action="append")
    models = [Path(m) for m in (ap.parse_args().model or [AP.parent / "training_model/v2/v2.onnx", AP / "models/best.onnx"])]
    old_test = [l.rsplit("\t", 1)[0] for l in (AP / "split.txt").read_text(encoding="utf-8-sig").splitlines()
                if l.strip() and not l.startswith("#") and l.rstrip().endswith("\ttest")]
    old_truth = {n: ev.CLASSES[int((AP / "labels" / f"{Path(n).stem}.txt").read_text().split()[0])] for n in old_test}
    conv8 = sorted(ev.CONV.iterdir())
    rows = []
    for path in models:
        det = ev.Detector(path)
        print(f"\n######## {path}")
        print("=== 1. 컨베이어 시험 사진 (test_conveyor) ===")
        ok = tot = 0
        for cdir in sorted((AP / "test_conveyor").iterdir()):
            exp = "검출없음" if cdir.name == "empty_belt" else cdir.name
            res, confs = Counter(), []
            for p in sorted(cdir.iterdir()):
                d = det(ev.imread(p)); res[ev.verdict(d)] += 1
                if d: confs.append(max(cf for _, _, cf in d))
            k, n = res[exp], sum(res.values()); ok += k; tot += n
            print(f"  {cdir.name:30s} {k:3d}/{n:3d} = {k / n:6.1%}  평균 최고 신뢰도 {np.mean(confs):.2f}" if confs else
                  f"  {cdir.name:30s} {k:3d}/{n:3d} = {k / n:6.1%}  평균 최고 신뢰도 -", f" 결과 {dict(res)}")
        print(f"  전체 {ok}/{tot} = {ok / tot:.1%}")
        by = defaultdict(Counter)
        for n in old_test:
            by[old_truth[n]][ev.verdict(det(ev.imread(AP / "images" / n)))] += 1
        k2 = sum(r[t] for t, r in by.items()); n2 = sum(sum(r.values()) for r in by.values())
        print(f"=== 2. 예전 보류 시험: {k2}/{n2} = {k2 / n2:.1%}")
        hit = known = crack = 0
        for p in conv8:
            v = ev.verdict(det(ev.imread(p))); exp = ev.CONV_TRUTH.get(p.name)
            crack += v == ev.CLASSES[0]
            if exp: known += 1; hit += v == exp
        print(f"=== 3. 예전 컨베이어 8장: 기종 {hit}/{known}, 정상→불량 {crack}/8")
        blk = ev.verdict(det(np.zeros((1080, 1920, 3), np.uint8)))
        print(f"=== 4. 검은 화면 -> {blk}")
        rows.append((path.name, ok, tot, k2, n2, hit, known, crack, blk))
    print("\n######## 요약")
    for r in rows:
        print(f"  {r[0]:28s} 컨베이어 {r[1]}/{r[2]}  예전 {r[3]}/{r[4]}  예전 컨베이어 기종 {r[5]}/{r[6]} 불량오판 {r[7]}  검은 화면 {r[8]}")


if __name__ == "__main__":
    main()
