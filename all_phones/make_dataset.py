"""all_phones 학습 데이터(dataset/) 다시 만들기.

images/ 와 labels/ 를 읽어 dataset/ 을 새로 만든다 (기존 dataset/ 은 지우고 다시 만듦).
- 분할은 split.txt 를 그대로 따른다 (train / val / test).
  split.txt 는 "거의 같은 사진(dHash 해밍거리 4 이하) 묶음" 단위로 나눈 것이라
  비슷한 사진이 학습과 검증·시험에 갈라져 들어가지 않는다.
  (v3 에서 더한 컨베이어 사진 conv_* 은 출처마다 촬영 시간 순 마지막 약 15 % 를 val 로 둔 것)
- test 는 학습에도 검증에도 쓰지 않는 보류 시험용이다 (scripts/eval_v2.py, scripts/eval_v3.py 가 사용).
  컨베이어 시험 사진은 따로 test_conveyor/<클래스>/ 에 있다.
- split.txt 에 없는 새 사진은 train 으로 들어간다. 새 사진을 검증·시험에 넣고 싶으면 split.txt 에 직접 적는다.
- 라벨 파일이 없는 사진은 빠진다. 빈 라벨(박스 없음, 예: 빈 벨트)은 split.txt 에 있으면 배경 사진으로 들어간다.

사용 (이 파일이 있는 all_phones 폴더에서):
    py -3.13 make_dataset.py
결과: dataset/data.yaml (val 로 검증), dataset/data_test.yaml (test 로 검증)
"""
import shutil
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent
EXTS = {".jpg", ".jpeg", ".png", ".bmp"}


def main() -> None:
    classes = [c.strip() for c in (ROOT / "classes.txt").read_text(encoding="utf-8-sig").splitlines() if c.strip()]
    split = {}
    for line in (ROOT / "split.txt").read_text(encoding="utf-8-sig").splitlines():
        if line.strip() and not line.startswith("#"):
            name, part = line.rsplit("\t", 1)
            split[name] = part.strip()

    ds = ROOT / "dataset"
    if ds.exists():
        shutil.rmtree(ds)
    for part in ("train", "val", "test"):
        for sub in ("images", "labels"):
            (ds / sub / part).mkdir(parents=True)

    count, skipped = Counter(), Counter()
    for img in sorted(p for p in (ROOT / "images").iterdir() if p.suffix.lower() in EXTS):
        lab = ROOT / "labels" / f"{img.stem}.txt"
        if not lab.exists():
            skipped["라벨 없음"] += 1
            continue
        rows = [l.split() for l in lab.read_text(encoding="utf-8-sig").splitlines() if l.strip()]
        if not rows and img.name not in split:
            skipped["빈 라벨인데 split.txt에 없음"] += 1
            continue
        part = split.get(img.name, "train")
        if img.name not in split:
            skipped["split.txt에 없어서 train"] += 1
        shutil.copy2(img, ds / "images" / part / img.name)
        shutil.copy2(lab, ds / "labels" / part / lab.name)
        count[(int(rows[0][0]) if rows else "empty", part)] += 1

    base = [f"path: '{ds.as_posix()}'", "train: images/train"]
    names = ["names:"] + [f"  {i}: '{c}'" for i, c in enumerate(classes)]
    (ds / "data.yaml").write_text("\n".join(base + ["val: images/val"] + names) + "\n", encoding="utf-8")
    (ds / "data_test.yaml").write_text("\n".join(base + ["val: images/test"] + names) + "\n", encoding="utf-8")

    for cid, name in enumerate(classes):
        print(f"{cid} {name:30s} train {count[(cid, 'train')]:4d}  val {count[(cid, 'val')]:4d}  test {count[(cid, 'test')]:3d}")
    print(f"  빈 벨트(박스 없음)                train {count[('empty', 'train')]:4d}  val {count[('empty', 'val')]:4d}")
    if skipped:
        print("참고:", dict(skipped))
    print(f"-> {ds / 'data.yaml'}")


if __name__ == "__main__":
    main()
