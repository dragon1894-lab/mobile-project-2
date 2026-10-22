# v5_merged_default — 최종 통합 모델 (2026-10-22, 학습 PC)

- 파일: `v5_merged_default.onnx`(앱용), `v5_merged_default.pt`, `classes.txt`, `model_info.json`(앱 `04_Vision/models/`에 그대로 넣을 수 있게 작성), `settings.json`, `compare_v4_v5_20261022.txt`
- SHA256 (`v5_merged_default.onnx`): `9fb6d967b016862953b3f5e9521da4b55850d4f97120bcf954f6f8ea1d92038b`
- 입력 `images` [1, 3, 640, 640] / 출력 `output0` **[1, 10, 8400]** (클래스 6개 → v4의 [1, 9, 8400]과 다름)
- 클래스 (김하균님 결정 2026-10-22):

  | 번호 | 이름 | 뜻 | v4 이름 |
  |---|---|---|---|
  | 0 | `iphone12_promax_crack` | **불량** — 깨진 12 Pro Max | `crack_iphone` (일부) |
  | 1 | `iphone16_white` | 정상 — 16 화이트 | `iphon16_white` |
  | 2 | `iphone12_pro_silver` | 정상 — 12 Pro 실버 | `iphone_12_pro_silver` |
  | 3 | `iphone15_pro_Natural_Titanium` | 정상 — 15 Pro 내추럴 티타늄 | `iphone15pro_Natural_Titanium` |
  | 4 | `iphone14_white` | 정상 — 14 화이트 | `iphone_14_white` |
  | 5 | `iphone8_crack` | **불량** — 깨진 아이폰8 | `crack_iphone` (일부) |

- 학습 데이터: v4 데이터(`all_phones`)에서 **번호·이름만 바꿈(박스 그대로, 라벨 다시 안 땀)**. 깨진 폰은 컨베이어 깨진 12 Pro Max 84장 + 옛 12 Pro Max 파손 60장 → 0번, 컨베이어 깨진 아이폰8 90장 → 5번. **다른 깨진 폰 사진 230장**(`아이폰깨짐` 165, 옛 crack_phone 59, broken_phone_extra 6)은 8·12 Pro Max가 아니라서 **뺌**. 학습 1,248 / 검증 228 (빈 벨트 포함).
- 설정: v4와 같음 (yolo26n, 640, 배치 16, Ultralytics 기본 증강). 278회째 자동 종료, **178회째 최고**.
- 검증 점수: P 0.993, R 0.993, mAP50 0.991, mAP50-95 0.861 (그래프: `../data_set/`, 원본 기록·클래스별 값: `training_record/`)
- 인식률(앱 판정, 신뢰도 0.45, 학습에 안 쓴 사진 155장): **v4와 같은 153/155**, 깨진 폰 28장은 **이름까지 전부 정확**(8 ↔ 12 Pro Max 혼동 0). 틀린 2장은 우드락 전 사진의 16 화이트(15 Pro로 판정).
- 빠진 깨진 폰 230장(다른 기종)은 69%만 불량으로 잡음 → **8·12 Pro Max가 아닌 깨진 폰은 놓칠 수 있음.**
- 앱 연결 방법: `../HANDOFF_v5.md`
- `training_record/`: 학습 기록 원본 — `v5_results.csv`(278회 손실·지표·학습률), `v5_metrics_summary.json`(클래스별 P/R/mAP), YOLO가 그린 원본 그래프(`ultralytics_*.png`: results, 혼동 행렬, PR·F1 곡선)
