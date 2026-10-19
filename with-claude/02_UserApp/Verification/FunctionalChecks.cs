using ProductionLine.Shared;
using ProductionLine.UserApp;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using OpenCvSharp;
using ProductionLine.Vision;

namespace ProductionLine.Verification;

internal static class FunctionalChecks
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    static T Field<T>(object f, string n) => (T)f.GetType().GetField(n, Flags)!.GetValue(f)!;
    static Task Call(object f, string n, params object[] args) => (Task)f.GetType().GetMethod(n, Flags)!.Invoke(f, args)!;
    static object? Invoke(object f, string n, params object[] args) => f.GetType().GetMethod(n, Flags)!.Invoke(f, args);
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var server = new TestServer();
        using var api = new ProductionLineApiClient(server.Url);
        api.LoginAsync("test", "test").GetAwaiter().GetResult();
        using var form = new UserMainForm(api);
        var file = Field<string>(form, "_conversationFile");
        form.Shown += async (_, _) =>
        {
            try
            {
                for (var i = 0; i < 100 && Field<bool>(form, "_loading"); i++) await Task.Delay(50);
                await Call(form, "RefreshAsync", false);
                Field<System.Windows.Forms.Timer>(form, "_refresh").Stop();
                Check(Field<TabControl>(form, "_pages").TabCount == 5, "five pages");
                Check(Field<Label>(form, "_counts").Text.Contains("정상률"), "counts fetched");
                Field<TabControl>(form, "_pages").SelectedIndex = 2;
                await Task.Delay(100);
                await Call(form, "SubmitIssueAsync", new CreateIssueRequest(1, IssueType.InspectionSystem, "테스트 문제", "증상"));
                Check(server.LastCreate?.Title == "테스트 문제", "problem registration payload");
                var action = Field<TextBox>(form, "_action");
                Check(!Field<Button>(form, "_requestCompletion").Enabled, "completion disabled before action");
                action.Text = "단계 건너뛰기 시도";
                await Call(form, "SaveActionAsync", IssueStatus.CompletionRequested);
                Check(server.LastUpdate == null && action.Text == "단계 건너뛰기 시도", "invalid transition rejected without transmission");
                action.Clear();
                await Call(form, "SaveActionAsync", IssueStatus.Acknowledged);
                Check(server.LastUpdate?.Status == IssueStatus.Acknowledged && server.LastUpdate.ActionNote == "사용자가 문제를 확인함", "acknowledgment works without completion note");
                server.Fail = true; action.Text = "실패해도 유지";
                await Call(form, "SaveActionAsync", IssueStatus.InProgress);
                Check(action.Text == "실패해도 유지" && Field<Button>(form, "_saveAction").Enabled, "action draft retained on start error");
                server.Fail = false;
                await Call(form, "SaveActionAsync", IssueStatus.InProgress);
                Check(server.LastUpdate?.ActionNote == "사용자가 점검을 시작함" && action.Text == "실패해도 유지", "inspection start preserves completion draft");
                Check(!Field<Button>(form, "_saveAction").Enabled, "same-state InProgress request disabled");
                var updateCount = server.UpdateCount;
                await Call(form, "SaveActionAsync", IssueStatus.InProgress);
                Check(server.UpdateCount == updateCount, "same-state request never sent");
                Check(Field<TextBox>(form, "_issueDetail").Text.Contains("마지막 조치 메모: 사용자가 점검을 시작함"), "latest server note displayed");
                Check(Field<Label>(form, "_lineInfo").Text.Contains("실제 라인"), "line status and type displayed");
                action.Text = "완료 확인";
                server.Fail = true;
                await Call(form, "SaveActionAsync", IssueStatus.CompletionRequested);
                Check(action.Text == "완료 확인", "failed completion keeps actual action text");
                server.Fail = false;
                await Call(form, "SaveActionAsync", IssueStatus.CompletionRequested);
                Check(server.LastUpdate?.Status == IssueStatus.CompletionRequested && server.LastUpdate.ActionNote == "완료 확인" && action.Text == "" && action.ReadOnly, "completion sends actual action only and locks input after success");
                Check(!Field<Button>(form, "_saveAction").Enabled, "no user reversal after completion request");
                server.SetIssueState(IssueStatus.InProgress, "관리자 반려: 추가 점검 필요");
                await Call(form, "RefreshAsync", false);
                Check(!action.ReadOnly && Field<Button>(form, "_requestCompletion").Enabled && !Field<Button>(form, "_saveAction").Enabled, "admin rejection enables re-completion but not repeated start");
                Check(Field<TextBox>(form, "_issueDetail").Text.Contains("관리자 반려: 추가 점검 필요"), "rejection reason shown");
                action.Text = "추가 점검 완료";
                await Call(form, "SaveActionAsync", IssueStatus.CompletionRequested);
                server.SetIssueState(IssueStatus.Closed, "관리자 최종 종결");
                await Call(form, "RefreshAsync", false);
                Check(!Field<Button>(form, "_acknowledge").Enabled && !Field<Button>(form, "_requestCompletion").Enabled && action.ReadOnly, "closed issue read-only");
                using (var preview = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(preview, new Rectangle(System.Drawing.Point.Empty, form.Size));
                    preview.Save(Path.Combine(AppContext.BaseDirectory, "db-issue-page.png"));
                }
                var input = Field<TextBox>(form, "_aiInput"); input.Text = "무엇부터 확인하나요?";
                await Call(form, "SendChatAsync", false);
                Check(Field<RichTextBox>(form, "_aiMessages").Text.Contains("테스트 근거"), "AI answer and source");
                server.Fail = true; input.Text = "보존할 질문";
                await Call(form, "SendChatAsync", false);
                Check(input.Text == "보존할 질문" && Field<Button>(form, "_aiSend").Enabled, "AI failed input retained; retry enabled");
                server.Fail = false;
                await Call(form, "SendChatAsync", false);
                Check(input.Text == "", "AI retry");
                Field<TextBox>(form, "_adminInput").Text = "관리자 확인 요청";
                int createdBeforeInquiry = server.CreateCount;
                await Call(form, "SendChatAsync", true);
                Check(server.CreateCount == createdBeforeInquiry && !Field<Button>(form, "_adminSend").Enabled, "closed issue cannot send or create duplicate problem");
                Check(Field<TextBox>(form, "_adminInput").MaxLength == 2000 && Field<TextBox>(form, "_adminInput").ReadOnly, "message length limit and closed-issue read-only");
                server.SetIssueState(IssueStatus.InProgress, "조치 중");
                await Call(form, "RefreshAsync", false);
                await Call(form, "PollAdminMessagesAsync");
                var adminInput = Field<TextBox>(form, "_adminInput");
                Check(Field<Button>(form, "_adminSend").Enabled, "open issue enables message send");
                adminInput.Text = "  서버 문의  ";
                await Call(form, "SendChatAsync", true);
                Check(adminInput.Text == "" && server.LastMessage == "서버 문의" && server.CreateCount == createdBeforeInquiry, "message sent using official API without duplicate issue");
                await Call(form, "PollAdminMessagesAsync");
                var chat = Field<RichTextBox>(form, "_adminMessages").Text;
                Check(chat.Contains("관리자 답장") && chat.Contains("서버 문의") && chat.Split("서버 문의").Length == 2, "server replies displayed and duplicate message IDs merged");
                server.Fail = true; adminInput.Text = "실패 입력 유지";
                await Call(form, "SendChatAsync", true);
                Check(adminInput.Text == "실패 입력 유지" && Field<Button>(form, "_adminSend").Enabled, "message failure retains input and enables manual retry");
                server.Fail = false;
                Check(Field<System.Windows.Forms.Timer>(form, "_messageRefresh").Interval == 5000, "separate five second message poll timer");
                using (var restored = new UserMainForm(api))
                    Check(Field<List<UserMainForm.ChatEntry>>(restored, "_conversations").Count == 4, "AI conversation restored independently from admin messages");
                server.SetIssueState(IssueStatus.Occurred, "발생");
                await Call(form, "RefreshAsync", false);
                server.SetIssueState(IssueStatus.Acknowledged, "다른 PC에서 먼저 확인함");
                action.Text = "409 후에도 보존할 입력";
                updateCount = server.UpdateCount;
                await Call(form, "SaveActionAsync", IssueStatus.Acknowledged);
                Check(server.UpdateCount == updateCount && Field<Button>(form, "_saveAction").Enabled && !Field<Button>(form, "_acknowledge").Enabled, "409 refreshes latest state after concurrent change without retry");
                Check(action.Text == "409 후에도 보존할 입력", "409 preserves draft");
                server.HasIssue = false;
                await Call(form, "SaveActionAsync", IssueStatus.InProgress);
                Check(Field<DataGridView>(form, "_issues").Rows.Count == 0 && !Field<Button>(form, "_saveAction").Enabled, "404 refresh removes missing problem and disables actions");
                server.HasIssue = true;
                server.SetIssueState(IssueStatus.Closed, "최종 종결");
                await Call(form, "RefreshAsync", false);
                using (var expired = new UserMainForm(api))
                {
                    server.AuthFailure = true;
                    await Call(expired, "RefreshAsync", false);
                    Check(expired.SessionExpired && expired.LogoutRequested, "known 401 response returns to login");
                    server.AuthFailure = false;
                }
                // 실제 서버에는 기록하지 않고 로컬 테스트 서버에만 검사 결과를 전송합니다.
                using (var reference = new Mat(120, 160, MatType.CV_8UC3, Scalar.White))
                using (var changed = new Mat(120, 160, MatType.CV_8UC3, Scalar.Black))
                {
                    var analyzer = new OpenCvSharpFrameAnalyzer(new VisionInspector());
                    var normal = analyzer.Inspect(1, reference.ToBytes(".jpg"), reference.ToBytes(".jpg"));
                    var defect = analyzer.Inspect(1, changed.ToBytes(".jpg"), reference.ToBytes(".jpg"));
                    Check(normal.Result == InspectionResultType.Normal && defect.Result == InspectionResultType.Defect, "native OpenCV analysis: matching / changed frames");
                }
                await Call(form, "StartInspectionAsync");
                Check(!Field<bool>(form, "_runningInspection"), "inspection blocked without camera/reference");
                Invoke(form, "ConnectCamera");
                for (int i = 0; i < 100 && Field<object?>(form, "_camera") != null; i++)
                {
                    var camera = Field<object>(form, "_camera");
                    if (camera.GetType().GetProperty("Latest")!.GetValue(camera) != null || camera.GetType().GetProperty("Error")!.GetValue(camera) != null) break;
                    await Task.Delay(100);
                }
                var activeCamera = Field<object?>(form, "_camera");
                if (activeCamera?.GetType().GetProperty("Latest")!.GetValue(activeCamera) != null)
                {
                    Invoke(form, "SetReference");
                    await Call(form, "StartInspectionAsync");
                    Check(server.InspectionCount > 0, "live camera analysis submitted to test server");
                    Invoke(form, "StopInspection", "test stop");
                    int count = server.InspectionCount;
                    await Task.Delay(1700);
                    Check(server.InspectionCount == count, "stop prevents further inspection requests");
                    server.Fail = true;
                    await Call(form, "StartInspectionAsync");
                    Check(!Field<bool>(form, "_runningInspection"), "server error stops inspection");
                    server.Fail = false;
                }
                else Console.WriteLine("SKIP: physical camera not available; live capture/inspection cannot be verified on this device");
                Invoke(form, "DisconnectCamera");
                Check(!Field<bool>(form, "_runningInspection"), "disconnect stops inspection");
                // 장치 유무와 무관하게 같은 검사 루프를 합성 프레임으로 검증합니다.
                using (var image = new Mat(120, 160, MatType.CV_8UC3, Scalar.White))
                {
                    typeof(UserMainForm).GetField("_camera", Flags)!.SetValue(form, new TestFeed(image.ToBytes(".jpg")));
                    Invoke(form, "SetReference");
                    int before = server.InspectionCount;
                    await Call(form, "StartInspectionAsync");
                    Check(server.InspectionCount == before + 1, "start analyzes a frame and sends existing inspection contract");
                    Invoke(form, "StopInspection", "test stop");
                    int stopped = server.InspectionCount;
                    await Task.Delay(1700);
                    Check(server.InspectionCount == stopped, "stop halts periodic requests");
                    server.Fail = true;
                    await Call(form, "StartInspectionAsync");
                    Check(!Field<bool>(form, "_runningInspection"), "inspection auto-stops on server failure");
                    server.Fail = false;
                    Invoke(form, "DisconnectCamera");
                }
                Field<TabControl>(form, "_pages").SelectedIndex = 0;
                using (var preview = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(preview, new Rectangle(System.Drawing.Point.Empty, form.Size)); preview.Save(Path.Combine(AppContext.BaseDirectory, "camera-page.png")); }
                server.Dispose(); input.Text = "연결 끊겨도 유지";
                await Call(form, "SendChatAsync", false);
                Check(input.Text == "연결 끊겨도 유지", "input retained on connection failure");
                foreach (var size in new[] { new System.Drawing.Size(1400, 900), new System.Drawing.Size(1200, 800) })
                {
                    form.Size = size;
                    var pages = Field<TabControl>(form, "_pages");
                    for (int page = 0; page < pages.TabCount; page++)
                    {
                        pages.SelectedIndex = page;
                        form.PerformLayout();
                        using var preview = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(preview, new Rectangle(System.Drawing.Point.Empty, form.Size));
                        preview.Save(Path.Combine(AppContext.BaseDirectory, $"modern-{size.Width}-{page}.png"));
                    }
                }
                Console.WriteLine("ALL CHECKS PASSED; production data unchanged");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (File.Exists(file)) File.Delete(file); form.Close(); }
        };
        Application.Run(form);
    }
}

