# 파손 재학습본 (10-19, 이 PC) — 적용 안 됨

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 640, 640] / 출력 [1, 9, 8400] · 만든 시각 2026-10-19 18:32 · SHA256 앞 16자 `7a174e701f799127`
- 클래스: 0 `target_iphone` · 1 `other_phone` · 2 `crack` · 3 `crack_phone` · 4 `broken_phone`
- 무엇: 5클래스: 3클래스 + `crack_phone` + `broken_phone`.
- 학습 데이터: 66장: 카카오톡 파손 데이터 59장 + 추가 7장(`broken_phone`). `broken_phone`은 검증 사진 0장. results.csv 기준 120 에포크.
- 어떻게 쓰였나: 당시 앱이 `broken_phone` 클래스를 몰라서 적용하지 않음.
- 결과·문제: 검증 mAP50-95 약 0.84(학습과 같은 출처라 실제 성능 근거 아님).
- 이 폴더 파일: `best.onnx`, `best.pt`, `training_result.json`, `runs/iphone_detector/weights/`
