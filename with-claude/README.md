# 생산라인 검사·AI 문제대응 시스템 (with claude 통합본)

5명이 나눠 만든 코드를 김하균(서버·통합)이 하나로 합친 **최신 통합본**입니다. 2026-10-19 기준.
C#/.NET 8, 관리자·작업자 PC와 서버는 유선 이더넷의 HTTP REST/JSON으로 통신합니다.

## 폴더와 담당자

| 폴더 | 담당 | 들어 있는 것 |
|---|---|---|
| `00_Shared` | 김하균 관리, 전원 합의 | 앱↔서버 공용 양식(`Contracts.cs`, `ChatContracts.cs`), 공용 호출 함수, 단체 채팅 화면(`ProductionLine.ChatUI`) |
| `01_AdminApp` | 조영준 | 관리자 앱: 라인 현황, 사용자 관리, 완료 요청 종결·반려, 검사 이력, 1:1 문의 답변, **단체 채팅 버튼** |
| `02_UserApp` | 장서진 | 작업자 앱: 실시간 검사(카메라), 검사 이력, 문제·조치, AI 상담, 관리자 상담, **06 단체 채팅** |
| `03_Server` | 김하균 | 서버: 로그인·권한, SQLite DB, 문제 처리 순서, 1:1 문의, 통신 기록, **단체 채팅, 외부 AI 연결** |
| `04_Vision` | 문성찬 | 영상처리 검사 (YOLO 연결 예정) |
| `05_AiAssistant` | 공도현 | 매뉴얼 답변 + 외부 AI(Gemini/OpenAI) 상담 |
| `md파일` | 김하균 | **서버·DB 정리와 할 일** (아래 참고) |
| `docs` | — | API 목록, 병합 규칙 |
| `scripts` | — | 빌드·실행 스크립트, Gemini 키 저장 |
| `tools` | — | 기획서 PDF를 만드는 파이썬 스크립트 (앱과 무관) |

## 김하균이 먼저 볼 문서 (`md파일`)

| 파일 | 내용 |
|---|---|
| `클로드 버전_서버_추가기능_정리_2026-10-19.md` | 서버에 추가한 기능 전체 (단체 채팅·AI 포함) |
| `DB_구조_정리_2026-10-19.md` | DB 표 8개와 파일 저장 위치 |
| `클로드 버전_앞으로_할일_2026-10-19.md` | 남은 할 일, 조원에게 전할 말 |
| `gpt버전_서버_업그레이드_15가지_쉽게정리.md` | 같은 서버를 GPT가 정리한 버전 (10-19 새벽 기준, 채팅·AI 전) |

## 실행

Visual Studio에서 `ProductionLineSystem.sln`을 열거나 PowerShell에서:

```powershell
.\scripts\build.ps1
.\scripts\run-server.ps1      # AI 키 없이 (AI 상담은 매뉴얼 답변)
.\scripts\run-admin.ps1
.\scripts\run-user.ps1
```

- 실제 AI를 쓰려면 서버 PC에서 `scripts\set-gemini-key.cmd`로 키를 한 번 저장한 뒤 `scripts\run-server-gemini.cmd`로 켭니다. 키는 GitHub에 올리지 않습니다.
- 시연용 계정: 관리자 `admin` / `admin123!`, 작업자 `worker1` / `worker123!` (저장소 공개 전에 바꿀 것)

## 다른 PC에서 연결

서버 PC에서 `ipconfig`로 IPv4를 확인하고, 앱을 켜기 **전에** 앱 PC에서 설정합니다.

```powershell
$env:PRODUCTIONLINE_SERVER_URL='http://<서버PC-IP>:5050'
```

서버는 `0.0.0.0:5050`에서 받습니다. 서버 PC 방화벽에서 TCP 5050 인바운드를 허용하세요.

## 데이터가 저장되는 곳 (GitHub에는 안 올라감)

| 위치 | 내용 |
|---|---|
| `03_Server/.../App_Data/productionline.db` | DB (사용자, 라인, 검사, 문제, 1:1 문의, 통신 기록) |
| `03_Server/.../data/group-chat.json` | 단체 채팅 |
| `03_Server/.../data/ai-events.jsonl` | AI 진단 코드 (상담 내용은 저장 안 함) |

## 개발 원칙

- 각자 자기 번호 폴더를 중심으로 수정합니다.
- `00_Shared` 양식을 바꾸기 전에는 전원이 합의합니다.
- 앱은 DB에 직접 접근하지 않고 서버 API만 씁니다.
- 검사 판정은 `04_Vision`, 상담은 `05_AiAssistant`가 맡습니다. AI는 판정·설비 제어·종결을 하지 않습니다.
