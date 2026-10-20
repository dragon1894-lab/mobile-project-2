# 카카오톡으로 받은 파손 검출 모델 — 10-19~10-21 앱 기본 모델

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 448, 448] / 출력 [1, 8, 4116] · 만든 시각 2026-10-19 11:19 · SHA256 앞 16자 `ffb43c9b651d6d50`
- 클래스: 0 `target_iphone` · 1 `other_phone` · 2 `crack` · 3 `crack_phone`
- 무엇: 깨진 아이폰 검출용. 실제로 학습된 클래스는 3번 `crack_phone` 하나뿐입니다(0~2번은 사진 0장).
- 학습 데이터: 깨진 아이폰 사진 59장(학습 47 / 검증 12, 폰 전체 박스 1개). **정상 폰 사진 없음.** 학습 경로 `Desktop\broken_phone`. 같은 사진·라벨은 이 PC `imports/three-model-merge-20261019-145921/broken_phone`.
- 어떻게 쓰였나: 2026-10-19 11:32부터 v2로 바꾸기 전(10-21)까지 `04_Vision/models/best.onnx` 기본 모델. 앱은 `crack_phone`을 휴대폰 검출과 파손 판정에 함께 썼고, 파손은 표면 무늬 검사로 한 번 더 확인했습니다.
- 결과·문제: 정상 폰 사진도 전부 `crack_phone`으로 검출(시험 사진 100%, 파손 기준 신뢰도 10%). 이 모델 + 아래 alternatives 13개를 함께 돌린 앱은 컨베이어 정상 폰 8장을 **모두 불량**으로 판정, 1장 약 960ms.
- 이 폴더 파일: `best.onnx`, `best.pt`(같은 학습), `model_info.json`·`deployed_model.json`·`training_result.json`(적용 기록). `runs/`는 이 모델의 학습 기록이 아닙니다(→ `runs/iphone_detector/weights/MODEL_INFO.md`).
