# 기본 REST API

기본 주소: `http://서버IP:5050`

로그인 성공 후 `Authorization: Bearer {token}` 헤더를 사용합니다.

| Method | Path | 권한 | 기능 |
|---|---|---|---|
| POST | `/api/auth/login` | 공개 | 로그인 |
| GET | `/api/lines` | 로그인 | 접근 가능한 라인 조회 |
| GET/POST/PUT | `/api/users` | 관리자 | 사용자 관리 |
| GET | `/api/inspections?lineId=1` | 라인 권한 | 검사 이력 |
| POST | `/api/inspections/simulate` | 라인 권한 | 기본 검사 실행 |
| GET/POST | `/api/issues` | 라인 권한 | 문제 조회·등록 |
| PUT | `/api/issues/{id}/status` | 라인 권한 | 문제 상태 변경 |
| POST | `/api/ai/consult` | 라인 권한 | 문제 상담 |

정확한 요청·응답 필드는 `00_Shared/ProductionLine.Shared/Contracts.cs`를 기준으로 합니다.
