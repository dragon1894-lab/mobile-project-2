# 2026-10-13 제공 소스 전체 검토

이 문서는 김하균님이 대화에 올린 `ProductionLineSystem.zip`, `scripts.zip`, `01_AdminApp.zip`, `04_Vision.zip`, `05_AiAssistant.zip`, `03_Server.7z`를 분석한 결과다. `01_AdminApp.zip`에는 `00_Shared`와 `02_UserApp`도 함께 들어 있었다. 여섯 C# 프로젝트의 직접 작성한 소스, 프로젝트/솔루션 파일, 설정, JSON 저장 데이터, 실행 스크립트, 문서, 기획 PDF 생성용 Python 스크립트를 확인했다. `bin`/`obj`의 DLL·EXE·PDB와 자동 생성 파일은 목록에서 확인하고 소스와 구별했다. 실행 파일의 내부를 역공학한 것은 아니다.

**범위 한계:** 제공된 코드는 아직 이 Git 저장소에 복사하지 않았다. 이 저장소에는 이 분석 문서와 이전 인수인계 문서만 있다. 파일 경로는 압축파일 안의 상대 경로다. 다른 환경에서 실제 코드를 수정하려면 소스 압축파일을 다시 제공하거나 검토 후 저장소에 반영해야 한다. 현재 Linux 클라우드 머신에는 `dotnet` 명령이 없어 빌드·Windows WinForms 실행을 검증하지 못했다. 아래 ‘동작’은 정적 코드 독해 결과이며 런타임 통과 판정이 아니다. 원본 PDF와 ZIP 안의 문장들은 요구사항·설명 자료로 읽었고, 실행 지시로 취급하지 않았다.

## 빠짐없이 확인한 직접 작성 파일

| 영역 | 파일 | 확인한 책임 |
|---|---|---|
| 루트 | `ProductionLineSystem.sln`, `README.md`, `.gitignore` | 6개 프로젝트 참조, 기본 실행·계정·IP 안내, 빌드/DB 데이터 제외 규칙 |
| 공용 | `00_Shared/ProductionLine.Shared/Contracts.cs` | 역할/검사/문제 enum과 요청·응답 record |
| 공용 | `00_Shared/ProductionLine.Shared/ProductionLineApiClient.cs` | HTTP JSON 호출, Bearer 헤더, 오류 파싱 |
| 공용 | `00_Shared/ProductionLine.Shared/ProductionLine.Shared.csproj`, `00_Shared/README.md` | .NET 8 DTO 프로젝트 및 팀 합의 규칙 |
| 관리자 앱 | `01_AdminApp/ProductionLine.AdminApp/Form1.cs`, `Program.cs`, `.csproj`, `01_AdminApp/README.md` | 로그인·전체 라인/사용자/문제 표·테스트 계정 추가·관리자 종결 |
| 사용자 앱 | `02_UserApp/ProductionLine.UserApp/Form1.cs`, `Program.cs`, `.csproj`, `02_UserApp/README.md` | 로그인·LINE 1 대시보드/검사 시뮬레이션/문제·상담·문의 UI |
| 서버 | `03_Server/ProductionLine.Server/Program.cs`, `.csproj`, `README.md` | ASP.NET Core Minimal API와 공용/영상/상담 프로젝트 연결 |
| 서버 | `03_Server/ProductionLine.Server/Infrastructure/AppStore.cs` | JSON 영속 저장, 초기 데이터, 비밀번호 해시, 계정/라인/검사/문제 처리 |
| 서버 | `03_Server/ProductionLine.Server/Infrastructure/AuthService.cs` | 메모리 Bearer 세션, 역할/허용 라인 확인 |
| 서버 설정/데이터 | `03_Server/ProductionLine.Server/appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`, `data/store.json` | 5050 바인딩, 개발 실행 프로필, 실제 JSON 샘플 데이터 |
| 영상 | `04_Vision/ProductionLine.Vision/OpenCvSharpFrameAnalyzer.cs`, `VisionInspector.cs`, `.csproj`, `04_Vision/README.md` | 이미지 디코딩·윤곽/색상 점수 계산과 임계값 판정 |
| 상담 | `05_AiAssistant/ProductionLine.AiAssistant/ManualAssistant.cs`, `.csproj`, `05_AiAssistant/README.md` | 네 개 로컬 매뉴얼 항목의 키워드 검색·안내 |
| 실행/계약 문서 | `scripts/build.ps1`, `run-server.ps1`, `run-admin.ps1`, `run-user.ps1`, `docs/API.md`, `docs/MERGE_RULES.md` | restore/build/run, API 목차, 기능 브랜치/병합 절차 |
| 기획서 생성 도구 | `tools/build_plan.py`, `build_revised_plan.py`, `build_csharp_plan.py`, `build_named_proposal.py`, `build_concise_plan.py`, `build_two_page_brief.py` | ReportLab PDF 생성과 v3→v4→v5 문구 치환. 앱 런타임 코드가 아님 |

