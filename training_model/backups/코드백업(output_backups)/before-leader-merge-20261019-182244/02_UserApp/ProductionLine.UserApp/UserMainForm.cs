using ProductionLine.Shared;
using System.Text.Json;

namespace ProductionLine.UserApp;

public sealed partial class UserMainForm : Form
{
    private readonly ProductionLineApiClient _api;
    private readonly TabControl _pages = new PageHost { Dock = DockStyle.Fill };
    private readonly ComboBox _lines = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _connection = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 14, 0) };
    private readonly Label _counts = new() { Dock = DockStyle.Top, Height = 64, Font = new Font("맑은 고딕", 14, FontStyle.Bold) };
    private readonly Label _lineInfo = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label _result = new() { Dock = DockStyle.Top, Height = 80 };
    private readonly Label _videoStatus = new() { Dock = DockStyle.Bottom, Height = 48, Text = "카메라 미연결 · 카메라 연결 버튼을 누르세요." };
    private readonly PictureBox _video = new CameraCanvas { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(16, 28, 43) };
    private readonly DataGridView _history = Grid();
    private readonly DataGridView _issues = Grid();
    private readonly TextBox _issueDetail = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
    private Button _acknowledge = null!, _saveAction = null!, _requestCompletion = null!;
    private bool _savingAction;
    private readonly TextBox _action = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, PlaceholderText = "실제로 점검하거나 조치한 내용을 입력하세요." };
    private readonly ComboBox _aiIssue = IssuePicker();
    private readonly ComboBox _adminIssue = IssuePicker();
    private readonly RichTextBox _aiMessages = Messages();
    private readonly RichTextBox _adminMessages = Messages();
    private readonly TextBox _aiInput = ChatInput();
    private readonly TextBox _adminInput = ChatInput();
    private readonly Button _aiSend = new() { Text = "AI에게 보내기", Width = 140, Height = 42 };
    private readonly Label _aiReplyState = new() { Dock = DockStyle.Fill, Text = "상담 대기 · 문제를 선택하고 질문하세요.", AutoEllipsis = true, ForeColor = Color.DimGray };
    private readonly Button _adminSend = new() { Text = "관리자 문의 보내기", Width = 170, Height = 42 };
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 5000 };
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 500 };
    private readonly List<ChatEntry> _conversations = new();
    private readonly Dictionary<Guid, string> _actionDrafts = new();
    private Guid? _actionIssueId;
    private readonly string _conversationFile;
    private bool _loading, _binding;
    private int _lineId;
    public bool LogoutRequested { get; private set; }
    public bool SessionExpired { get; private set; }

    public UserMainForm(ProductionLineApiClient api)
    {
        _api = api;
        Text = "생산라인 검사 및 문제 대응 시스템 · 사용자";
        Size = new Size(1400, 900);
        MinimumSize = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("맑은 고딕", 10);
        BackColor = UserTheme.Canvas;
        var userKey = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(api.CurrentUser!.Id));
        _conversationFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProductionLine.UserApp", userKey + ".json");
        LoadConversations();

        Controls.Add(BuildModernShell());
        AddPage("실시간 검사", InspectionPage());
        AddPage("검사 개수 · 지난 기록", HistoryPage());
        AddPage("문제 · 조치", IssuePage());
        AddPage("AI 상담", ChatPage(false));
        AddPage("관리자 상담", ChatPage(true));
        ApplyModernControls(this);
        _lines.SelectedIndexChanged += async (_, _) =>
        {
            if (_binding || _lines.SelectedItem is not LineStatusDto line) return;
            DisconnectCamera();
            _lineId = line.Id;
            ResetRecognizedResult();
            _video.Image?.Dispose(); _video.Image = null;
            _videoStatus.Text = "카메라 미연결 · 선택한 라인의 카메라를 연결하세요.";
            await RefreshAsync();
        };
        _history.CurrentCellChanged += async (_, _) => { if (!_bindingHistory) await ShowInspectionImageAsync(); };
        _issues.SelectionChanged += (_, _) => ShowIssue();
        _aiIssue.SelectedIndexChanged += (_, _) => RenderChat(false);
        _adminIssue.SelectedIndexChanged += async (_, _) => { if (_binding) return; ChangeAdminIssue(); await PollAdminMessagesAsync(); };
        _aiSend.Click += async (_, _) => await SendChatAsync(false);
        _adminSend.Click += async (_, _) => await SendChatAsync(true);
        _refresh.Tick += async (_, _) => await RefreshAsync(false);
        _messageRefresh.Tick += async (_, _) => await PollAdminMessagesAsync();
        _frames.Tick += (_, _) => DisplayCameraFrame();
        Shown += async (_, _) => { await RefreshAsync(); if (IsDisposed || SessionExpired) return; _refresh.Start(); _messageRefresh.Start(); _frames.Start(); };
        FormClosed += (_, _) => { _runningInspection = false; _camera?.Dispose(); _conveyorHeartbeat.Dispose(); _conveyor?.Dispose(); if (!_inspectionBusy) _yoloInspector?.Dispose(); _inspectionTimer.Dispose(); _messageRefresh.Dispose(); _refresh.Dispose(); _frames.Dispose(); _video.Image?.Dispose(); _recognizedVideo.Image?.Dispose(); _historyImageRequest?.Cancel(); _historyImage.Image?.Dispose(); };
    }

    private Control InspectionPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(16) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 208));
        layout.Controls.Add(_result, 0, 0);
        layout.Controls.Add(RecognitionVideoPanels(), 0, 1);
        layout.Controls.Add(_videoStatus, 0, 2);
        layout.Controls.Add(CameraControls(), 0, 3);
        return layout;
    }

    private Control HistoryPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.Controls.Add(BuildMetricCards(), 0, 0);
        layout.Controls.Add(Surface(null, _lineInfo), 0, 1);
        layout.Controls.Add(Surface("검사 기록 · 선택하면 오른쪽에 사진 표시", HistoryWithImage()), 0, 2);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "개수는 라인 누적값이며, 아래 표는 서버에서 조회한 개별 검사 기록입니다. 현재 최대 100건 조회합니다." }, 0, 3);
        return layout;
    }

    private Control IssuePage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.Controls.Add(Surface("발생한 문제", _issues), 0, 0);
        layout.Controls.Add(Surface("선택한 문제의 현재 정보", _issueDetail), 0, 1);
        layout.Controls.Add(new Label { Text = "새 조치 내용 입력 · 발생 → 확인 → 조치 중 → 완료 요청 → 관리자 종결", AutoSize = true }, 0, 2);
        layout.Controls.Add(_action, 0, 3);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        actions.Controls.Add(Button("문제 등록", async () => await RegisterIssueAsync()));
        _acknowledge = IssueActionButton("문제 확인", IssueStatus.Acknowledged);
        _saveAction = IssueActionButton("점검 시작", IssueStatus.InProgress);
        _requestCompletion = IssueActionButton("완료 요청", IssueStatus.CompletionRequested);
        actions.Controls.Add(_acknowledge);
        actions.Controls.Add(_saveAction);
        actions.Controls.Add(_requestCompletion);
        UpdateIssueActions();
        layout.Controls.Add(actions, 0, 4);
        return layout;
    }

    private Control ChatPage(bool admin)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = admin
            ? "관리자와의 메시지는 선택한 문제 안에서 주고받습니다.\r\n대화는 서버에 저장되며, 새 답장을 5초마다 확인합니다. 종결된 문제는 조회만 가능합니다."
            : "어떤 부분이 막히셨나요? 문제를 선택하고 점검 방법을 질문해 보세요.\r\n답변과 매뉴얼 근거를 안내하며, 대화는 이 PC에 저장됩니다." }, 0, 0);
        layout.Controls.Add(Surface("관련 문제", admin ? _adminIssue : _aiIssue), 0, 1);
        layout.Controls.Add(Surface(admin ? "관리자 문의 내역" : "AI와의 대화", admin ? _adminMessages : _aiMessages), 0, 3);
        layout.Controls.Add(admin ? new Label { Dock = DockStyle.Fill, Text = "관리자 답장은 5초마다 확인합니다." } : _aiReplyState, 0, 2);
        var composer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 10, 0, 0) };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        composer.Controls.Add(admin ? _adminInput : _aiInput, 0, 0);
        composer.Controls.Add(admin ? _adminSend : _aiSend, 1, 0);
        layout.Controls.Add(composer, 0, 4);
        if (admin) { _adminInput.MaxLength = 2000; UpdateAdminAvailability(); }
        return layout;
    }

    private async Task RefreshAsync(bool showError = true)
    {
        if (_loading || IsDisposed) return;
        _loading = true;
        _lines.Enabled = false;
        try
        {
            var lines = await _api.GetLinesAsync();
            if (IsDisposed) return;
            _binding = true;
            try
            {
                if (!lines.Any(x => x.Id == _lineId)) _lineId = lines.FirstOrDefault()?.Id ?? 0;
                _lines.DataSource = lines; _lines.DisplayMember = nameof(LineStatusDto.Name);
                _lines.SelectedItem = lines.FirstOrDefault(x => x.Id == _lineId);
            }
            finally { _binding = false; }
            if (_lineId == 0) throw new InvalidOperationException("허용된 라인이 없습니다. 관리자에게 권한을 요청하세요.");
            var requestedLine = _lineId;
            var inspectionsTask = _api.GetInspectionsAsync(requestedLine);
            var issuesTask = _api.GetIssuesAsync();
            await Task.WhenAll(inspectionsTask, issuesTask);
            if (IsDisposed) return;
            var inspections = inspectionsTask.Result.OrderByDescending(x => x.InspectedAt).ToList();
            var issues = issuesTask.Result.Where(x => x.LineId == requestedLine).ToList();
            var selectedId = SelectedIssue()?.Id;
            BindInspectionHistory(inspections);
            _binding = true;
            try
            {
                _issues.DataSource = issues;
                ConfigureIssues();
                foreach (DataGridViewRow row in _issues.Rows)
                    if (row.DataBoundItem is IssueDto issue && issue.Id == selectedId) { _issues.CurrentCell = row.Cells[nameof(IssueDto.Title)]; break; }
            }
            finally { _binding = false; }
            _binding = true;
            try { BindPicker(_aiIssue, issues); BindPicker(_adminIssue, issues); }
            finally { _binding = false; }
            ChangeAdminIssue();
            _ = PollAdminMessagesAsync();
            var line = lines.First(x => x.Id == requestedLine);
            _lineInfo.Text = $"{line.Name} · {(line.IsReal ? "실제 라인" : "가상 라인")} · 가동: {line.OperationStatus} · 검사: {line.InspectionStatus}\r\n서버 기록 갱신: {line.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            var total = line.NormalCount + line.DefectCount;
            _counts.Text = $"정상 {line.NormalCount:N0}개     불량 {line.DefectCount:N0}개     판정 불가 {line.UndeterminedCount:N0}개     정상률 {(total == 0 ? 0 : line.NormalCount * 100.0 / total):0.0}%";
            UpdateMetricCards(line);
            var latest = inspections.FirstOrDefault();
            if (!_runningInspection && _camera == null && _recognizedVideo.Image == null)
            {
                _result.Text = latest == null ? "검사 결과 대기 중" : latest.DetectionConfidence.HasValue
                    ? $"최근 YOLO 결과: {ResultText(latest.Result)} · 검출 신뢰도 {latest.DetectionConfidence:P1}\r\n{latest.Reason} · {latest.InspectedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                    : $"최근 판정: {ResultText(latest.Result)}    형태 {latest.ShapeScore:P0} / 색상 {latest.ColorScore:P0}\r\n{latest.Reason}    |    {latest.InspectedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
                _result.ForeColor = latest?.Result == InspectionResultType.Defect ? UserTheme.Red : UserTheme.Text;
            }
            _connection.Text = $"연결됨 · {DateTime.Now:HH:mm:ss}";
            ShowIssue();
        }
        catch (Exception ex) { await HandleServerFailureAsync(ex, "조회 실패"); if (showError && !IsDisposed && !SessionExpired) MessageBox.Show(this, ex.Message, "조회 오류"); }
        finally { _loading = false; if (!IsDisposed) _lines.Enabled = true; }
    }

    private async Task RegisterIssueAsync()
    {
        if (_lineId == 0) return;
        using var dialog = new IssueReportDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await SubmitIssueAsync(new CreateIssueRequest(_lineId, dialog.IssueType, dialog.IssueTitle, dialog.Description)); }
        catch (Exception ex) { await HandleServerFailureAsync(ex, "문제 등록 실패"); }
    }

    private async Task SubmitIssueAsync(CreateIssueRequest request)
    {
        await _api.CreateIssueAsync(request);
        await RefreshAfterChangeAsync();
    }

    private async Task SaveActionAsync(IssueStatus status)
    {
        if (_savingAction) return;
        var issue = SelectedIssue();
        if (issue == null) { MessageBox.Show(this, "문제를 선택하세요."); return; }
        if (!CanChangeIssueStatus(issue.Status, status))
        { _connection.Text = "변경할 수 없는 단계입니다. 확인 → 조치 중 → 완료 요청 순서로 처리하세요. 종결은 관리자 담당입니다."; return; }
        var note = _action.Text.Trim();
        if (note.Length == 0) note = status switch
        {
            IssueStatus.Acknowledged => "사용자가 문제를 확인함",
            IssueStatus.InProgress => "사용자가 점검을 시작함",
            _ => string.Empty
        };
        if (note.Length == 0) { MessageBox.Show(this, "점검 또는 처리 내용을 입력하세요."); return; }
        _savingAction = true;
        UpdateIssueActions();
        try
        {
            // 과거 조치 이력은 서버가 관리합니다. 이번에 입력한 내용만 전송합니다.
            await _api.UpdateIssueAsync(issue.Id, new UpdateIssueStatusRequest(status, note));
            _actionDrafts.Remove(issue.Id);
            if (_actionIssueId == issue.Id) _action.Clear();
            await RefreshAfterChangeAsync();
        }
        catch (Exception ex) { await HandleServerFailureAsync(ex, "상태 변경 실패 · 입력 유지됨", refreshIssues: true); }
        finally { _savingAction = false; if (!IsDisposed) UpdateIssueActions(); }
    }

    private async Task SendChatAsync(bool admin)
    {
        if (admin) { await SendAdminMessageAsync(); return; }
        var picker = admin ? _adminIssue : _aiIssue;
        var input = admin ? _adminInput : _aiInput;
        var send = admin ? _adminSend : _aiSend;
        if (picker.SelectedItem is not IssueDto issue) { MessageBox.Show(this, "상담할 문제를 선택하세요."); return; }
        var question = input.Text.Trim();
        if (question.Length == 0) return;
        send.Enabled = false;
        if (!admin) _aiReplyState.Text = "상담 답변을 작성하는 중입니다...";
        try
        {
            {
                var response = await _api.ConsultAiAsync(new AiConsultRequest(issue.Id, question));
                var replyStatus = AiReplyStatus.FromResponse(response);
                _aiReplyState.Text = replyStatus.Label + (response.NeedAdmin ? " · 관리자 확인 권장" : string.Empty);
                _conversations.Add(new(issue.Id, false, "나", question, DateTimeOffset.Now));
                _conversations.Add(new(issue.Id, false, "AI", response.Answer + "\r\n\r\n근거: " + response.ManualSource + (response.NeedAdmin ? "\r\n관리자 상담을 권장합니다." : ""), DateTimeOffset.Now));
            }
            if (input.Text.Trim() == question) input.Clear();
            RenderChat(admin);
            try { Directory.CreateDirectory(Path.GetDirectoryName(_conversationFile)!); File.WriteAllText(_conversationFile, JsonSerializer.Serialize(_conversations)); }
            catch (Exception ex) { MessageBox.Show(this, "전송은 완료됐지만 이 PC에 대화를 저장하지 못했습니다: " + ex.Message); }
        }
        catch (Exception ex)
        {
            await HandleServerFailureAsync(ex, "전송 실패 · 입력 유지됨 · 다시 보내세요");
            if (!admin) _aiReplyState.Text = "상담 전송 실패 · 입력을 보존했습니다. 서버 연결을 확인하세요.";
        }
        finally { if (!IsDisposed) send.Enabled = true; }
    }

    private void LoadConversations()
    {
        try { if (File.Exists(_conversationFile)) _conversations.AddRange(JsonSerializer.Deserialize<List<ChatEntry>>(File.ReadAllText(_conversationFile)) ?? new()); }
        catch (Exception ex) { MessageBox.Show("기존 상담 기록을 읽지 못했습니다: " + ex.Message); }
    }

    private void RenderChat(bool admin)
    {
        var output = admin ? _adminMessages : _aiMessages;
        var picker = admin ? _adminIssue : _aiIssue;
        output.Clear();
        if (admin) { RenderAdminMessages(); return; }
        if (picker.SelectedItem is not IssueDto issue) { output.Text = "문제를 먼저 선택하세요."; return; }
        foreach (var message in _conversations.Where(x => x.IssueId == issue.Id && x.Admin == admin))
            output.AppendText($"[{message.Sender} · {message.Time.ToLocalTime():MM-dd HH:mm}]\r\n{message.Text}\r\n\r\n");
        if (output.TextLength == 0) output.Text = "아직 대화가 없습니다. 아래에 메시지를 입력하세요.";
        output.SelectionStart = output.TextLength; output.ScrollToCaret();
    }

    private static void BindPicker(ComboBox picker, List<IssueDto> issues)
    {
        var id = (picker.SelectedItem as IssueDto)?.Id;
        picker.DataSource = new List<IssueDto>(issues);
        picker.DisplayMember = nameof(IssueDto.Title);
        picker.SelectedItem = issues.FirstOrDefault(x => x.Id == id) ?? issues.FirstOrDefault();
    }

    private void ConfigureIssues()
    {
        foreach (DataGridViewColumn column in _issues.Columns) column.Visible = false;
        foreach (var (name, title) in new[] { (nameof(IssueDto.Title), "문제"), (nameof(IssueDto.Status), "상태"), (nameof(IssueDto.CreatedAt), "발생 시각") })
        { _issues.Columns[name].Visible = true; _issues.Columns[name].HeaderText = title; }
        _issues.CellFormatting -= FormatIssue;
        _issues.CellFormatting += FormatIssue;
    }
    private static void FormatIssue(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value is IssueStatus status) { e.Value = StatusText(status); e.FormattingApplied = true; }
        else if (e.Value is DateTimeOffset time) { e.Value = time.ToLocalTime().ToString("MM-dd HH:mm:ss"); e.FormattingApplied = true; }
    }
    private IssueDto? SelectedIssue() => _issues.CurrentRow?.DataBoundItem as IssueDto;
    private void ShowIssue()
    {
        if (_binding) return;
        var issue = SelectedIssue();
        if (_actionIssueId != issue?.Id)
        {
            if (_actionIssueId is Guid oldId) _actionDrafts[oldId] = _action.Text;
            _actionIssueId = issue?.Id;
            _action.Text = issue != null && _actionDrafts.TryGetValue(issue.Id, out var draft) ? draft : string.Empty;
        }
        _issueDetail.Text = issue == null ? "문제를 선택하세요." :
            $"[{StatusText(issue.Status)}] {issue.Title}\r\n문제 번호: {issue.Id} · 라인: {issue.LineId} · 종류: {(issue.Type == IssueType.ProductionLine ? "생산라인 문제" : "검사 시스템 문제")}\r\n" +
            $"등록자: {issue.CreatedBy} · 등록: {issue.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · 갱신: {issue.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}\r\n" +
            $"증상: {issue.Description}\r\n마지막 조치 메모: {issue.ActionNote ?? "없음"}\r\n" +
            "현재 정보입니다. 누가·언제 변경했는지에 대한 전체 처리 이력은 아직 조회할 수 없습니다.";
        UpdateIssueActions();
    }

    private static bool CanChangeIssueStatus(IssueStatus current, IssueStatus next) => (current, next) switch
    {
        (IssueStatus.Occurred, IssueStatus.Acknowledged) => true,
        (IssueStatus.Acknowledged, IssueStatus.InProgress) => true,
        (IssueStatus.InProgress, IssueStatus.CompletionRequested) => true,
        (IssueStatus.InProgress, IssueStatus.InProgress) => true,
        _ => false
    };

    private Button IssueActionButton(string title, IssueStatus status)
    {
        var button = new Button { Text = title, AutoSize = true, Height = 36, Padding = new Padding(8, 2, 8, 2) };
        button.Click += async (_, _) => await SaveActionAsync(status);
        return button;
    }

    private void UpdateIssueActions()
    {
        var issue = SelectedIssue();
        _acknowledge.Enabled = !_savingAction && issue != null && CanChangeIssueStatus(issue.Status, IssueStatus.Acknowledged);
        _saveAction.Enabled = !_savingAction && issue != null && CanChangeIssueStatus(issue.Status, IssueStatus.InProgress);
        _requestCompletion.Enabled = !_savingAction && issue != null && CanChangeIssueStatus(issue.Status, IssueStatus.CompletionRequested);
        _action.ReadOnly = _savingAction || issue == null || issue.Status is IssueStatus.CompletionRequested or IssueStatus.Closed;
    }

    private void AddPage(string title, Control content) { var page = new TabPage(title) { BackColor = UiCanvas }; page.Controls.Add(content); _pages.TabPages.Add(page); }
    private static Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 36, Padding = new Padding(8, 2, 8, 2) }; b.Click += (_, _) => action(); return b; }
    private static Button Button(string text, Func<Task> action) { var b = new Button { Text = text, AutoSize = true, Height = 36, Padding = new Padding(8, 2, 8, 2) }; b.Click += async (_, _) => { b.Enabled = false; try { await action(); } finally { if (!b.IsDisposed) b.Enabled = true; } }; return b; }
    private static ComboBox IssuePicker() => new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private static RichTextBox Messages() => new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Font = new Font("맑은 고딕", 11) };
    private static TextBox ChatInput() => new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, PlaceholderText = "메시지를 입력하세요." };
    private static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.White };
    private static string ResultText(InspectionResultType result) => result switch { InspectionResultType.Normal => "정상", InspectionResultType.Defect => "불량", _ => "판정 불가" };
    private static string StatusText(IssueStatus status) => status switch { IssueStatus.Occurred => "발생", IssueStatus.Acknowledged => "확인", IssueStatus.InProgress => "조치 중", IssueStatus.CompletionRequested => "완료 요청", IssueStatus.Closed => "종결", _ => status.ToString() };
    public sealed record ChatEntry(Guid IssueId, bool Admin, string Sender, string Text, DateTimeOffset Time);
}
