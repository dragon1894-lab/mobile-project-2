# `phone` + 16 / 15 Pro 재학습 (10-19)

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 640, 640] / 출력 [1, 7, 8400] · 만든 시각 2026-10-19 11:04 · SHA256 앞 16자 `7fde10d3b31bad9e`
- 클래스: 0 `phone` · 1 `iphone_16` · 2 `iphone_15_pro`
- 무엇: `phone`, `iphone_16`, `iphone_15_pro` 3클래스, 폰 전체 박스.
- 학습 데이터: 컨베이어 사진 **7장**(학습 5 / 검증 2). `phone` 3장, 16 2장, 15 Pro 2장. CPU 100 에포크. `training_예전결과/full_phone_20261019`와 같은 파일.
- 어떻게 쓰였나: 03 모델을 만들기 위한 중간 단계. 앱 기본 모델로 쓴 적 없음.
- 결과·문제: 검증 precision 0.43. 교차 시험에서 컨베이어 2/8 외에는 거의 못 찾음.
- 이 폴더 파일: `best.onnx`, `model_info.json`
