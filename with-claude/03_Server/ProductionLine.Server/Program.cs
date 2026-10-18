using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProductionLine.AiAssistant;
using ProductionLine.Server.Data;
using ProductionLine.Server.Infrastructure;
using ProductionLine.Shared;
using ProductionLine.Vision;

var builder = WebApplication.CreateBuilder(args);

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

DatabaseInitializer.Initialize(
    app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>());

// 앱과 서버가 주고받은 요청을 기록합니다.
// 저장·변경 요청, 실패한 요청, 새 메시지를 앱에 전달한 조회만 남기고, 비밀번호·토큰·메시지 내용은 남기지 않습니다.
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

app.MapGet("/", () => Results.Ok(new
{
    service = "ProductionLine.Server",
    message = "생산라인 서버가 실행 중입니다.",
    transport = "Ethernet TCP/IP - HTTP REST/JSON"
}));

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

app.MapGet("/api/lines", (HttpContext context, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    return Results.Ok(session.Role == UserRole.Admin ? store.GetLines() : store.GetLines(session.AllowedLineIds));
});

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
    // 새 메시지를 앱에 전달했을 때만 "전달됨" 증거로 남깁니다. 새 메시지가 없는 주기적 확인은 남기지 않습니다.
    if (messages.Count > 0)
    {
        context.Items[RequestLogItems.Force] = true;
        context.Items[RequestLogItems.Note] = messages.Count == 1
            ? $"메시지 {messages[0].Id}번 전달"
            : $"메시지 {messages[0].Id}~{messages[^1].Id}번 전달 ({messages.Count}건)";
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

app.Run();

static IResult Unauthorized() => Error(StatusCodes.Status401Unauthorized, "로그인이 필요합니다.");
static IResult Forbidden() => Error(StatusCodes.Status403Forbidden, "접근 권한이 없습니다.");
static IResult Error(int statusCode, string message) => Results.Json(new ApiMessage(message), statusCode: statusCode);
