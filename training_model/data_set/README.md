# data_set — 최종 모델 v5 성능 그래프

**한 번에 받기:** `v5_성능그래프.zip` (아래 그래프 3개가 들어 있음) → 파일을 누르고 오른쪽 위 다운로드 버튼

| 파일 | 내용 |
|---|---|
| `v5_loss_curves.png` | **손실 함수 변화** — box(박스 위치), cls(클래스 분류), l1(박스 좌표). 학습=파랑, 검증=주황. 낮을수록 좋음 |
| `v5_metrics_curves.png` | **성능 지표 변화** — Precision, Recall, mAP50, mAP50-95 (검증 데이터 228장). 높을수록 좋음 |
| `v5_per_class_mAP50-95.png` | **클래스별 성능** (mAP50-95, 검증 데이터) |

## 한눈에

- 모델: `training_model/v5_merged_default/v5_merged_default.onnx` (클래스 6개)
- 278회 학습 → 100회 동안 더 좋아지지 않아 자동 종료, **178회째 모델 사용** (그래프의 점선)
- 검증 점수: **Precision 0.993 · Recall 0.993 · mAP50 0.991 · mAP50-95 0.861**
- 클래스별 mAP50-95: 12 Pro Max 깨짐 0.869 · 16 화이트 0.873 · 12 Pro 실버 0.778 · 15 Pro 0.887 · 14 화이트 0.861 · 8 깨짐 0.873
- 학습에 안 쓴 사진 155장 정답률: 153/155

학습 기록 원본(csv·json, YOLO가 그린 원본 그래프)은 `training_model/v5_merged_default/training_record/`에 있습니다.
