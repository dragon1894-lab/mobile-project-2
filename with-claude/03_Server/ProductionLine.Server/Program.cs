// [서버 시작점] 서버를 실행하면 이 파일이 위에서부터 차례로 실행됩니다.
//   1. 서버 설정 (주소·포트, DB 파일 위치)
//   2. 서버 부품 등록 (저장 담당, 출입증 담당, 검사, AI, 통신 기록)
//   3. DB 준비 (표 만들기·기본 데이터)
//   4. 통신 기록 장치 (모든 요청을 지켜보고 필요한 것만 기록)
//   5. API 창구 (앱이 부르는 주소마다 할 일)
//   6. 서버 실행 (요청을 기다림)
// 모든 창구의 기본 흐름: 출입증 확인(auth.Get) → 권한 확인 → AppStore로 저장·조회 → 결과 반환
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProductionLine.AiAssistant;
using ProductionLine.Server.Data;
using ProductionLine.Server.Infrastructure;
using ProductionLine.Shared;
using ProductionLine.Vision;

// ===== 1. 서버 설정 =====
var builder = WebApplication.CreateBuilder(args);

// 요청을 받을 주소와 포트. appsettings.json의 ServerUrl (기본 0.0.0.0:5050 = 이 PC의 모든 주소, 5050번)

builder.WebHost.UseUrls(
    builder.Configuration["ServerUrl"] ?? "http://0.0.0.0:5050");

// DB 파일을 저장할 폴더와 파일 경로
var databaseDirectory = Path.Combine(
    builder.Environment.ContentRootPath, "App_Data");

Directory.CreateDirectory(databaseDirectory);

var databasePath = Path.Combine(
    databaseDirectory, "productionline.db");

// 필요할 때 AppDbContext를 만들어 SQLite에 연결하도록 등록
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

// ===== 2. 서버 부품 등록 =====
// 여기 등록한 부품은 API 창구의 매개변수(AppStore store, AuthService auth 등)로 자동으로 들어옵니다.
// Singleton = 서버가 켜져 있는 동안 하나만 만들어 계속 같이 씀
builder.Services.AddSingleton<AppStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<VisionInspector>();
builder.Services.AddSingleton<ManualAssistant>();

// 통신 기록: 보관 기간이 지난 기록은 App_Data/통신기록_백업에 CSV로 백업한 뒤 지웁니다.
builder.Services.AddSingleton(new RequestLogOptions
{
    RetentionDays = Math.Max(1, builder.Configuration.GetValue("RequestLogRetentionDays", 7)),
    BackupDirectory = Path.Combine(databaseDirectory, "통신기록_백업")
});
builder.Services.AddSingleton<RequestLogService>();
builder.Services.AddHostedService<RequestLogCleanupService>();

var app = builder.Build();

// ===== 3. DB 준비 ===== (자세한 내용은 Infrastructure/DatabaseInitializer.cs)
DatabaseInitializer.Initialize(
    app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>());

// ===== 4. 통신 기록 장치 =====
// app.Use = 모든 요청이 창구에 가기 전·후에 거쳐 가는 길목. 여기서 요청이 끝난 뒤 기록을 남깁니다.
// 앱과 서버가 주고받은 요청을 기록합니다.
// 저장·변경 요청, 실패한 요청, 새 메시지를 응답한 조회만 남기고, 비밀번호·토큰·메시지 내용은 남기지 않습니다.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    var receivedAt = DateTimeOffset.Now;
    var started = Stopwatch.GetTimestamp();
    var failed = false;
    try
    {
        await next();
    }
    catch
    {
        failed = true;
        throw;
    }
    finally
    {
        var statusCode = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var method = context.Request.Method;
        if (!HttpMethods.IsGet(method) || statusCode >= 400 || context.Items.ContainsKey(RequestLogItems.Force))
        {
            var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            var issueId = context.Items[RequestLogItems.IssueId] as Guid?;
            if (issueId is null && pattern?.StartsWith("/api/issues/") == true
                && Guid.TryParse(context.GetRouteValue("id")?.ToString(), out var routeIssueId))
                issueId = routeIssueId;
            var ip = context.Connection.RemoteIpAddress;

            context.RequestServices.GetRequiredService<RequestLogService>().Record(new RequestLogEntity
            {
                ReceivedAt = receivedAt,
                UserId = context.Items[RequestLogItems.UserId] as string,
                ClientIp = ip is null ? null : (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString(),
                Method = method,
                Path = context.Request.Path.Value ?? "",
                Action = RequestLogService.ActionName(method, pattern, context.Request.Path.Value ?? ""),
                IssueId = issueId,
                StatusCode = statusCode,
                DurationMs = (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Note = context.Items[RequestLogItems.Note] as string
            });
        }
    }
});

