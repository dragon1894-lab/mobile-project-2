# 02_기종검출_카메라영역 모델의 PyTorch 가중치

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- 무엇: `alternatives/02_기종검출_카메라영역/best.onnx`를 만든 학습(사진 4장, 6클래스)의 `best.pt`/`last.pt`.
- 학습 데이터: `Desktop\새 폴더 (3)` 데이터(→ 02 메모). 100 에포크, 마지막 mAP50 0.995 / mAP50-95 0.6965(검증 1장이라 의미 없음).
- 어떻게 쓰였나: 옛 최상위 `models/runs`에 있던 파일. `best.pt` SHA가 이 PC `imports/phone-merge-20261017-121058/labeled-project/models/best.pt`와 같아서 02 모델의 학습본으로 확인.
- 결과·문제: → `alternatives/02_기종검출_카메라영역/MODEL_INFO.md`
- 이 폴더 파일: `best.pt`, `last.pt`
