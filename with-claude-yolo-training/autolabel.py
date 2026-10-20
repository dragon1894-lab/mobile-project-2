"""도우미 모델로 아직 라벨이 없는 사진에 박스를 자동으로 그린다 (김하균 요청, 2026-10-20 작성).

순서
1. 사람이 학습 툴에서 20~30장을 직접 라벨링한다.
2. 그 사진들로 도우미 모델을 짧게 학습한다 (README의 4단계).
3. 이 스크립트로 나머지 사진에 박스를 자동으로 그려 학습 툴 프로젝트의 labels 폴더에 저장한다.
4. 사람이 학습 툴에서 autolabel_list.txt 에 적힌 사진을 하나씩 열어 확인·수정하고 '라벨 저장'을 누른다.

규칙
- 이미 labels\\사진이름.txt 가 있는 사진은 건드리지 않는다 (사람이 그린 라벨 보호).
- 아무것도 못 찾은 사진은 라벨 파일을 만들지 않는다 → 학습 툴에서 '미저장'으로 보이므로 직접 그린다.
- 도우미 모델의 클래스 번호가 프로젝트 classes.txt 와 다르면 번호가 꼬이므로 바로 멈춘다.

사용
  python autolabel.py --project "C:\\Users\\<사용자>\\Desktop\\crack_iphone" --model "C:\\Users\\<사용자>\\Desktop\\crack_iphone\\helper_model\\best.pt"

주의: Python이 없는 PC에서 작성해 실행 시험은 하지 않았다. 처음 실행 결과를 학습 툴에서 몇 장 확인할 것.
"""
import argparse
from pathlib import Path

EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp"}  # 학습 툴(YoloProject.cs)과 같은 확장자


def main() -> None:
    parser = argparse.ArgumentParser(description="도우미 YOLO 모델로 라벨이 없는 사진에 박스 자동 생성")
    parser.add_argument("--project", required=True, help="학습 툴 프로젝트 폴더 (images, labels, classes.txt 가 있는 곳)")
    parser.add_argument("--model", required=True, help="도우미 모델 best.pt 경로")
    parser.add_argument("--conf", type=float, default=0.25, help="이 신뢰도 이상인 박스만 저장")
    parser.add_argument("--imgsz", type=int, default=640)
    args = parser.parse_args()

    from ultralytics import YOLO

    root = Path(args.project)
    images = root / "images"
    labels = root / "labels"
    labels.mkdir(exist_ok=True)
    classes = [line.strip() for line in (root / "classes.txt").read_text(encoding="utf-8-sig").splitlines() if line.strip()]

    model = YOLO(args.model)
    for class_id, name in model.names.items():
        expected = classes[class_id] if class_id < len(classes) else "(없음)"
        if expected != name:
            raise SystemExit(f"클래스 번호가 다릅니다: 모델 {class_id}={name}, classes.txt {class_id}={expected}")

    made, not_found, skipped = [], [], 0
    for image in sorted(p for p in images.iterdir() if p.suffix.lower() in EXTENSIONS):
        label = labels / (image.stem + ".txt")
        if label.exists():
            skipped += 1
            continue
        result = model.predict(source=str(image), conf=args.conf, imgsz=args.imgsz, verbose=False)[0]
        lines = [
            f"{int(class_id)} {x:.6f} {y:.6f} {w:.6f} {h:.6f}"
            for class_id, (x, y, w, h) in zip(result.boxes.cls.tolist(), result.boxes.xywhn.tolist())
        ]
        if not lines:
            not_found.append(image.name)
            continue
        label.write_text("\n".join(lines) + "\n", encoding="utf-8")
        made.append(image.name)

    (root / "autolabel_list.txt").write_text("\n".join(made) + ("\n" if made else ""), encoding="utf-8")
    print(f"자동 라벨 {len(made)}장 (autolabel_list.txt), 못 찾음 {len(not_found)}장 (직접 그리기), 이미 라벨 있음 {skipped}장")
    if not_found:
        print("못 찾은 사진:", ", ".join(not_found))


if __name__ == "__main__":
    main()
