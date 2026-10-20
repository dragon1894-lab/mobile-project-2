using ProductionLine.AiAssistant;
using ProductionLine.Server.Infrastructure;
using ProductionLine.Shared;
using ProductionLine.Vision;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ServerUrl"] ?? "http://0.0.0.0:5050");
builder.Services.AddSingleton<AppStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<VisionInspector>();
builder.Services.AddProductionLineConsultation(builder.Configuration);

var app = builder.Build();

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
    return session?.Role == UserRole.Admin ? Results.Ok(store.GetUsers()) : Forbidden();
});

app.MapPost("/api/users", (HttpContext context, CreateUserRequest request, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session?.Role != UserRole.Admin) return Forbidden();
    if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name) || request.Password.Length < 6)
        return Error(400, "ID, 이름, 6자 이상의 비밀번호를 입력하세요.");
    try { return Results.Ok(store.AddUser(request)); }
    catch (InvalidOperationException ex) { return Error(409, ex.Message); }
});

app.MapPut("/api/users/{id}", (HttpContext context, string id, UpdateUserRequest request, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session?.Role != UserRole.Admin) return Forbidden();
    try
    {
        var updated = store.UpdateUser(id, request);
        auth.InvalidateUser(id);
        return Results.Ok(updated);
    }
    catch (KeyNotFoundException ex) { return Error(404, ex.Message); }
    catch (InvalidOperationException ex) { return Error(400, ex.Message); }
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

app.MapGet("/api/inspections/{id:guid}/image", (HttpContext context, Guid id, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    var inspection = store.FindInspection(id);
    if (inspection == null) return Error(404, "검사 기록을 찾을 수 없습니다.");
    if (!session.CanAccessLine(inspection.LineId)) return Forbidden();
    context.Response.Headers.CacheControl = "no-store";
    var image = store.GetInspectionImage(id);
    return image == null ? Error(404, "이 기록에는 저장된 인식 사진이 없습니다.") : Results.File(image, "image/jpeg");
});

app.MapPost("/api/inspections/camera", (HttpContext context, CameraInspectionRequest request, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session is null) return Unauthorized();
    if (!session.CanAccessLine(request.LineId)) return Forbidden();
    if (store.FindLine(request.LineId) is not { IsReal: true }) return Error(400, "대표 라인만 실제 검사할 수 있습니다.");
    if (!Enum.IsDefined(request.Result) || !double.IsFinite(request.Confidence) || request.Confidence is < 0 or > 1 ||
        string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
        return Error(400, "카메라 검사 결과와 검출 신뢰도를 확인하세요.");
    var result = request.ClassificationAvailable ? request.Result : InspectionResultType.Undetermined;
    if (request.ResultImage is { } image && (image.Length < 4 || image.Length > 5 * 1024 * 1024 ||
        image[0] != 0xff || image[1] != 0xd8 || image[^2] != 0xff || image[^1] != 0xd9))
        return Error(400, "인식 사진은 5MB 이하의 JPEG 파일이어야 합니다.");
    var reason = request.ClassificationAvailable ? request.Reason.Trim()
        : "품질 판정 미지원 · " + request.Reason.Trim();
    return Results.Ok(store.AddInspection(new InspectionDto(Guid.NewGuid(), request.LineId, result, 0, 0, reason, DateTimeOffset.Now, request.Confidence), request.ResultImage));
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
    return Results.Ok(store.AddIssue(request, session.UserId));
});

app.MapGet("/api/issues/{id:guid}/messages", (HttpContext context, Guid id, long? afterId, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session == null) return Unauthorized();
    var issue = store.FindIssue(id);
    if (issue == null) return Error(404, "문제를 찾을 수 없습니다.");
    if (!session.CanAccessLine(issue.LineId)) return Forbidden();
    if (afterId < 0) return Error(400, "조회 번호를 확인하세요.");
    return Results.Ok(store.GetIssueMessages(id, afterId));
});

app.MapPost("/api/issues/{id:guid}/messages", (HttpContext context, Guid id, SendIssueMessageRequest request, AppStore store, AuthService auth) =>
{
    var session = auth.Get(context);
    if (session == null) return Unauthorized();
    var issue = store.FindIssue(id);
    if (issue == null) return Error(404, "문제를 찾을 수 없습니다.");
    if (!session.CanAccessLine(issue.LineId)) return Forbidden();
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 2000)
        return Error(400, "메시지는 1~2000자로 입력하세요.");
    var sender = store.GetUsers().First(x => x.Id.Equals(session.UserId, StringComparison.OrdinalIgnoreCase));
    try { return Results.Ok(store.AddIssueMessage(id, sender, request.Message)); }
    catch (InvalidOperationException ex) { return Error(409, ex.Message); }
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
    try { return Results.Ok(store.UpdateIssue(id, request, session.Role)); }
    catch (InvalidOperationException ex) { return Error(409, ex.Message); }
});

app.MapPost("/api/ai/consult", async (HttpContext context, AiConsultRequest request,
    AppStore store, AuthService auth, AiConsultationService assistant) =>
{
    var session = auth.Get(context);
    var issue = store.FindIssue(request.IssueId);
    if (session is null) return Unauthorized();
    if (issue is null) return Error(404, "상담할 문제를 먼저 선택하세요.");
    if (!session.CanAccessLine(issue.LineId)) return Forbidden();
    return Results.Ok(await assistant.ConsultAsync(session.UserId, issue, request.Question, context.RequestAborted));
});

app.Run();

static IResult Unauthorized() => Error(StatusCodes.Status401Unauthorized, "로그인이 필요합니다.");
static IResult Forbidden() => Error(StatusCodes.Status403Forbidden, "접근 권한이 없습니다.");
static IResult Error(int statusCode, string message) => Results.Json(new ApiMessage(message), statusCode: statusCode);