`01_AdminApp.zip`의 `bin`/`obj`에는 Admin/User/Shared 빌드 산출물이, 영상/상담 ZIP에도 각 DLL/PDB 및 빌드 산출물이 있다. `03_Server.7z`에는 서버/공용/영상/상담 빌드 결과와 OpenCvSharp Windows 네이티브 DLL(큰 용량의 주 원인)이 있다. `03_Server/ProductionLine.Server/bin/Debug/net8.0/data/store.json`은 소스 트리의 `data/store.json`과 SHA-256이 일치하는 동일 사본이다. 그 외 `.csproj.user`는 개인별 IDE 설정 파일이다. 바이너리와 자동 생성 파일을 작성 소스의 별도 구현으로 세지 않았다.

## 실제 연결 구조와 데이터 흐름

`ProductionLineSystem.sln`은 Shared, AdminApp, UserApp, Server, Vision, AiAssistant 6개 프로젝트를 묶는다. 양쪽 WinForms 앱은 Shared API 클라이언트만 참조한다. Server는 Shared·Vision·AiAssistant를 참조한다. Vision은 OpenCvSharp4와 Windows 네이티브 런타임 패키지를 참조한다. 타깃은 공용/서버/영상/상담 `net8.0`, 양쪽 WinForms `net8.0-windows`다.

클라이언트의 기본 주소는 `http://127.0.0.1:5050`, 다른 PC에서는 `PRODUCTIONLINE_SERVER_URL`로 서버 IP를 지정한다. `ProductionLineApiClient`는 10초 HTTP 타임아웃을 갖고 로그인 후 받은 Bearer 토큰을 후속 요청에 붙인다. 서버의 `appsettings.json`은 `ServerUrl=http://0.0.0.0:5050`으로 바인딩한다. `launchSettings.json`에는 별도의 localhost 개발 프로필이 있다. 유선 이더넷은 물리 연결/네트워크 구성이고 API는 HTTP REST/JSON이다. 서버에 별도 raw TCP 소켓 구현은 없다.

흐름:

```text
AdminApp / UserApp
  → ProductionLineApiClient + Shared DTO
  → Server/Program.cs (HTTP 라우트·권한 검사)
      → AuthService (메모리 Bearer 세션)
      → AppStore (data/store.json)
      → VisionInspector (점수 기반 모의 검사)
      → ManualAssistant (키워드 기반 상담)
```

서버는 `OpenCvSharpFrameAnalyzer`를 등록하거나 호출하지 않는다. 사용자 앱의 4개 검사 버튼은 고정된 형태/색상 점수를 `/api/inspections/simulate`로 보내므로 현재 화면은 카메라·실제 이미지 검사 흐름이 아니다. `OpenCvSharpFrameAnalyzer` 자체는 이미지 바이트를 디코딩하고 기준 영상 크기로 변경한 뒤, Otsu 이진화/최대 외곽선/`MatchShapes`로 형태 점수, BGR 평균 차이로 색상 점수를 계산해 `VisionInspector`에 넘기는 예제다. `VisionInspector`는 기본 임계값 0.85/0.80, 이미지 획득 실패·점수 범위 오류→판정 불가, 기준 미달→불량, 나머지→정상으로 처리한다. 기준 버전·품목 ID·원본 이미지 저장은 아직 없다.

