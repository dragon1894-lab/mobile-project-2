# mobile-project-2

2차 프로젝트(생산라인 검사·AI 문제대응 시스템) 작업 저장소입니다. 비공개.

## 어디에 뭐가 있나

| 위치 | 내용 | 누가 보나 |
|---|---|---|
| [`with-claude/`](with-claude/README.md) | **최신 통합 코드** (5명 코드 + 서버·DB, 2026-10-20, 김하균 PC의 노트북용 폴더와 같음) | 모두 |
| [`with-claude/md파일/`](with-claude/md파일) | 김하균 서버·DB 정리, 앞으로 할 일, **조원별 정리** | 김하균, 조원 |
| [`docs/AI_COLLAB_LOG.md`](docs/AI_COLLAB_LOG.md) | 날짜별 작업 기록 (무엇을 했고, 어떻게 확인했고, 다음은 무엇인지) | 김하균, Claude, Codex |
| [`docs/PROJECT_HANDOFF.md`](docs/PROJECT_HANDOFF.md) | 프로젝트 인수인계 (방향, 담당, 연결 지점) | 작업을 이어받는 사람·AI |
| [`docs/SOURCE_AUDIT_2026-10-13.md`](docs/SOURCE_AUDIT_2026-10-13.md) | 처음 받은 소스 검토 (10-13 기준, 지금 코드와 다를 수 있음) | 참고 |
| [`AGENTS.md`](AGENTS.md) | AI 에이전트 작업 규칙 | Claude, Codex |

## 빠르게 시작하기

1. 코드 구조와 실행 방법: [`with-claude/README.md`](with-claude/README.md)
2. 서버에 무엇이 있는지: `with-claude/md파일/클로드 버전_서버_추가기능_정리_2026-10-19.md`
3. 최근에 무엇이 바뀌었는지: [`docs/AI_COLLAB_LOG.md`](docs/AI_COLLAB_LOG.md) 맨 아래
4. 조원별로 바뀐 점과 할 일: `with-claude/md파일/조원별_정리_2026-10-20.md`

## 2026-10-20 기준 상태

- 관리자 앱(조영준), 작업자 앱(장서진)이 서버에 연결된 새 버전으로 교체됨
- 서버: SQLite DB, 문제 처리 순서, 1:1 문의, 통신 기록, **단체 채팅**, **외부 AI(Gemini/OpenAI) 상담** (공도현), **새 아이디 규칙**
- 10-20 수정: 두 앱 화면 배율 글자 잘림 해결, 단체 채팅 파일 한글·한국 시간 저장, 관리자 앱 아이디 중복·규칙 팝업
- 빌드 경고·오류 0, 서버 시험 48개·화면 시험 17개 통과, 김하균 직접 실행·DB 확인. 실제 AI 키 시험과 두 PC 연결 시험은 아직
