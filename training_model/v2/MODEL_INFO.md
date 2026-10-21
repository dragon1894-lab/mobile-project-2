# v2 — 통합 모델 (v3로 교체됨, 비교용) (2026-10-21, 학습 PC)

- 파일: `v2.onnx`(앱용), `v2.pt`, `classes.txt` — v3 이전에 `all_phones/models/`에 있던 파일
- 클래스: v1과 같음 (0 crack_iphone · 1 iphon16_white · 2 iphone_12_pro_silver · 3 iphone15pro_Natural_Titanium · 4 iphone_14_white)
- ONNX: 입력 `images` [1, 3, 640, 640] / 출력 `output0` [1, 9, 8400]
- 학습: yolo26n, 640, 179회째 자동 종료(79회째 최고), 사진 1,163장(v1 + 컨베이어 PC 옛 데이터, 중복 61장 제거, 거의 같은 사진은 묶음째로 분할)
- 결과: 못 본 사진 78장 **100%** / 컨베이어 정상 폰→불량 오판 **0/8**, 기종 4/6 / 검은 화면→검출없음
- 남은 문제: 컨베이어 위 16 화이트를 15 Pro로 착각(2/2)
- 데이터·자세한 인수인계: `all_phones/README.md`