// ===== 5. API 창구 =====
// app.MapGet = 조회 창구, app.MapPost = 등록 창구, app.MapPut = 변경 창구
// 공용 코드(ProductionLineApiClient.cs)의 함수 하나가 아래 창구 하나를 부릅니다.

// 서버 살아 있는지 확인용 (브라우저에서 http://서버IP:5050 을 열면 이 메시지가 보임)
app.MapGet("/", () => Results.Ok(new
{
    service = "ProductionLine.Server",
    message = "생산라인 서버가 실행 중입니다.",
    transport = "Ethernet TCP/IP - HTTP REST/JSON"
}));

// --- 로그인 (LoginAsync) --- 성공하면 출입증과 사용자 정보를 돌려줌
app.MapPost("/api/auth/login", (HttpContext context, LoginRequest request, AppStore store, AuthService auth) =>
{
    var user = store.ValidateLogin(request.UserId, request.Password);
    // 실패해도 시도한 아이디는 남깁니다. 비밀번호는 기록하지 않습니다.
    context.Items[RequestLogItems.UserId] = user?.Id ?? request.UserId;
    if (user is null)
    {
        context.Items[RequestLogItems.Note] = "로그인 실패";
        return Error(StatusCodes.Status401Unauthorized, "아이디 또는 비밀번호를 확인하세요.");
    }
    return Results.Ok(new LoginResponse(auth.Create(user), AppStore.ToDto(user)));
});

// --- 라인 조회 (GetLinesAsync) --- 관리자는 전체, 작업자는 자기 라인만
app.MapGet("/api/lines", (HttpContext context, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    return Results.Ok(session.Role == UserRole.Admin ? store.GetLines() : store.GetLines(session.AllowedLineIds));
});

// --- 사용자 관리 (관리자만): 목록 GetUsersAsync, 추가 CreateUserAsync, 수정 UpdateUserAsync ---
app.MapGet("/api/users", (HttpContext context, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    return session.Role == UserRole.Admin ? Results.Ok(store.GetUsers()) : Forbidden();
});

app.MapPost("/api/users", (HttpContext context, CreateUserRequest request, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    if (session.Role != UserRole.Admin) return Forbidden();
    if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name)
        || string.IsNullOrEmpty(request.Password) || request.Password.Length < 6)
        return Error(400, "ID, 이름, 6자 이상의 비밀번호를 입력하세요.");
    try { return Results.Ok(store.AddUser(request)); }
    catch (InvalidOperationException ex) { return Error(409, ex.Message); }
    catch (ArgumentException ex) { return Error(400, ex.Message); }
});

app.MapPut("/api/users/{id}", (HttpContext context, string id, UpdateUserRequest request, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    if (session.Role != UserRole.Admin) return Forbidden();
    try { return Results.Ok(store.UpdateUser(id, request)); }
    catch (KeyNotFoundException ex) { return Error(404, ex.Message); }
    catch (ArgumentException ex) { return Error(400, ex.Message); }
});

// --- 검사: 이력 GetInspectionsAsync, 실행 SimulateInspectionAsync (실제 라인 LINE 1만) ---
app.MapGet("/api/inspections", (HttpContext context, int lineId, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    if (!session.CanAccessLine(lineId)) return Forbidden();
    return Results.Ok(store.GetInspections(lineId));
});

app.MapPost("/api/inspections/simulate", (HttpContext context, InspectionRequest request,
    AppStore store, AuthService auth, VisionInspector inspector) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    if (!session.CanAccessLine(request.LineId)) return Forbidden();
    if (store.FindLine(request.LineId) is not { IsReal: true })
        return Error(400, "대표 라인만 실제 검사할 수 있습니다.");
    var inspection = store.AddInspection(inspector.Inspect(request));
    context.Items[RequestLogItems.Note] = $"검사 {inspection.Id} 결과 {inspection.Result}";
    return Results.Ok(inspection);
});

// --- 문제: 목록 GetIssuesAsync, 등록 CreateIssueAsync, 상태 변경 UpdateIssueAsync ---
// 상태 변경 순서 검사는 AppStore.ValidateIssueTransition이 하고, 여기서는 결과를 번호(400/403/404/409)로 바꿔 돌려줌
app.MapGet("/api/issues", (HttpContext context, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    return Results.Ok(session.Role == UserRole.Admin ? store.GetIssues() : store.GetIssues(session.AllowedLineIds));
});

app.MapPost("/api/issues", (HttpContext context, CreateIssueRequest request, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    if (!session.CanAccessLine(request.LineId)) return Forbidden();
    if (string.IsNullOrWhiteSpace(request.Title)) return Error(400, "문제 제목을 입력하세요.");
    try
    {
        var created = store.AddIssue(request, session.UserId);
        context.Items[RequestLogItems.IssueId] = created.Id;
        return Results.Ok(created);
    }
    catch (ArgumentException ex) { return Error(400, ex.Message); }
});

