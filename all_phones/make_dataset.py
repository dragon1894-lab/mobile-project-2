"""all_phones 학습 데이터(dataset/) 다시 만들기.

images/ 와 labels/ 를 읽어 dataset/ 을 새로 만든다 (기존 dataset/ 은 지우고 다시 만듦).
- 클래스마다 20 %를 검증용(val)으로 뗀다 → 모든 클래스가 검증에 들어감
- 파일 이름 해시(FNV-1a)로 나누므로 몇 번을 실행해도 같은 결과
- 라벨 파일이 없거나 비어 있는 사진은 빠진다

사용 (이 파일이 있는 all_phones 폴더에서):
    py -3.13 make_dataset.py
"""
import shutil
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent
EXTS = {".jpg", ".jpeg", ".png", ".bmp"}
VAL_RATIO = 0.2


def fnv(s: str) -> int:
    h = 2166136261
    for ch in s:
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def main() -> None:
    classes = [c.strip() for c in (ROOT / "classes.txt").read_text(encoding="utf-8-sig").splitlines() if c.strip()]
    by_class = defaultdict(list)
    for img in sorted(p for p in (ROOT / "images").iterdir() if p.suffix.lower() in EXTS):
        lab = ROOT / "labels" / f"{img.stem}.txt"
        rows = [l.split() for l in lab.read_text(encoding="utf-8-sig").splitlines() if l.strip()] if lab.exists() else []
        if rows:
            by_class[int(rows[0][0])].append(img.name)

    ds = ROOT / "dataset"
    if ds.exists():
        shutil.rmtree(ds)
    for sub in ("images/train", "images/val", "labels/train", "labels/val"):
        (ds / sub).mkdir(parents=True)

    count = Counter()
    for cid, names in sorted(by_class.items()):
        order = sorted(names, key=fnv)
        n_val = max(1, round(len(order) * VAL_RATIO))
        for i, name in enumerate(order):
            part = "val" if i < n_val else "train"
            stem = Path(name).stem
            shutil.copy2(ROOT / "images" / name, ds / "images" / part / name)
            shutil.copy2(ROOT / "labels" / f"{stem}.txt", ds / "labels" / part / f"{stem}.txt")
            count[(cid, part)] += 1

    yaml = [f"path: '{ds.as_posix()}'", "train: images/train", "val: images/val", "names:"]
    yaml += [f"  {i}: '{c}'" for i, c in enumerate(classes)]
    (ds / "data.yaml").write_text("\n".join(yaml) + "\n", encoding="utf-8")

    for cid, name in enumerate(classes):
        print(f"{cid} {name:30s} train {count[(cid, 'train')]:4d}  val {count[(cid, 'val')]:4d}")
    print(f"-> {ds / 'data.yaml'}")


if __name__ == "__main__":
    main()
