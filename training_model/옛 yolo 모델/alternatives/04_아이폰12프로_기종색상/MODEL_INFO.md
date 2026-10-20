# 아이폰 12 Pro 색상 3종 모델 (10-19)

> 사용 중지된 옛 모델입니다. 지금 앱은 `training_model/v2`(= `all_phones/models/best.onnx`)만 씁니다.

- `best.onnx`: 입력 [1, 3, 640, 640] / 출력 [1, 10, 8400] · 만든 시각 2026-10-19 11:45 · SHA256 앞 16자 `c84fd688ee00a1fe`
- 클래스: 0 `target_iphone` · 1 `other_phone` · 2 `crack` · 3 `iphone_12pro_graphite` · 4 `iphone_12pro_pacificblue` · 5 `iphone_12pro_silver`
- 무엇: 3클래스 + `iphone_12pro_graphite`, `iphone_12pro_pacificblue`, `iphone_12pro_silver`.
- 학습 데이터: 다른 조원 PC(`ankh7`)의 `바탕 화면\yolo` 데이터. 받은 파일 이름 `best (1).onnx`. **원본 사진·라벨은 받지 못함**(그래파이트·퍼시픽블루 사진 수 확인 불가).
- 어떻게 쓰였나: 기종·색상 후보로만 사용(품질 판정 끔).
- 결과·문제: 교차 시험(2026-10-20, 신뢰도 0.45): 이 모델을 다른 기종 사진에 돌렸을 때의 결과입니다. 모델마다 3번 클래스 하나만 학습해서 ‘이 기종이 아니다’를 배운 적이 없습니다. 12 Pro 실버 21장 → 실버 10, 그래파이트 7, 퍼시픽블루 5(섞임). 12 Pro Max 파손 12/12 → 퍼시픽블루. 14 화이트 5/10 → 그래파이트.
- 이 폴더 파일: `best.onnx`, `model_info.json`