상담 `ManualAssistant`는 카메라·센서·검사 프로그램·설비 네 항목의 키워드를 문제 제목/설명/질문 전체에서 세어 최고 점수의 고정 안내를 돌려준다. 일치 항목이 없으면 `NeedAdmin=true`와 ‘근거 없음’을 반환한다. 이름과 UI에는 AI가 쓰이지만 현재 코드에는 LLM 호출, RAG, 사건별 대화 기억, 답변/출처 영속 저장이 없다.

## API와 공용 계약

| 메서드·경로 | 현재 처리 | 연결 코드 |
|---|---|---|
| `POST /api/auth/login` | 활성 계정+비밀번호 확인 후 8시간 메모리 토큰 발급 | 양쪽 LoginForm → ApiClient.LoginAsync → AppStore.ValidateLogin/AuthService.Create |
| `GET /api/lines` | 관리자 전체, 사용자 허용 라인만 반환 | 양쪽 대시보드 → GetLinesAsync |
| `GET /api/users` | 관리자만 목록 | 관리자 화면 → GetUsersAsync |
| `POST /api/users`, `PUT /api/users/{id}` | 관리자 계정 추가·이름/활성/라인/새 비밀번호 수정 | 관리자 앱은 현재 추가만 호출. 수정 API용 ApiClient 메서드는 아직 없음 |
| `GET /api/inspections?lineId=` | 토큰의 라인 접근 검사 후 최대 100건 | 사용자 화면 → GetInspectionsAsync |
| `POST /api/inspections/simulate` | 실제 LINE 1만, 전달된 점수로 판정 후 JSON 저장 | 사용자 4개 모의 검사 버튼 → VisionInspector → AppStore.AddInspection |
| `GET /api/issues` | 관리자 전체, 사용자 허용 라인만 반환 | 양쪽 목록 → GetIssuesAsync |
| `POST /api/issues` | 제목 검사 후 문제 생성 | 사용자 문제 등록. ‘관리자 문의’ 버튼도 이 API로 새 라인 문제 생성 |
| `PUT /api/issues/{id}/status` | 해당 라인 접근 확인. Closed만 관리자 제한 | 사용자 조치 중/완료 요청, 관리자 종결 |
| `POST /api/ai/consult` | 해당 문제 접근 확인 후 키워드 상담 반환 | 사용자 상담 패널 → ManualAssistant |
| `GET /` | 서비스 기본 응답 | 접속 확인용 |

Shared enum: `UserRole(Admin, User)`, `InspectionResultType(Normal, Defect, Undetermined)`, `IssueType(ProductionLine, InspectionSystem)`, `IssueStatus(Occurred, Acknowledged, InProgress, CompletionRequested, Closed)`. 핵심 DTO는 Login, User, LineStatus, Inspection, Issue, AiConsult와 `ApiMessage`다. `docs/API.md`는 경로 요약이며 정확한 필드는 `Contracts.cs`에 있다. 현재 DTO에는 검사 기준 버전/이미지·품목 ID, 문제 상태 변경 이력, AI 대화/근거 원문, 별도 관리자 문의 구조가 없다. DTO 변경은 네 파트와 합의하고 3번 담당이 최종 반영하는 규칙이다.

## 현재 JSON 저장소와 DB 전환 범위

`AppStore`는 singleton이며 `IHostEnvironment.ContentRootPath/data/store.json`을 시작 시 읽는다. 파일이 없으면 관리자·LINE 1 사용자와 실제 LINE 1/가상 LINE 2~4 데이터를 생성한다. 메서드가 메모리의 `StoreData`를 바꾼 후 `File.WriteAllText`로 파일 전체를 다시 쓴다. `lock`은 단일 프로세스의 동시 호출을 직렬화한다. 비밀번호는 16바이트 난수 salt와 PBKDF2-SHA256 100,000회/32바이트 결과를 `salt.hash` 문자열로 저장하고 고정 시간 비교한다. 이 해시 형식은 DB 전환 시 기존 계정 로그인 보존과 관련된다.

제공된 `store.json`에는 `users` 2명, `lines` 4개, `inspections` 12건, `issues` 7건이 있다. 검사·문제 모두 LINE 1에 속한다. 검사 결과 enum 값은 정상/불량/판정 불가 세 종류이며 문제 유형은 현재 생산라인 문제만 들어 있다. 사용자 ID·검사 ID·문제 ID는 각각 파일 안에서 중복되지 않았다. LINE 1의 누적 정상 3·불량 7·판정 불가 2는 검사 12건과 맞는다. 가상 2~4라인의 수량은 시드 데이터다. 암호 해시·토큰 값·개인 메시지 원문은 이 문서에 복사하지 않았다.

