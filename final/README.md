# final — 생산라인 시스템 최종 통합본 (2026-10-21 밤 최신)

> **이 문서를 받은 Codex에게:** 목표는 **PC 2대에서 서버 연결이 되는지 확인**하는 것입니다.
> `ProductionLine_final_20261021.zip`을 받아 아래 순서대로 빌드·실행하고, "4. 확인할 것"의 결과를 김하균님에게 보고하세요.
> 코드를 고치기 전에 먼저 실행 결과를 보고하세요.

## 1. 이 압축에 들어 있는 것

컨베이어 PC(카메라·아두이노가 연결된 PC)의 2026-10-21 23:14 기준 최신 소스입니다. 같은 이름의 이전 압축(오후 버전)을 이 파일로 교체했습니다.

| 포함 | 내용 |
|---|---|
| 소스 | `00_Shared` ~ `06_TrainingTool`, `ProductionLineSystem.sln` |
| 서버 연결 창 | 관리자 앱: "서버 켜고 접속" / 작업자 앱: 같은 네트워크 서버 자동 찾기(UDP 5051) + IP 직접 입력 |
| 작업자 앱 | 모던 UI (메뉴 5개: 실시간 검사 / 검사 개수·지난 기록 / 문제·조치 / AI 상담 / 단체 채팅), V7 인식 후 가속, COM 선택·포트 갱신·벨트 연결·벨트 구동 버튼 |
| 관리자 채팅 | 별도 1:1 메뉴 없음. **문제·조치 → 관리자 채팅**에서만, 문제가 **조치 중**일 때만 전송(서버도 거부) |
| AI 상담 | 문제를 고르지 않아도 바로 질문(자유 상담, 기록 생성 안 함) |
| 관리자 앱 | 대표 라인 1은 아두이노가 보고한 실제 벨트·가속·긴급정지·서보 각도 표시, 완료 요청 창 상태 필터·채팅, 전체 정지(확인 창 → 작업자 정지 응답 확인), 원격으로 이동·55° 유지 시간 변경 |
| YOLO 모델 | `04_Vision/models/best.onnx` = 저장소 `training_model/v4_merged_default` (SHA256 앞자리 `55AC3EFD6FC19D86`) |
| 펌웨어 | `hardware/ConveyorYoloControl` (V7), 업로드 도구 `tools/arduino-cli` |

**빠진 것 (의도적):** 서버 DB·채팅·검사 사진(`App_Data`, `data`), 빌드 결과물(`bin`, `obj`), 학습 사진. 서버를 처음 켜면 빈 DB가 새로 만들어지고 기본 계정이 생깁니다.

- 관리자: `admin` / `admin123!`
- 작업자: `worker1` / `worker123!`

## 2. 준비 (두 PC 모두)

1. **짧은 경로에 압축을 푸세요.** 예: `C:\PL\ProductionLine_final`
   - 경로가 길면 Windows 260자 제한 때문에 서버가 `Unable to load DLL 'e_sqlite3' ... (0x800700CE)` 오류로 바로 꺼집니다. 컨베이어 PC에서 실제로 겪었고, 짧은 경로에서는 정상 동작했습니다.
2. .NET 8 SDK 이상이 필요합니다(`dotnet --list-sdks`).
3. 빌드:
   ```powershell
   cd C:\PL\ProductionLine_final
   dotnet build .\ProductionLineSystem.sln
   ```
   컨베이어 PC에서 새 폴더에 풀어 빌드했을 때 경고 0, 오류 0.

## 3. 실행 순서

### PC A — 서버 PC
아래 둘 중 하나로 서버를 켭니다. **서버는 한 대만** 켜세요(두 대가 각각 켜면 데이터가 따로 저장됩니다).

- 관리자 앱 실행 → 연결 창에서 **[서버 켜고 접속]** (서버를 직접 켜고 이 PC 주소를 크게 보여 줌)
  ```powershell
  .\scripts\run-admin.ps1
  ```
