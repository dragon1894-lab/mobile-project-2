# 6클래스 기종 검출 시도 (10-17) — 카메라 영역 라벨

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 640, 640] / 출력 [1, 10, 8400] · 만든 시각 2026-10-17 11:25 · SHA256 앞 16자 `a38e3f740e001725`
- 클래스: 0 `target_iphone` · 1 `other_phone` · 2 `crack` · 3 `iphone_16` · 4 `iphone_15` · 5 `iphone_15_pro`
- 무엇: 3클래스 + `iphone_16`, `iphone_15`, `iphone_15_pro`. 박스는 폰 전체가 아니라 **뒷면 카메라 영역**에 그려짐.
- 학습 데이터: 사진 **4장**(학습 3 / 검증 1). 라벨이 있는 클래스는 `iphone_16` 2개, `iphone_15_pro` 2개뿐. 학습 경로 `Desktop\새 폴더 (3)`.
- 어떻게 쓰였나: 품질 판정 끔. 기종 후보로만 사용.
- 결과·문제: 교차 시험(2026-10-20, 신뢰도 0.45): 이 모델을 다른 기종 사진에 돌렸을 때의 결과입니다. 모델마다 3번 클래스 하나만 학습해서 ‘이 기종이 아니다’를 배운 적이 없습니다. 12 Pro 실버 사진 21장 중 18장, 14 화이트 10/10, 12 Pro Max 12/12를 `iphone_15_pro`로 판정. 기종 구분 불가.
- 이 폴더 파일: `best.onnx`, `model_info.json` (PyTorch 가중치는 `runs/iphone_detector/weights`)
