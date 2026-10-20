# 연속 이동 모드 검증 · 2026-10-17

- 빌드: Verification 및 사용자 앱, 경고 0 / 오류 0.
- 기본 연속 모드: 실제 ONNX + 모의 카메라·벨트 + 로컬 테스트 서버, 16개 통과.
- 유지한 선택 정지 모드: 프로토콜·모델 선택·정지 후 검사·자동/수동 재구동·실패 처리·화면 배치, 51개 통과.
- 1100×740, 1400×900 화면 배치 검증. 1100×740 출력 이미지 육안 확인.
- 인식 시 STOP 미전송, 계속 표시·기록, 제품별 중복 방지, 다음 제품 재검출, 검사 중지와 벨트 정지 분리 확인.
- 하드웨어 펌웨어는 기존 V3 그대로이며 이번에는 보드 업로드·실제 모터 구동을 수행하지 않음.
- 실행 배포본: output/ProductionLine_Continuous/ProductionLine.UserApp.exe.
- 변경 전 백업: output/backups/before-continuous-mode-20261017-182527.zip.

재현:

    dotnet build 02_UserApp/Verification/Verification.csproj --artifacts-path tmp/continuous-mode-build -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true -p:CopyRetryCount=0 -m:1
    tmp/continuous-mode-build/bin/Verification/debug/Verification.exe --moving-inspection 04_Vision/samples/iphone/iphone_sample.jpg
    tmp/continuous-mode-build/bin/Verification/debug/Verification.exe --continuous-yolo 04_Vision/samples/iphone/iphone_sample.jpg
