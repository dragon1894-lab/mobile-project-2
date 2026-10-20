# backups — 컨베이어 PC 코드 백업과 옛 작업 기록

컨베이어 PC 프로젝트 폴더의 `output/backups`(코드를 크게 바꾸기 **직전 상태**를 저장한 것)와 `docs`의 옛 작업 기록을 2026-10-21에 옮겨 보관한 폴더입니다.
컨베이어 PC 로컬 Git에는 커밋 기록이 없어서 **예전 코드로 돌아갈 근거는 이 백업뿐**입니다. 지금 코드는 저장소 루트가 아니라 컨베이어 PC 폴더에 있으며, 여기 파일을 그대로 덮어쓰면 이후 수정이 사라집니다.

- 이름 규칙: `before-<작업>-<날짜>-<시각>` = 그 작업을 하기 **직전** 파일. 압축(zip)은 그 작업에서 바뀐 폴더·파일만 담았습니다(전체 프로젝트 아님).
- “관련 기록”이 있으면 그 문서에 변경 내용과 검증 결과가 있습니다. 없으면 이름과 내용으로 판단한 설명입니다.
- 들어 있는 서버 설정(`appsettings*.json`)은 기본값뿐이며 키·DB는 없습니다. 옛 소스에 시연용 계정(`admin/admin123!`, `worker1/worker123!`)이 적혀 있는 것은 현재 코드와 같습니다.

## 코드백업(output_backups) — 시간 순

| 날짜·시각 | 백업 | 담긴 것 | 무엇을 하기 직전 | 관련 기록 |
|---|---|---|---|---|
| 10-17 10:16 | `before-admin-ai-integration-20261017-101641.zip` | 00~05 소스 36개 | 관리자 앱·AI 상담 수정본 통합 | `docs/ADMIN_AI_INTEGRATION.md` |
| 10-17 11:20 | `model-before-second-training-20261017-112048/` | `best.onnx`·`best.pt` | 두 번째 학습 모델로 바꾸기 전 모델: 3클래스(target/other/crack), 입력 736, 20 에포크(10-16 학습) | — |
| 10-17 11:31 | `previous-three-class-model-20261017-113137/` | `best.pt`·json | 공용 휴대폰 모델로 바꾸기 전 3클래스 모델(10-17 11:20 적용분, 100 에포크) | — |
| 10-17 11:31 | `before-shared-phone-model-20261017-113137.zip` | 00~04 소스·모델 58개 | 조원 공유용 휴대폰 YOLO 모델 적용 | `옛 작업기록(docs)/PHONE_SHARED_MODEL_UPDATE.md` |
| 10-17 12:12 | `before-phone-packages-merge-20261017-121212.zip` | 04_Vision·models·dataset·사진·라벨·TrainingTool 125개 | 조원 휴대폰 모델 패키지 합치기 | `docs/PHONE_PACKAGE_MERGE.md` |
| 10-17 14:35 | `before-pul-dir-control-20261017-143549.zip` | 02_UserApp·hardware·scripts 29개 | 아두이노 PUL/DIR 컨베이어 제어 통합 | `docs/PUL_DIR_CONTROL_INTEGRATION.md` |
| 10-17 15:21 | `before-auto-resume-20261017-152143.zip` | 02_UserApp·hardware·docs 37개 | 자동 검사 재개 흐름 변경 | `docs/AUTO_INSPECTION_FLOW.md` |
| 10-17 17:52 | `before-working-pin-fix-20261017-175220.zip` | 펌웨어 `.ino`·컨베이어 통신 코드 6개 | 아두이노 핀 수정 | — |
| 10-17 18:25 | `before-continuous-mode-20261017-182527.zip` | 02_UserApp·hardware·docs 38개 | 벨트 이동 중 연속 검출 모드 | `옛 작업기록(docs)/CONTINUOUS_MODE_VERIFICATION.md` |
| 10-17 18:36 | `before-recognition-result-20261017-183630.zip` | 검사 화면 코드 5개 | 인식 결과 표시 방식 변경 | `옛 작업기록(docs)/RECOGNITION_RESULT_VERIFICATION.md` |
| 10-19 10:04 | `before-history-photos-20261019-100414.zip` | 00·02·03 소스 44개 | 검사 이력별 인식 사진 저장·조회 | `docs/HISTORY_PHOTOS_VERIFICATION.md` |
| 10-19 10:15 | `before-user-ui-merge-20261019-101543.zip` | 00~03 소스 62개 | 사용자 앱 UI 수정본(장서진) 합치기 | `docs/USER_UI_MERGE_20261019.md` |
| 10-19 11:05 | `before-full-phone-20261019-110520.zip` | 모델·라벨·데이터셋 33개 | 휴대폰 전체 외곽 `phone` 모델 적용 | `docs/PHONE_FULL_BODY_TRAINING_20261019.md` |
| 10-19 12:04 | `before-admin-status-merge-20261019-120414.zip` | 00·01·tools 25개 | 관리자 앱 서버 상태 표시 합치기 | `옛 작업기록(docs)/ADMIN_STATUS_ALL_MODELS_20261019.md` |
| 10-19 12:08 | `before-all-models-20261019-120824.zip` | 04_Vision 모델·소스, 02_UserApp 65개 | “전체 학습 사용”(모델 여러 개 동시 실행) 적용 | `옛 작업기록(docs)/ADMIN_STATUS_ALL_MODELS_20261019.md` |
| 10-19 14:59 | `before-three-model-merge-20261019-145921.zip` | `models` 폴더 40개 | 파손·14 화이트·16 화이트 모델 3개 추가 | — |
| 10-19 18:22 | `before-leader-merge-20261019-182244.zip` 와 같은 이름 폴더 | 00~05 전체 소스·sln 93개 (zip과 폴더는 같은 내용) | 조장 전달 전체 코드 병합 | `옛 작업기록(docs)/LEADER_CODE_MERGE_20261019.md` |
| 10-20 | `before-models-20261020/` | `CombinedYoloModels.cs`·`CombinedModelChecks.cs`·`ProductionLine.Vision.dll` | 10-20 추가 모델 4개(09~12) 연결 | `옛 작업기록(docs)/MODEL_IMPORT_20261020.md` |
| 10-20 | `before-led-d7-20261020/` | 펌웨어 `.ino`·컨베이어 코드 4개 | 조명 LED(D7) 제어 추가 | — |
| 10-20 | `before-hardware-expansion-20261020/` | `runtime/`(관리자·서버·작업자 실행 dll) + `source-backup.zip`(펌웨어·Shared·컨베이어 코드 36개) | 센서·분류·긴급정지 하드웨어 확장 | — |