SQLite `.db`/`.sqlite` 파일, SQL 스크립트, EF Core 패키지/`DbContext`/마이그레이션은 제공 소스에 없다. 따라서 ‘JSON을 EF Core·SQLite로 교체’는 **아직 해야 할 작업**이다. 현재 `data/store.json`은 `.gitignore`에 포함되므로 Git에 그대로 올릴 데이터가 아니다. 이력을 보존해야 한다면 기존 JSON의 데이터·ID·해시를 어떻게 SQLite에 이전할지 별도 결정이 필요하다. 데모 데이터를 새 DB에서 재생성하는 방식과 기존 데이터를 이관하는 방식은 결과가 다르므로 임의로 선택하지 않는다.

현재 `StoreData`는 `Users`, `Lines`, `Inspections`, `Issues` 네 목록만 갖는다. `UserRecord`는 ID·이름·역할·해시·활성·라인 권한이며 생성/수정/비활성화 시각과 변경자 이력이 없다. `InspectionDto`는 ID·라인·결과·두 점수·사유·시각뿐이다. `IssueDto`는 ID·라인·유형·제목/설명·현재 상태·생성자·마지막 메모·생성/수정 시각만 있다. 상담과 관리자 문의의 별도 저장 테이블/목록은 없다.

## 파일별 현 상태와 v5 요구사항 차이

- 관리자 앱 `Form1.cs`: 관리자 역할을 로그인 뒤 UI에서도 확인하고 라인/사용자/문제 목록을 보여준다. ‘테스트 사용자 추가’는 고정된 초기 비밀번호의 데모 계정을 만들고 메시지에 비밀번호를 표시한다. ‘선택 문제 종결’은 상태를 Closed로 요청한다. 사용자 수정/비활성화, 기준 관리, 감사/통계, 관리자 문의 답변/지시, 완료 요청 검토와 복구 확인 UI는 없다.
- 사용자 앱 `Form1.cs`: UI는 LINE 1로 고정하고 10초 타이머로 라인/검사/문제를 갱신한다. 4개 모의 검사 버튼, 문제 생성, InProgress·CompletionRequested 요청, 키워드 상담이 연결돼 있다. ‘관리자 문의 등록’은 선택한 문제의 질문/화면 답변을 문자열로 합쳐 **별도 `ProductionLine` 문제를 새로 만든다**. 원문 문의·답변이나 기존 문제와의 참조 관계가 없다. 완료 요청 메모도 고정 문자열이며 사용자 조치 내용 입력은 없다. 검사 화면은 그려진 컨베이어/제품 미리보기이며 카메라 영상이 아니다. 로그아웃은 UI를 닫을 뿐 서버 토큰을 폐기하지 않는다.
- 서버 `Program.cs`: 라우트별 역할/라인 접근 검사의 기본 형태가 있다. `POST /api/inspections/simulate`만 실제 플래그 LINE 1을 요구한다. `POST /api/issues`는 가상 라인에도 관리자라면 생성할 수 있다. 검사 실패에서 검사 시스템 문제 자동 생성은 없다. 미지원 입력의 일부 유효성 검사가 부족하다.
- `AuthService.cs`: 토큰 생성 시점의 역할과 `AllowedLineIds` 목록을 세션에 보관하고 이후 요청 때 현재 DB의 `IsActive`/최신 권한을 다시 조회하지 않는다. 계정을 비활성화하거나 라인 권한을 바꿔도 발급된 토큰은 최대 8시간 그대로 작동할 수 있다. 서버 재시작 때 세션은 사라지지만 명시적 로그아웃/취소 API도 없다. v5의 ‘다음 요청부터 권한 변경 적용’ 요구와 충돌한다.
- `AppStore.cs`: `AddIssue`는 동일 라인/유형 미종결 문제를 중복 검사하지 않는다. `UpdateIssue`는 요청된 enum 상태로 바로 덮어써 허용 순서, 관리자 확인, 반려/복구 확인을 검증하지 않는다. Closed만 관리자로 제한한 API와 합쳐도 관리자가 다른 상태로 되돌릴 수 있고, 사용자도 허용 라인에서 임의 전이를 요청할 수 있다. 기존 조치 메모·변경자·당시 상태를 전환 이력으로 남기지 않는다. 검사는 500건 초과 시 오래된 기록을 삭제해 v5 이력 보존과 충돌하며, `Save`는 파일 전체를 직접 덮어쓰므로 중단 시 원자적 저장을 보장하지 않는다.
- 영상 `VisionInspector.cs`와 `OpenCvSharpFrameAnalyzer.cs`: 실제 사진 처리 예제는 있지만 서버 흐름에 연결되지 않았다. ROI·조명 보정·실제 카메라, 검사 기준/버전, 영상 품질 실패 기준, 최종 이미지 참조가 없다. 색상 점수는 평균 BGR 차이로 계산한다.
- 상담 `ManualAssistant.cs`: 네 개의 고정 매뉴얼 문자열만 검색하며 문서 파일·버전·절/원문 링크·사건별 대화·LLM이 없다. 질문과 답변은 저장되지 않는다.
- `scripts/*.ps1`: `build.ps1`은 솔루션 restore/build, 나머지는 server/admin/user를 `dotnet run`한다. `DOTNET_CLI_HOME`은 루트 `.dotnet-home`으로 지정한다. Windows PowerShell/Visual Studio 실행을 전제로 한다.
- `tools/*.py`: 모두 기획 PDF 작성용이며 ReportLab과 Windows `C:/Windows/Fonts/malgun*.ttf`를 사용한다. `build_revised_plan.py`는 v3, `build_csharp_plan.py`는 v4, `build_named_proposal.py`는 v5를 문구 치환/실행으로 만든다. `build_plan.py`는 그보다 이전 모니터링·채팅 안이다. `build_two_page_brief.py`에는 사용자 앱/영상처리 담당자 이름이 v5와 서로 뒤바뀐 오래된 역할표가 있으므로 최신 역할 기준으로 사용하지 않는다. 해당 스크립트들은 앱에 필요하지 않으며 분석 중 실행하지 않았다.

