# 5번 - 상담 AI와 매뉴얼

`AiConsultationService`가 서버의 상담 요청을 처리합니다. Gemini 또는 OpenAI 연결이 설정되면 매뉴얼·문제·이전 대화를 참고해 생성형 답변을 요청하며, 설정이 없거나 호출이 실패하면 `ManualAssistant`의 기본 매뉴얼 상담으로 전환합니다. MQ-2 가스 센서와 DHT11 온습도 센서는 잠정 모델이며 현장 기준은 미정입니다.

- `ProductionLine.AiAssistant/ManualAssistant.cs`: 주제 구분, 구조화 답변, 수신 오류·가스 경보·관리자 전환 처리
- `ProductionLine.AiAssistant/Manuals.json`: 출처·버전이 포함된 상담 자료. 수정 후 다시 빌드합니다.
- `ProductionLine.AiAssistant.SmokeTests`: 별도 패키지 없는 상담 시나리오 검증
- `AI_CHAT_GUIDE.md`: 상담 시나리오와 매뉴얼 설명
- `../docs/AI_CHAT_INTEGRATION.md`: 서버 연동과 실제 구현 범위
- `../docs/GEMINI_SETUP.md`: 서버 API 키 및 제공자 설정

공용 `AiConsultRequest` / `AiConsultResponse`를 유지합니다. 응답은 `Answer`, `ManualSource`, `NeedAdmin`입니다. 서버 대화 문맥은 사용자·라인·문제별 메모리에 보관하며, 사용자 화면의 표시 대화는 기존 사용자별 로컬 파일에 저장합니다. 실제 센서 데이터 수집과 서버 DB 대화 이력은 별도 작업입니다.

AI는 검사 판정, 설비 제어, 경보 해제, 문제 종결을 수행하지 않습니다. MQ-2 원시값을 ppm으로 해석하지 않고, DHT11 측정 사양을 공정 정상 범위로 사용하지 않습니다.

전체 솔루션을 빌드한 뒤 서버와 사용자 앱을 실행해 확인합니다. 회귀 검증:

```powershell
dotnet run --project .\05_AiAssistant\ProductionLine.AiAssistant.SmokeTests\ProductionLine.AiAssistant.SmokeTests.csproj
```
