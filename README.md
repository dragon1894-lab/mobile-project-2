# mobile-project-2

2차 프로젝트(생산라인 검사·AI 문제대응 시스템) 작업 저장소입니다. 비공개.

## 어디에 뭐가 있나

| 위치 | 내용 | 누가 보나 |
|---|---|---|
| [`final/`](final/README.md) | **최신 통합본** (2026-10-21 23:14, 컨베이어 PC 기준). `ProductionLine_final_20261021.zip` + 두 PC 연결 확인 순서 | 모두 |
| [`with-claude/`](with-claude/README.md) | 이전 통합 코드 (5명 코드 + 서버·DB, 2026-10-20, 김하균 PC의 노트북용 폴더와 같음). 최신 코드는 `final/` | 참고 |
| [`with-claude-yolo-training/`](with-claude-yolo-training/README.md) | **깨진 아이폰(crack_iphone) YOLO 학습** 인수인계 + 조장 학습 툴 코드 (2026-10-20) | 학습을 맡은 Claude, 김하균 |
| [`all_phones/`](all_phones/README.md) | **아이폰 5종 통합 YOLO 모델 v4**(불량 + 기종 4개, 우드락 컨베이어 사진·15 Pro 보강) 사진·라벨·모델(`models/best.onnx`), 인식률, 컨베이어 PC에서 할 일 (2026-10-21) | 컨베이어 검출 담당 Claude, 조장, 김하균 |
| [`with-claude/md파일/`](with-claude/md파일) | 김하균 서버·DB 정리, 앞으로 할 일, **조원별 정리** | 김하균, 조원 |
| [`docs/AI_COLLAB_LOG.md`](docs/AI_COLLAB_LOG.md) | 날짜별 작업 기록 (무엇을 했고, 어떻게 확인했고, 다음은 무엇인지) | 김하균, Claude, Codex |
| [`docs/PROJECT_HANDOFF.md`](docs/PROJECT_HANDOFF.md) | 프로젝트 인수인계 (방향, 담당, 연결 지점) | 작업을 이어받는 사람·AI |
| [`docs/SOURCE_AUDIT_2026-10-13.md`](docs/SOURCE_AUDIT_2026-10-13.md) | 처음 받은 소스 검토 (10-13 기준, 지금 코드와 다를 수 있음) | 참고 |
| [`AGENTS.md`](AGENTS.md) | AI 에이전트 작업 규칙 | Claude, Codex |

## 빠르게 시작하기

1. 최신 코드 받기와 실행 순서: [`final/README.md`](final/README.md) (압축을 `C:\PL\ProductionLine_final`처럼 짧은 경로에 풀기)
2. 코드 구조와 기능: 압축 안의 `ProductionLine_final/README.md`
3. 서버에 무엇이 있는지: `with-claude/md파일/클로드 버전_서버_추가기능_정리_2026-10-19.md`
4. 최근에 무엇이 바뀌었는지: [`docs/AI_COLLAB_LOG.md`](docs/AI_COLLAB_LOG.md) 맨 아래
5. 조원별로 바뀐 점과 할 일: `with-claude/md파일/조원별_정리_2026-10-20.md`

## 2026-10-21 기준 상태

- 최신 통합본은 `final/ProductionLine_final_20261021.zip` (00_Shared ~ 06_TrainingTool, YOLO 모델 v4, 펌웨어 V7)
- 서버: SQLite DB(계정·검사·문제·이력·관리자 채팅), 단체 채팅은 `data/group-chat.json`, 검사 사진은 `data/inspection-images`. 압축에는 DB·채팅·사진이 빠져 있어 처음 켜면 빈 DB와 기본 계정이 생김
- 서버 연결 창: 관리자 앱 **[서버 켜고 접속]**, 작업자 앱 같은 네트워크 서버 자동 찾기(UDP 5051) + IP 직접 입력
- 작업자 앱: 메뉴 5개(실시간 검사 / 검사 개수·지난 기록 / 문제·조치 / AI 상담 / 단체 채팅), COM 선택·벨트 연결·벨트 구동 버튼
- 관리자 채팅: 별도 1:1 메뉴 없이 **문제·조치 → 관리자 채팅** 탭에서 문제별로, 문제가 조치 중일 때만 전송
- AI 상담: 문제를 고르지 않아도 바로 질문(자유 상담)
- 관리자 앱: 라인 1 실제 벨트·가속·긴급정지·서보 상태, 완료 요청 필터·채팅, **전체 정지**(작업자 정지 응답 확인), 원격 조작
- 빌드 경고·오류 0, 통합 시험 143개 통과. 남은 실패 2개는 옛 기준 화면 시험(앱 문제 아님). 실제 두 PC 연결, 실물 카메라·벨트, 아두이노 V7 업로드는 아직
