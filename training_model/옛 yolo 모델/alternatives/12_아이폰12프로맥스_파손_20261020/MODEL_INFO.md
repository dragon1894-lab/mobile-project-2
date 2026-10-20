# 아이폰 12 Pro Max 파손 모델 (10-19 학습)

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 640, 640] / 출력 [1, 8, 8400] · 만든 시각 2026-10-19 18:23 · SHA256 앞 16자 `548ed8c6c60e9f16`
- 클래스: 0 `target_iphone` · 1 `other_phone` · 2 `crack` · 3 `iphone_12_pro_max_Crack`
- 무엇: 3번 `iphone_12_pro_max_Crack` 하나(기종 + 파손을 한 이름으로).
- 학습 데이터: 깨진 12 Pro Max 60장(학습 48 / 검증 12), 폰 전체 박스 1개. **정상 폰 사진 없음.** `iphone_12_pro_max_Crack.zip`.
- 어떻게 쓰였나: 앱이 이 클래스를 `crack_phone` + `iphone_12_pro_max` 두 이름으로 나눠, 검출되면 표면 검사 없이 바로 불량 처리.
- 결과·문제: **컨베이어 정상 폰 8장 중 7장을 파손으로 검출** — 앱이 정상 폰을 불량으로 판정한 주된 원인. 파손 사진은 잘 잡음.
- 이 폴더 파일: `best.onnx`, `model_info.json`
