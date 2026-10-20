# 최초 `phone` 위치 검출 데모 (10-17)

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 416, 416] / 출력 [1, 5, 3549] · 만든 시각 2026-10-17 11:10 · SHA256 앞 16자 `a81ccd89ab1b8f27`
- 클래스: 0 `phone`
- 무엇: 클래스 1개 `phone`, 입력 416. 휴대폰 위치만 찾음.
- 학습 데이터: Codex 데모 프로젝트(`Documents\Codex\2026-10-16\...\phone_yolo_demo_20261017`)에서 만든 모델.
- 어떻게 쓰였나: 초기 위치 검출용. CPU 추론 약 14ms(`model_info.json`).
- 결과·문제: 거의 모든 폰 사진에서 `phone`을 찾음(위치 전용이라 기종·파손 판정은 못 함).
- 이 폴더 파일: `best.onnx`, `model_info.json`
