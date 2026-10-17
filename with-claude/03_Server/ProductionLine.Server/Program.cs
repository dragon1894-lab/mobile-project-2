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

var app = builder.Build();

DatabaseInitializer.Initialize(
    app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>());

app.MapGet("/", () => Results.Ok(new
{
    service = "ProductionLine.Server",
    message = "생산라인 서버가 실행 중입니다.",
    transport = "Ethernet TCP/IP - HTTP REST/JSON"
}));

app.MapPost("/api/auth/login", (LoginRequest request, AppStore store, AuthService auth) =>
{
    var user = store.ValidateLogin(request.UserId, request.Password);
    return user is null
        ? Error(StatusCodes.Status401Unauthorized, "아이디 또는 비밀번호를 확인하세요.")
        : Results.Ok(new LoginResponse(auth.Create(user), AppStore.ToDto(user)));
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
    return Results.Ok(store.AddInspection(inspector.Inspect(request)));
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
    try { return Results.Ok(store.AddIssue(request, session.UserId)); }
    catch (ArgumentException ex) { return Error(400, ex.Message); }
});

app.MapPut("/api/issues/{id:guid}/status", (HttpContext context, Guid id, UpdateIssueStatusRequest request,
    AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    var issue = store.FindIssue(id);
    if (session is null) return Unauthorized();
    if (issue is null) return Error(404, "문제를 찾을 수 없습니다.");
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

app.Run();

static IResult Unauthorized() => Error(StatusCodes.Status401Unauthorized, "로그인이 필요합니다.");
static IResult Forbidden() => Error(StatusCodes.Status403Forbidden, "접근 권한이 없습니다.");
static IResult Error(int statusCode, string message) => Results.Json(new ApiMessage(message), statusCode: statusCode);
