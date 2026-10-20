"""휴대폰 사진을 학습 툴에 넣기 전에 정리한다 (김하균 요청, 2026-10-20 작성).

하는 일
- 사진의 회전 정보(EXIF Orientation)를 실제 픽셀에 적용한다.
  학습 툴(C# Image.FromStream)은 회전 정보를 무시하고 보여 주지만, YOLO(OpenCV)는 회전 정보를 적용해 읽는다.
  그대로 두면 세로로 찍은 사진에서 박스 위치가 학습 때 어긋난다.
- JPG로 저장하고 이름을 crack_iphone_0001.jpg 처럼 영어+번호로 바꾼다 (나중에 다른 사람 사진과 합칠 때 이름 충돌 방지).
- HEIC 사진은 pillow-heif 가 설치돼 있으면 함께 JPG로 바꾼다:  pip install pillow-heif

사용
  python fix_orientation.py --src "C:\\Users\\<사용자>\\Desktop\\아이폰 깨짐" --dst "C:\\Users\\<사용자>\\Desktop\\아이폰깨짐_정리"

원본 폴더는 건드리지 않는다. 결과 폴더(--dst)는 비어 있어야 한다.
주의: Python이 없는 PC에서 작성해 실행 시험은 하지 않았다. 처음에는 사진 몇 장으로 먼저 확인할 것.
"""
import argparse
from pathlib import Path

from PIL import Image, ImageOps

try:
    from pillow_heif import register_heif_opener

    register_heif_opener()
    HEIC_SUPPORTED = True
except ImportError:
    HEIC_SUPPORTED = False

EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".heic", ".heif"}


def main() -> None:
    parser = argparse.ArgumentParser(description="EXIF 회전 적용 + JPG 변환 + 영어 이름으로 복사")
    parser.add_argument("--src", required=True, help="원본 사진 폴더 (한글 이름 가능)")
    parser.add_argument("--dst", required=True, help="정리한 사진을 저장할 새 폴더")
    parser.add_argument("--prefix", default="crack_iphone", help="새 파일 이름 앞부분")
    args = parser.parse_args()

    src = Path(args.src)
    dst = Path(args.dst)
    if not src.is_dir():
        raise SystemExit(f"원본 폴더가 없습니다: {src}")
    dst.mkdir(parents=True, exist_ok=True)
    if any(dst.iterdir()):
        raise SystemExit(f"결과 폴더가 비어 있지 않습니다: {dst}")

    files = sorted(f for f in src.iterdir() if f.is_file() and f.suffix.lower() in EXTENSIONS)
    saved, skipped = 0, []
    for file in files:
        if file.suffix.lower() in {".heic", ".heif"} and not HEIC_SUPPORTED:
            skipped.append(f"{file.name} (HEIC: pip install pillow-heif 필요)")
            continue
        try:
            with Image.open(file) as image:
                fixed = ImageOps.exif_transpose(image).convert("RGB")
                saved += 1
                fixed.save(dst / f"{args.prefix}_{saved:04d}.jpg", quality=95)
        except Exception as error:  # 깨진 파일 등은 건너뛰고 이름을 알려 준다
            skipped.append(f"{file.name} ({error})")

    print(f"정리 완료: {saved}장 → {dst}")
    if skipped:
        print(f"건너뜀 {len(skipped)}장:")
        for item in skipped:
            print("  -", item)


if __name__ == "__main__":
    main()