- 또는 서버만 실행:
  ```powershell
  .\scripts\run-server.ps1
  ```
  서버 창에 `다른 PC 접속 주소: http://<IPv4>:5050`이 표시됩니다.

**방화벽:** 처음 켤 때 Windows 방화벽 창이 뜨면 **허용**(개인 네트워크). 창이 안 떴는데 다른 PC에서 못 찾으면 관리자 PowerShell에서:
```powershell
New-NetFirewallRule -DisplayName "ProductionLine TCP 5050" -Direction Inbound -Protocol TCP -LocalPort 5050 -Action Allow
New-NetFirewallRule -DisplayName "ProductionLine UDP 5051" -Direction Inbound -Protocol UDP -LocalPort 5051 -Action Allow
```

### PC B — 작업자(또는 두 번째) PC
```powershell
.\컨베이어_통합실행.cmd
```
(작업자 앱을 빌드 후 실행. 또는 `.\scripts\run-user.ps1`)

1. **서버 연결 · 작업자** 창이 뜨면 목록에 PC A가 나타납니다(같은 공유기/네트워크여야 함). 고르고 [접속].
2. 목록에 없으면 `192.168.x.x` 형식으로 PC A의 IP를 직접 입력.
3. `worker1` / `worker123!` 로그인. 화면 아래에 `연결됨 · 서버 <IP>:5050`이 보이면 PC A 서버에 붙은 것입니다.

## 4. 확인할 것 (결과를 보고)

| # | 확인 | 기대 결과 |
|---|---|---|
| 1 | PC A 서버 창의 접속 주소 | `http://<공유기 IPv4>:5050` (VPN 주소 100.x가 아니어야 함) |
| 2 | PC B 연결 창 자동 찾기 | PC A가 목록에 한 줄로 나타남 |
| 3 | PC B 직접 입력 접속 | PC A IP 입력으로 접속 성공 |
| 4 | 같은 데이터 | PC B 작업자 앱에서 **단체 채팅**에 글 작성 → PC A 관리자 앱 단체 채팅에 보임 (반대도) |
| 5 | 같은 데이터 | PC B에서 문제 등록 → PC A 관리자 앱 목록에 보임 |
| 6 | 끊김 처리 | PC A 서버를 끄면 PC B에 연결 실패 안내, 다시 켜면 재접속 |

자동 찾기가 안 되고 IP 직접 입력만 되면: 방화벽 UDP 5051 또는 두 PC가 다른 네트워크(공유기·서브넷)일 가능성이 큽니다.

## 5. 컨베이어 PC에서 이미 확인한 것 / 안 한 것

- 확인함(한 PC 안, 이 압축을 새 짧은 경로에 풀어서): 빌드 경고 0·오류 0, 서버 실행 → 새 DB 생성, 관리자 로그인, 라인 4개 조회, 문제 없이 AI 상담 응답, 서버 모델 v4 해시 일치. (UDP 찾기 응답은 이전 압축에서 확인)
- 자동 테스트(별도 테스트 서버, 실제 COM 포트 미사용): 원격·채팅·AI·사진·전체 정지 통합 143개 통과(`tools/RemoteVerification`, 인수: 서버 dll 경로, 결과 폴더), 가상 라인 34·가상 검사 사진 67·시간/분류 66, 이력 스크롤 24·문제 목록 16, 연속 검사 49·컨베이어 25·하드웨어 11 통과.
- 남은 실패(옛 기준 테스트, 앱 문제 아님): `ScrollVerification --modern-ui`는 "COM·벨트 버튼이 없어야 함"을 검사해 실패(버튼을 의도적으로 다시 넣음 → 테스트 갱신 필요, 그 뒤 화면 검사는 미확인), 연속 검사의 1400×900 배치 검사.
- **안 함:** 실제 두 PC 연결, 실물 카메라·벨트, 아두이노 V7 업로드.
