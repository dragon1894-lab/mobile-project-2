# 인식 시 결과 표시 검증 · 2026-10-17

변경:
- 연속 분석·실시간 영상 표시와 제품 결과 표시를 분리.
- 신뢰도 기준을 통과한 새 휴대폰을 인식할 때만 결과 사진·제품 번호·클래스·신뢰도·시각 갱신.
- 같은 제품 또는 빈 화면은 마지막 인식 결과를 덮어쓰지 않음.
- 서버 조회·검사 중지 후에도 마지막 사진과 결과 유지.
- 카메라 새 연결·모델 변경·라인 변경 시 인식 결과 초기화. 화면 인식 번호는 해당 화면의 연결·모델 세션 내 순번이며 서버 검사 ID와 별개.
- 인식 시 벨트 정지 기본 해제 유지. 펌웨어 변경 없음.

검증:
- 사용자 앱 및 Verification 빌드: 경고 0, 오류 0.
- --moving-inspection: 실제 ONNX + 합성 카메라 입력 + 모의 벨트 + 로컬 HTTP 서버, 25개 통과.
- --continuous-yolo: 기존 선택 정지·재촬영·재구동·실패 처리, 51개 통과.
- 1100×740 및 1400×900 배치 검사. 1100×740 화면 출력 이미지 육안 확인.
- 배포본 DLL·모델·펌웨어 해시 일치 확인.
- 실제 COM 포트·모터 구동 및 보드 업로드 없음.

배포본: output/ProductionLine_RecognitionResult/ProductionLine.UserApp.exe
빌드 결과: tmp/recognition-result-build/bin/Verification/debug
백업: output/backups/before-recognition-result-20261017-183630.zip
