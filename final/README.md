# final — 생산라인 시스템 최종 통합본 (2026-10-21)

> **이 문서를 받은 Codex에게:** 목표는 **PC 2대에서 서버 연결이 되는지 확인**하는 것입니다.
> `ProductionLine_final_20261021.zip`을 받아 아래 순서대로 빌드·실행하고, "4. 확인할 것"의 결과를 김하균님에게 보고하세요.
> 코드를 고치기 전에 먼저 실행 결과를 보고하세요.

## 1. 이 압축에 들어 있는 것

컨베이어 PC(카메라·아두이노가 연결된 PC)에서 2026-10-21에 합친 최종 소스입니다.

| 포함 | 내용 |
|---|---|
| 소스 | `00_Shared` ~ `06_TrainingTool`, `ProductionLineSystem.sln` |
| 서버 연결 창 | 관리자 앱: "서버 켜고 접속" / 작업자 앱: 같은 네트워크 서버 자동 찾기(UDP 5051) + IP 직접 입력 |
| 작업자 앱 | 모던 UI v6.3 (메뉴 5개, 스크롤·겹침 수정) + V7 인식 후 가속 |
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

- 확인함(한 PC 안): 새 폴더 빌드 경고 0·오류 0, 서버 실행 → 새 DB 생성, UDP 찾기 응답 `PRODUCTIONLINE_SERVER_V1|5050|<PC이름>|<IPv4>`, 관리자 로그인, 라인 4개 조회, 서버 모델 v4 해시 일치. 작업자 앱 테스트: 스크롤 24·문제 목록 16·모던 UI 72·연속 검사 49·컨베이어 25·하드웨어 11 통과.
- 남은 실패 2개(옛 화면 기준 테스트): 1400×900 배치 검사, 조명 버튼 검사(v6.3에서 버튼을 의도적으로 삭제).
- **안 함:** 실제 두 PC 연결, 실물 카메라·벨트, 아두이노 V7 업로드.