app.MapPut("/api/issues/{id:guid}/status", (HttpContext context, Guid id, UpdateIssueStatusRequest request,
    AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    var issue = store.FindIssue(id);
    if (session is null) return Unauthorized();
    if (issue is null) return Error(404, "문제를 찾을 수 없습니다.");
    // 성공·거절과 관계없이 어떤 상태 변경을 요청했는지 남깁니다.
    context.Items[RequestLogItems.Note] = $"{issue.Status} → {request.Status}";
    if (!session.CanAccessLine(issue.LineId)) return Forbidden();
    if (request.Status == IssueStatus.Closed && session.Role != UserRole.Admin)
        return Error(403, "문제 종결은 관리자만 수행할 수 있습니다.");
    try { return Results.Ok(store.UpdateIssue(id, request, session.UserId)); }
    catch (KeyNotFoundException ex) { return Error(404, ex.Message); }
    catch (UnauthorizedAccessException ex) { return Error(403, ex.Message); }
    catch (ArgumentException ex) { return Error(400, ex.Message); }
    catch (InvalidOperationException ex) { return Error(409, ex.Message); }
});

// --- AI 상담 (ConsultAiAsync) --- 05_AiAssistant의 ManualAssistant가 답을 만듦. 상담 내용은 아직 DB에 저장하지 않음
app.MapPost("/api/ai/consult", (HttpContext context, AiConsultRequest request,
    AppStore store, AuthService auth, ManualAssistant assistant) =>
{
    var session = auth.Get(context);
    var issue = store.FindIssue(request.IssueId);
    if (session is null) return Unauthorized();
    if (issue is null) return Error(404, "상담할 문제를 먼저 선택하세요.");
    if (!session.CanAccessLine(issue.LineId)) return Forbidden();
    return Results.Ok(assistant.Consult(issue, request.Question));
});

// --- 문의 메시지: 읽기 GetIssueMessagesAsync, 보내기 SendIssueMessageAsync ---
// 문제 한 건에 대한 작업자 문의·관리자 답변 (AI 상담과 별도)
app.MapGet("/api/issues/{id:guid}/messages", (HttpContext context, Guid id, long? afterId,
    AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    var issue = store.FindIssue(id);
    if (issue is null) return Error(404, "문제를 찾을 수 없습니다.");
    if (!session.CanAccessLine(issue.LineId)) return Forbidden();
    var messages = store.GetIssueMessages(id, afterId);
    // 새 메시지를 응답으로 보냈을 때만 남깁니다. 서버가 응답했다는 근거이며, 앱의 실제 수신·화면 표시까지 증명하지는 않습니다.
    // 새 메시지가 없는 주기적 확인은 남기지 않습니다.
    if (messages.Count > 0)
    {
        context.Items[RequestLogItems.Force] = true;
        context.Items[RequestLogItems.Note] = messages.Count == 1
            ? $"메시지 {messages[0].Id}번 조회 응답"
            : $"메시지 {messages[0].Id}~{messages[^1].Id}번 조회 응답 ({messages.Count}건)";
    }
    return Results.Ok(messages);
});

app.MapPost("/api/issues/{id:guid}/messages", (HttpContext context, Guid id, CreateIssueMessageRequest request,
    AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    var issue = store.FindIssue(id);
    if (issue is null) return Error(404, "문제를 찾을 수 없습니다.");
    if (!session.CanAccessLine(issue.LineId)) return Forbidden();
    try
    {
        var saved = store.AddIssueMessage(id, session.UserId, request.Message);
        context.Items[RequestLogItems.Note] = $"메시지 {saved.Id}번 저장";
        return Results.Ok(saved);
    }
    catch (KeyNotFoundException ex) { return Error(404, ex.Message); }
    catch (ArgumentException ex) { return Error(400, ex.Message); }
    catch (InvalidOperationException ex) { return Error(409, ex.Message); }
});

// ===== 6. 서버 실행 ===== 여기서부터 서버가 요청을 기다립니다 (창을 닫거나 Ctrl+C로 종료)
app.Run();

// 오류 응답 도우미: 앱에 결과 번호와 { "message": "안내 문구" }를 돌려줌
// 401 = 로그인 필요, 403 = 권한 없음, 그 밖의 번호는 Error(번호, 문구)로 직접 지정
static IResult Unauthorized() => Error(StatusCodes.Status401Unauthorized, "로그인이 필요합니다.");
static IResult Forbidden() => Error(StatusCodes.Status403Forbidden, "접근 권한이 없습니다.");
static IResult Error(int statusCode, string message) => Results.Json(new ApiMessage(message), statusCode: statusCode);
