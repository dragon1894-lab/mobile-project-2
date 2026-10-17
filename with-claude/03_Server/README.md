# 3번 - C# 서버, 통신, 데이터

ASP.NET Core 서버가 `0.0.0.0:5050`에서 유선 이더넷 TCP/IP의 HTTP REST/JSON 요청을 받습니다.

- 로그인 및 역할/라인 권한 검사
- 라인, 사용자, 검사, 문제 API
- 영상처리/상담 AI 모듈 연결
- 현재 JSON 저장소 관리

서버 PC의 유선 LAN IPv4가 `192.168.0.10`이라면 클라이언트 PC에서 아래 환경 변수를 설정합니다.

```powershell
$env:PRODUCTIONLINE_SERVER_URL='http://192.168.0.10:5050'
```

Windows 방화벽에서 TCP 5050 인바운드 허용이 필요할 수 있습니다.

`Infrastructure/AppStore.cs`는 실행 가능한 초기 JSON 저장소입니다. DB 담당자는 공개 메서드를 유지하면서 EF Core/SQLite 저장소로 교체합니다.
