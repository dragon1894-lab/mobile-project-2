# 아이폰 영상처리

2026-10-19 사용자 제공 `best.onnx`를 기본 모델로 적용했습니다. `target_iphone`, `other_phone`, `crack`, `crack_phone` 네 클래스를 사용하며, 적용 전 전체 외곽 모델도 함께 보관합니다.

| 모델 | 위치 (`04_Vision/models` 기준) | 입력 / 출력 | 용도 |
|---|---|---|---|
| 기본 사용자 제공 | `best.onnx` | 448×448 / `[1,8,4116]` | 대상·기타 휴대폰 및 파손 검출 |
| 적용 전 전체 외곽 | `alternatives/03_휴대폰전체검출_적용전백업/best.onnx` | 640×640 / `[1,5,8400]` | 휴대폰 본체 전체 위치·크기(px) |
| 이전 6클래스 | `alternatives/02_기종검출_카메라영역/best.onnx` | 640×640 / `[1,10,8400]` | 이전 기종·카메라 영역 모델 |
| 추가 3클래스 | `alternatives/01_기존_3클래스/best.onnx` | 640×640 / `[1,7,8400]` | `target_iphone` 등 기존 클래스 검출 |
| 이전 위치 검출 | `alternatives/05_이전_휴대폰위치검출/best.onnx` | 416×416 / `[1,5,3549]` | `phone` 위치 검출 |
| 아이폰 14 화이트 | `alternatives/07_아이폰14_화이트/best.onnx` | 640×640 / `[1,8,8400]` | `iphone_14_white` 기종·색상 검출 |
| 아이폰 16 화이트 | `alternatives/08_아이폰16_화이트/best.onnx` | 448×448 / `[1,8,4116]` | `iphone16_white` 기종·색상 검출 |

현재 기본 클래스는 ID 순서대로 `target_iphone`, `other_phone`, `crack`, `crack_phone`입니다. `crack_phone`은 휴대폰 진입과 파손 판정에 모두 사용합니다. 이전 6클래스 모델은 `target_iphone`, `other_phone`, `crack`, `iphone_16`, `iphone_15`, `iphone_15_pro` 순서입니다. 이름과 번호는 선택한 ONNX 메타데이터에서 읽습니다.

## 검출과 품질 판정

선택한 모델의 클래스·신뢰도·영상상 크기(px)를 표시합니다. 벨트는 검출돼도 계속 이동합니다. 화면 가장자리에 걸린 검출 박스는 전체 진입을 기다린 뒤 기록합니다. 검사 간격은 0.1초를 목표로 하며 실제 속도는 PC와 추론 시간에 따라 달라집니다. 기본 신뢰도는 45%, NMS IoU는 0.45입니다.

현재 기본 모델은 사용자 제공 파일이며 학습 원본과 독립 시험 자료는 프로젝트에 포함되지 않았습니다. 보유한 동일 촬영환경 사진 7장에서는 `crack_phone` 전체 박스가 검출됐지만 실제 현장 정확도를 보장하지 않습니다. 여섯 아이폰 기종과 네 색상, 실제 cm 치수는 이 모델에 포함되지 않습니다. 색상 4종은 [별도 학습 프로젝트](../training/phone_colors/README.md)에 실제 자료가 준비된 뒤 학습합니다.

모델 폴더의 `model_info.json`에 `classificationAvailable: true`와 `targetIphoneIsNormal: true`를 저장했습니다. `target_iphone`만 정상으로 기록하며 `other_phone`, `crack`, `crack_phone`은 불량으로 기록합니다. 이 파일도 모델과 함께 실행 폴더로 복사됩니다.

## 실행

1. `ProductionLineSystem.sln`을 다시 빌드하고 서버 → 사용자 앱 순서로 실행합니다.
2. 사용자 화면에서 카메라 연결 → YOLO 모델 확인 → 검사 시작을 누릅니다.
3. 모델을 바꾸려면 검사 중지 → YOLO 모델 선택을 누릅니다. 실행 폴더의 `Models/alternatives`에 다른 모델이 있습니다. 선택은 현재 실행 중에 적용되며 앱을 다시 열면 기본 모델을 사용합니다.
4. 벨트 연결은 [아두이노 연결 안내](../hardware/README.md)를 따릅니다.

Visual Studio에서 `ProductionLine.Vision.Tester`를 시작 프로젝트로 실행하면 기본 샘플 결과를 `04_Vision/output/iphone_result.jpg`에 저장합니다. 사진과 모델을 인수로 지정할 수도 있습니다.

```text
ProductionLine.Vision.Tester.exe "사진.jpg" "best.onnx" 0.5
```

현재 학습 결과와 백업은 [전체 외곽 재학습 안내](../docs/PHONE_FULL_BODY_TRAINING_20261019.md), 이전 패키지 원본과 적용 내역은 [과거 통합 기록](../docs/PHONE_PACKAGE_MERGE.md)을 참고하세요.
