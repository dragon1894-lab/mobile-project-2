# v3_merged_default — 지금 쓰는 통합 모델 (2026-10-21, 학습 PC)

- 파일: `v3_merged_default.onnx`(앱용), `v3_merged_default.pt`, `classes.txt` — `all_phones/models/best.onnx`·`best.pt`와 같은 파일
- 클래스: v1·v2와 같음 (0 crack_iphone · 1 iphon16_white · 2 iphone_12_pro_silver · 3 iphone15pro_Natural_Titanium · 4 iphone_14_white)
- ONNX: 입력 `images` [1, 3, 640, 640] / 출력 `output0` [1, 9, 8400] (v2와 같음 → 앱 코드 변경 없이 파일만 교체)
- 학습 데이터: v2 데이터 + **우드락 컨베이어 환경 사진** (16 화이트 90, 15 Pro 92, 14 화이트 80, 12 Pro 실버 108, 깨진 폰 174(아이폰8 90 + 12 Pro Max 84 → 모두 `crack_iphone`), 빈 벨트 24). 학습 1,367 / 검증 286.
- 설정: yolo26n, 640, 배치 16, Ultralytics 기본 증강(v2와 같음), 248회째 자동 종료(148회째 최고). `settings.json` 참고.
- 검증 점수: P 0.979, R 0.973, mAP50 0.980, mAP50-95 0.834
- 인식률(앱과 같은 판정, 신뢰도 0.45): 새 컨베이어 시험 74장 **100%** (v2 28.4%), 예전 보류 시험 78장 98.7%, 예전 컨베이어 8장 기종 6/6(정상→불량 2). 원본 출력 `compare_result_20261021.txt`.
- 같이 학습한 후보 `v3_merged_conveyor`(색 흔들기 작게·좌우 뒤집기 끔), `v3_conveyor_only`(컨베이어 사진만)는 학습 PC 바탕화면에만 있음. 자세한 비교는 `all_phones/README.md`.
