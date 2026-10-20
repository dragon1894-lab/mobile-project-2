# v1 — 첫 통합 모델 (2026-10-20, 학습 PC) · 사용 중지

- 파일: `best.onnx`(앱용), `best.pt`, `classes.txt`
- 클래스: 0 crack_iphone · 1 iphon16_white · 2 iphone_12_pro_silver · 3 iphone15pro_Natural_Titanium · 4 iphone_14_white
- ONNX: 입력 `images` [1, 3, 640, 640] / 출력 `output0` [1, 9, 8400]
- 학습: yolo26n, 640, 300 에포크(213회째 최고), 사진 878장(학습 PC의 기종별 데이터 5개를 번호만 바꿔 합침)
- 결과: 검증 100%(학습 사진과 거의 같은 사진이 검증에 섞여 부풀려짐) / 못 본 사진 **35.9%** / 컨베이어 정상 폰→불량 오판 **4/8** / 검은 화면→불량
- 원래 데이터: 커밋 `c2eed56`의 `all_phones/`
- **v2로 교체됨.** 비교 기록용으로만 보관.