internal sealed class TestFeed(byte[] jpeg) : ICameraFeed
{
    long _sequence;
    public string? Error => null;
    public CameraFrame Latest => new(jpeg, Interlocked.Increment(ref _sequence), DateTime.UtcNow);
    public Task Completion => Task.CompletedTask;
    public void Dispose() { }
}

internal sealed class TestServer : IDisposable
{
    readonly HttpListener _listener = new();
    readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    IssueDto _issue = new(Guid.NewGuid(), 1, IssueType.InspectionSystem, "초기 문제", "증상", IssueStatus.Occurred, "test", "과거 조치", DateTimeOffset.Now, DateTimeOffset.Now);
    public string Url { get; }
    public bool Fail { get; set; }
    public bool AuthFailure { get; set; }
    public bool HasIssue { get; set; } = true;
    public CreateIssueRequest? LastCreate { get; private set; }
    public UpdateIssueStatusRequest? LastUpdate { get; private set; }
    public int InspectionCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int CreateCount { get; private set; }
    public string? LastMessage { get; private set; }
    readonly List<IssueMessageDto> _messages = new();
    public void SetIssueState(IssueStatus status, string note) => _issue = _issue with { Status = status, ActionNote = note };
    public TestServer()
    {
        var picker = new TcpListener(IPAddress.Loopback, 0); picker.Start();
        var port = ((IPEndPoint)picker.LocalEndpoint).Port; picker.Stop();
        Url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(Url); _listener.Start(); _ = Task.Run(Serve);
    }
    async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext c;
            try { c = await _listener.GetContextAsync(); } catch { break; }
            try
            {
                object result;
                var path = c.Request.Url!.AbsolutePath;
                if (AuthFailure) { c.Response.StatusCode = 401; result = new ApiMessage("로그인이 필요합니다."); }
                else if (Fail) { c.Response.StatusCode = 503; result = new ApiMessage("테스트 서버 오류"); }
                else if (path == "/api/auth/login") result = new LoginResponse("test-token", new UserDto("verification-" + Environment.ProcessId, "검증 사용자", UserRole.User, true, new[] { 1 }));
                else if (path == "/api/lines") result = new[] { new LineStatusDto(1, "테스트 라인", true, "가동", "검사 중", 10, 2, 1, DateTimeOffset.Now) };
                else if (path == "/api/inspections") result = Array.Empty<InspectionDto>();
                else if (path == "/api/inspections/simulate")
                {
                    var request = (await JsonSerializer.DeserializeAsync<InspectionRequest>(c.Request.InputStream, _json))!;
                    result = new VisionInspector().Inspect(request); InspectionCount++;
                }
                else if (path == "/api/issues" && c.Request.HttpMethod == "GET") result = HasIssue ? new[] { _issue } : Array.Empty<IssueDto>();
                else if (path == "/api/issues")
                {
                    LastCreate = (await JsonSerializer.DeserializeAsync<CreateIssueRequest>(c.Request.InputStream, _json))!;
                    CreateCount++;
                    result = _issue with { Id = Guid.NewGuid(), Title = LastCreate.Title, Description = LastCreate.Description };
                }
                else if (path.EndsWith("/messages"))
                {
                    if (_messages.Count == 0) _messages.Add(new(1, _issue.Id, "admin", "관리자", UserRole.Admin, "관리자 답장", DateTimeOffset.Now));
                    if (c.Request.HttpMethod == "POST")
                    {
                        var request = (await JsonSerializer.DeserializeAsync<CreateIssueMessageRequest>(c.Request.InputStream, _json))!;
                        LastMessage = request.Message;
                        var message = new IssueMessageDto(_messages.Count + 1, _issue.Id, "test", "작업자", UserRole.User, request.Message, DateTimeOffset.Now);
                        _messages.Add(message); result = message;
                    }
                    else
                    {
                        long.TryParse(c.Request.QueryString["afterId"], out var after);
                        result = _messages.Where(x => x.Id > after).ToList();
                    }
                }
                else if (path.EndsWith("/status"))
                {
                    var request = (await JsonSerializer.DeserializeAsync<UpdateIssueStatusRequest>(c.Request.InputStream, _json))!;
                    if (!HasIssue) { c.Response.StatusCode = 404; result = new ApiMessage("문제를 찾을 수 없습니다."); }
                    else if ((int)request.Status != (int)_issue.Status + 1 || request.Status == IssueStatus.Closed)
                    { c.Response.StatusCode = 409; result = new ApiMessage("서버 상태 순서 불일치"); }
                    else
                    {
                        LastUpdate = request; UpdateCount++;
                        _issue = _issue with { Status = LastUpdate.Status, ActionNote = LastUpdate.ActionNote }; result = _issue;
                    }
                }
                else if (path == "/api/ai/consult") result = new AiConsultResponse("전원 확인", "테스트 근거", true);
                else { c.Response.StatusCode = 404; result = new ApiMessage("not found"); }
                c.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(c.Response.OutputStream, result, result.GetType(), _json);
            }
            finally { c.Response.Close(); }
        }
    }
    public void Dispose() { if (_listener.IsListening) _listener.Stop(); }
}
