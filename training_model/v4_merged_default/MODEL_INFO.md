# v4_merged_default — 통합 모델 (v5로 교체됨, 되돌리기용) (2026-10-21 오후, 학습 PC)

- 파일: `v4_merged_default.onnx`(앱용), `v4_merged_default.pt`, `classes.txt` — `all_phones/models/best.onnx`·`best.pt`와 같은 파일
- SHA256 (`v4_merged_default.onnx` = `all_phones/models/best.onnx`): `55ac3efd6fc19d86a1c86b23b7218bd1314216df9d90eb17be19979cf7c87cfd`
- 클래스·입출력: v1~v3와 같음 (0 crack_iphone · 1 iphon16_white · 2 iphone_12_pro_silver · 3 iphone15pro_Natural_Titanium · 4 iphone_14_white / 입력 [1, 3, 640, 640], 출력 [1, 9, 8400]) → 앱은 파일만 교체
- 학습 데이터: v3 데이터 1,731장 + **오후에 새로 찍은 우드락 컨베이어 15 Pro 35장** (50장 중 첫 촬영 묶음 15장은 시험용). 학습 1,397 / 검증 291.
- 설정: v3와 같음 (yolo26n, 640, 배치 16, Ultralytics 기본 증강). 222회째 자동 종료(122회째 최고).
- 검증 점수: P 0.989, R 0.968, mAP50 0.980, mAP50-95 0.837
- 인식률(앱 판정, 신뢰도 0.45): **새 15 Pro 시험 15장 15/15 (v3는 4/15, 11장을 12 Pro로 착각)**, 컨베이어 시험 전체 89/89 (v3 78/89), 12 Pro 실버 8/8, 예전 보류 78장 77/78. 원본 출력 `compare_result_20261021.txt`.
- 같이 학습한 `v4_merged_conveyor`(색 흔들기 작게·좌우 뒤집기 끔)는 시험 결과가 같아 학습 PC 바탕화면에 예비로만 둠.
