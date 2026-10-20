# 통합 모델 v1 (10-20) — `training_model/v1`과 같은 파일

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 640, 640] / 출력 [1, 9, 8400] · 만든 시각 2026-10-20 20:54 · SHA256 앞 16자 `ecc1d35b5347c952`
- 클래스: 0 `crack_iphone` · 1 `iphon16_white` · 2 `iphone_12_pro_silver` · 3 `iphone15pro_Natural_Titanium` · 4 `iphone_14_white`
- 무엇: 5클래스 통합: 0 `crack_iphone`, 1 `iphon16_white`, 2 `iphone_12_pro_silver`, 3 `iphone15pro_Natural_Titanium`, 4 `iphone_14_white`.
- 학습 데이터: 878장. 학습 PC(김하균)에서 학습, 이 PC가 GitHub에서 받아 둔 사본.
- 어떻게 쓰였나: 평가만 하고 앱에는 넣지 않음. v2로 교체.
- 결과·문제: → `training_model/v1/MODEL_INFO.md` (못 본 사진 35.9%, 컨베이어 정상→불량 4/8)
- 이 폴더 파일: `best.onnx`(SHA `ecc1d35b`, `v1.onnx`와 동일), `best.pt`, `training_result.json`, `runs/iphone_detector/weights/`
