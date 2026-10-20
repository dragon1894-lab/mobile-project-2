# 기본 REST API

기본 주소: `http://서버IP:5050`

로그인 성공 후 `Authorization: Bearer {token}` 헤더를 사용합니다.

| Method | Path | 권한 | 기능 |
|---|---|---|---|
| POST | `/api/auth/login` | 공개 | 로그인 |
| GET | `/api/lines` | 로그인 | 접근 가능한 라인 조회 |
| GET/POST/PUT | `/api/users` | 관리자 | 사용자 관리 (새 아이디는 영어·숫자·`_`·`-` 3~20자, 중복은 409) |
| GET | `/api/inspections?lineId=1` | 라인 권한 | 검사 이력 |
| POST | `/api/inspections/simulate` | 라인 권한 | 기본 검사 실행 |
| GET/POST | `/api/issues` | 라인 권한 | 문제 조회·등록 |
| PUT | `/api/issues/{id}/status` | 라인 권한 | 문제 상태 변경 |
| POST | `/api/ai/consult` | 라인 권한 | 문제 상담 |
| GET | `/api/issues/{id}/messages?afterId=` | 라인 권한 | 문제의 문의·답변 메시지 조회 (`afterId`는 선택, 그 번호보다 새 메시지만) |
| POST | `/api/issues/{id}/messages` | 라인 권한 | 문의·답변 메시지 보내기 (1~2,000자, 종결된 문제는 불가) |
| GET | `/api/chat/messages?afterId=&limit=` | 활성 계정 | 단체 채팅 읽기 (limit 1~100) |
| POST | `/api/chat/messages` | 활성 계정 | 단체 채팅 보내기 (`ClientMessageId`로 중복 방지, 1~2,000자) |

정확한 요청·응답 필드는 `00_Shared/ProductionLine.Shared/Contracts.cs`, 단체 채팅은 `ChatContracts.cs`를 기준으로 합니다.
