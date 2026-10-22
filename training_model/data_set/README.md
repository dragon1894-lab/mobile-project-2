# data_set — 최종 모델 v5_merged_default 성능 지표·손실 함수 그래프 (2026-10-22)

모델: `training_model/v5_merged_default/v5_merged_default.onnx` (클래스 6개, 인수인계 `training_model/HANDOFF_v5.md`)

| 파일 | 내용 |
|---|---|
| `v5_loss_curves.png` | **손실 함수 변화** — box(박스 위치), cls(클래스 분류), l1(박스 좌표) 손실을 학습·검증 데이터로 에포크마다 그림. 낮을수록 좋음 |
| `v5_metrics_curves.png` | **성능 지표 변화** — Precision, Recall, mAP50, mAP50-95 (검증 데이터 228장). 차이가 보이도록 칸마다 세로축 범위를 다르게 확대함 |
| `v5_per_class_mAP50-95.png` | 클래스별 mAP50-95 (검증 데이터) |
| `v5_metrics_summary.json` | 최고 회차 값, 클래스별 P / R / mAP50 / mAP50-95 (검증·보류 시험) |
| `v5_results.csv` | Ultralytics 학습 기록 원본 (278회, 에포크마다 손실·지표·학습률) |
| `ultralytics_*.png` | Ultralytics가 그린 원본 그래프 (results, 혼동 행렬, PR·F1 곡선) |

## 한눈에

- 학습 278회 → 100회 동안 더 좋아지지 않아 자동 종료, **178회째 모델 사용** (그래프의 점선)
- 검증: **Precision 0.993, Recall 0.993, mAP50 0.991, mAP50-95 0.861**
- 손실: 학습 손실은 끝까지 내려가고, 검증 손실은 100~150회쯤부터 평평 → 그 뒤로는 더 배울 게 적음(과적합 직전에서 자동 종료)
- 클래스별 mAP50-95: 12 Pro Max 깨짐 0.869 · 16 화이트 0.873 · 12 Pro 실버 0.778 · 15 Pro 0.887 · 14 화이트 0.861 · 8 깨짐 0.873
- 앱과 같은 판정으로 잰 정답률(학습에 안 쓴 사진 155장): 153/155 — `training_model/v5_merged_default/compare_v4_v5_20261022.txt`

그래프 색은 Precision 등 한 칸에 한 지표(파랑), 손실은 학습=파랑·검증=주황입니다.