“관련 기록”의 `docs/…`는 컨베이어 PC 프로젝트 폴더의 `docs`, `옛 작업기록(docs)/…`는 이 폴더 안입니다.

## 옛 작업기록(docs) — 다른 문서가 링크하지 않는 옛 작업 일지 8개

| 파일 | 내용 |
|---|---|
| `ADMIN_STATUS_ALL_MODELS_20261019.md` | 관리자 앱 서버 상태 표시 + “전체 학습 사용” 모델 통합 기록(10-19) |
| `ALL_MODELS_20261019.json` | 위 문서의 모델 목록·SHA256 |
| `CONTINUOUS_MODE_VERIFICATION.md` | 벨트 연속 이동 모드 검증(10-17) |
| `IPHONE_DATASET_AUDIT_20261019.md` | 아이폰 기종 데이터 감사 — 당시 모델로는 6기종 판별 불가라는 결론(10-19) |
| `LEADER_CODE_MERGE_20261019.md` | 조장 전달 코드 병합 기록(10-19) |
| `MODEL_IMPORT_20261020.md` | 모델 4개 추가로 “전체 학습 13종”이 된 기록(10-20). **실행 폴더 경로는 지금 없음** |
| `PHONE_SHARED_MODEL_UPDATE.md` | 공유용 `phone` 모델 적용 기록(10-17) |
| `RECOGNITION_RESULT_VERIFICATION.md` | 인식 결과 표시 검증(10-17) |

이 문서들에 적힌 `output/…`, `tmp/…` 실행 경로는 2026-10-21에 정리되어 컨베이어 PC에 없습니다. 지금 실행은 프로젝트 폴더의 `컨베이어_통합실행.cmd`입니다.

작성: 2026-10-21, Claude(컨베이어 PC).
