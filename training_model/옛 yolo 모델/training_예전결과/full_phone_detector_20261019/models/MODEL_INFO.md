# `phone` 전체 외곽 모델 원본 (10-19)

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 640, 640] / 출력 [1, 5, 8400] · 만든 시각 2026-10-19 11:04 · SHA256 앞 16자 `03662fa055bd16b4`
- 클래스: 0 `phone`
- 무엇: → `alternatives/03_휴대폰전체검출_적용전백업`과 같은 모델(같은 ONNX).
- 학습 데이터: 컨베이어 사진 7장, CPU 80 에포크.
- 어떻게 쓰였나: 10-19 오전 잠깐 기본 모델.
- 결과·문제: → 03 메모
- 이 폴더 파일: `best.onnx`, `best.pt`, json 2개, `runs/full_phone/weights/`