## 김하균 파트의 연결과 다음 구현 순서

1. `Contracts.cs`와 `docs/API.md`: 계정 관리·검사 결과·문제 전환·상담/문의·오류 계약을 실제 요구사항에 맞춰 팀원과 먼저 고정. 기존 DTO의 모든 필드 변경은 Admin/User/Vision/AI와 연결된다.
2. `Program.cs`/`AuthService.cs`: 매 요청 활성·최신 역할/허용 라인을 확인하고 비활성화·로그아웃·권한 변경을 반영. 존재하지 않는 라인/무효 입력 처리와 상태 전이 권한을 서버에서 검증.
3. `AppStore.cs`→EF Core·SQLite: User/AllowedLine/Line/Inspection/Issue/IssueTransition/InspectionCriterionVersion/AiConversation/AdminInquiry/Audit 등의 저장 단위를 설계. 이는 제안 스키마이며 확정 테이블명은 아니다. `store.json` 기존 기록 이관 여부를 결정하고, 데이터 손실 없이 반복 가능한 초기화/마이그레이션을 준비.
4. 검사: 장서진과 이미지 입력/판정/오류·기준 버전·품목 ID 계약을 맞추고 실제 분석 경로를 연결. 판정 불가와 검사 시스템 문제 생성/중복 방지를 분리해서 검증.
5. 상담/문의: 공도현과 사건 맥락·문서 근거·대화 저장 및 AI 실패 대체 경로를 연결. 관리자 문의는 원래 문제를 참조하는 별도 기록으로 만들고 조영준/문성찬 앱의 화면과 계약을 맞춤.
6. 통합: 두 앱/서버의 빌드 및 두 PC의 로그인·권한 우회/비활성화·검사 세 결과·문제 전이/종결·AI 장애·재연결·중복 요청·이력 보존을 확인한 뒤 Git 병합/실행 상태를 기록.

**이번 검토에서는 소스 파일을 수정하거나 SQLite 전환/병합을 실행하지 않았다.** 정적 분석 및 JSON 구조 검사는 완료했다. 실제 빌드·API 기능 테스트는 .NET SDK와 Windows 실행 환경이 필요하다.
