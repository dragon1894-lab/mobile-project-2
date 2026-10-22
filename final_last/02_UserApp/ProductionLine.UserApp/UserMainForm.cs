using ProductionLine.Shared;
using ProductionLine.ChatUI;

namespace ProductionLine.UserApp;

public sealed partial class UserMainForm : Form
{
    private readonly ProductionLineApiClient _api;
    private readonly GroupChatPanel _groupChat;
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
    private readonly RichTextBox _aiMessages = Messages();
    private readonly TextBox _aiInput = ChatInput();
    private readonly Button _aiSend = new() { Text = "AI에게 보내기", Width = 140, Height = 42 };
    private readonly Label _aiReplyState = new() { Dock = DockStyle.Fill, Text = "상담 대기 · 문제를 선택하고 질문하세요.", AutoEllipsis = true, ForeColor = Color.DimGray };
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 500 };
    private readonly List<ChatEntry> _conversations = new();
    private readonly Dictionary<Guid, string> _actionDrafts = new();
    private Guid? _actionIssueId;
    private bool _loading, _binding;
    private int _lineId;
    public bool LogoutRequested { get; private set; }
    public bool SessionExpired { get; private set; }

    public UserMainForm(ProductionLineApiClient api)
    {
        _api = api;
        Text = "생산라인 검사 및 문제 대응 시스템 · 사용자 [모던 UI v6.3]";
        Size = new Size(1440, 960);
        MinimumSize = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("맑은 고딕", 10);
        BackColor = UserTheme.Canvas;
        Controls.Add(BuildModernShell());
        AddPage("실시간 검사", InspectionPage());
        AddPage("검사 개수 · 지난 기록", HistoryPage());
        AddPage("문제 · 조치", IssuePage());
        AddPage("AI 상담", ChatPage());
        _groupChat = new GroupChatPanel(api) { Dock = DockStyle.Fill };
        AddPage("단체 채팅", _groupChat);
        var groupMenu = _navigation[^1];
        _groupChat.UnreadChanged += (_, _) => groupMenu.Text = _groupChat.UnreadCount > 0
            ? $"05   단체 채팅 ({_groupChat.UnreadCount})"
            : "05   단체 채팅";
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
        _issues.CurrentCellChanged += (_, _) => ShowIssue();
        _aiSend.Click += async (_, _) => await SendAiChatAsync();
        _messageRefresh.Tick += async (_, _) => await PollAdminMessagesAsync();
        Shown += async (_, _) => { await PollAdminMessagesAsync(); if (!IsDisposed) _messageRefresh.Start(); };
        FormClosed += (_, _) => _messageRefresh.Dispose();
        _refresh.Tick += async (_, _) => await RefreshAsync(false);
        _frames.Tick += (_, _) => DisplayCameraFrame();
        Shown += async (_, _) => { _groupChat.Start(); await RefreshAsync(); if (IsDisposed || SessionExpired) return; _refresh.Start(); _frames.Start(); StartRemoteWorker(); };
        FormClosed += (_, _) => { _runningInspection = false; _camera?.Dispose(); _conveyorHeartbeat.Dispose(); _conveyor?.Dispose(); if (!_inspectionBusy) _yoloInspector?.Dispose(); _inspectionTimer.Dispose(); _refresh.Dispose(); _frames.Dispose(); _video.Image?.Dispose(); _recognizedVideo.Image?.Dispose(); _historyImageRequest?.Cancel(); _historyImage.Image?.Dispose(); _groupChat.Dispose(); };
    }

    private Control InspectionPage()
    {
        var scroll = new InspectionScrollPanel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 4, Padding = new Padding(2), Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 298));
        _result.Font = new Font("맑은 고딕", 10, FontStyle.Bold);
        layout.Controls.Add(Surface(null, _result), 0, 0);
        var monitor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        monitor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        monitor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286));
        monitor.Controls.Add(RecognitionVideoPanels(), 0, 0);
        monitor.Controls.Add(Surface(null, _sensorTrends), 1, 0);
        layout.Controls.Add(monitor, 0, 1);
        var statuses = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        statuses.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); statuses.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        _hardwareSummary.Font = new Font("맑은 고딕", 9);
        _remoteNotice.Font = new Font("맑은 고딕", 9); _remoteNotice.ForeColor = UiMuted;
        statuses.Controls.Add(_hardwareSummary, 0, 0); statuses.Controls.Add(_remoteNotice, 1, 0);
        layout.Controls.Add(statuses, 0, 2);
        _remoteControlArea = CameraControls();
        layout.Controls.Add(Surface("카메라 및 검사 설정", _remoteControlArea), 0, 3);
        scroll.SetContent(layout, 760);
        return scroll;
    }

    private Control HistoryPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.Controls.Add(BuildMetricCards(), 0, 0);
        _lineInfo.AutoSize = true;
        _lineInfo.AutoEllipsis = false;
        _lineInfo.Margin = Padding.Empty;
        var information = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 1, Margin = Padding.Empty };
        information.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        information.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        information.Controls.Add(_lineInfo, 0, 0);
        var informationCard = new RoundedSurface { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14, 12, 14, 12), Margin = new Padding(0, 0, 0, 10) };
        informationCard.Controls.Add(information);
        layout.Controls.Add(informationCard, 0, 1);
        layout.Controls.Add(Surface("검사 기록 · 선택하면 오른쪽에 사진 표시", HistoryWithImage()), 0, 2);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "개수는 라인 누적값이며, 아래 표는 서버에서 조회한 개별 검사 기록입니다. 현재 최대 100건 조회합니다." }, 0, 3);
        return layout;
    }

    private Control IssuePage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(2) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        var list = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        list.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); list.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var register = Button("새 문제 등록", async () => await RegisterIssueAsync()); register.Margin = new Padding(0, 0, 0, 8);
        list.Controls.Add(register, 0, 0); list.Controls.Add(_issues, 0, 1);
        var listCard = Surface("문제 목록", list); listCard.Margin = new Padding(0, 0, 12, 0); layout.Controls.Add(listCard, 0, 0);
        var detail = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        detail.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); detail.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        detail.Controls.Add(Surface("선택한 문제", _issueDetail), 0, 0);
        detail.Controls.Add(Surface("점검 및 처리 내용", _action), 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0), WrapContents = true };
        _acknowledge = IssueActionButton("문제 확인", IssueStatus.Acknowledged);
        _saveAction = IssueActionButton("점검 시작", IssueStatus.InProgress);
        _requestCompletion = IssueActionButton("완료 요청", IssueStatus.CompletionRequested);
        actions.Controls.AddRange([_acknowledge, _saveAction, _requestCompletion]);
        detail.Controls.Add(actions, 0, 2);
        var detailTabs = new TabControl { Dock = DockStyle.Fill };
        var actionTab = new TabPage("점검·조치"); actionTab.Controls.Add(detail);
        var chatTab = new TabPage("관리자 채팅"); chatTab.Controls.Add(BuildIssueChatPanel());
        detailTabs.TabPages.Add(actionTab); detailTabs.TabPages.Add(chatTab);
        layout.Controls.Add(detailTabs, 1, 0);
        UpdateIssueActions(); return layout;
    }

    private Control ChatPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(2) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.Controls.Add(_aiReplyState, 0, 0); layout.Controls.Add(Surface("AI와의 대화", _aiMessages), 0, 1);
        var composer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 8, 0, 0) };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        _aiInput.PlaceholderText = "문제를 등록하지 않아도 바로 질문할 수 있습니다.";
        _aiInput.MaxLength = 4000;
        composer.Controls.Add(_aiInput, 0, 0); composer.Controls.Add(_aiSend, 1, 0);
        layout.Controls.Add(composer, 0, 2); RenderAiChat(); return layout;
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
                // v6 화면에서는 라인 선택 목록을 창에 넣지 않으므로, 숨긴 목록에도 바인딩이 동작하도록 BindingContext를 직접 지정합니다.
                _lines.BindingContext ??= new BindingContext();
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
            BindInspectionHistory(inspections);
            BindIssues(issues);
            var line = lines.First(x => x.Id == requestedLine);
            _lineName.Text = line.Name;
            _lineInfo.Text = $"{line.Name} · {(line.IsReal ? "실제 라인" : "가상 라인")} · 가동: {line.OperationStatus} · 검사: {line.InspectionStatus}\r\n서버 기록 갱신: {line.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            if (line.IsReal) _lineInfo.Text += "\r\n" + HardwareStatusText.Format(line.Hardware);
            if (_conveyor == null)
            {
                _hardwareSummary.Text = HardwareStatusText.Format(line.Hardware);
                if (line.Hardware != null) _sensorTrends.Append(line.Hardware);
            }
            var total = line.NormalCount + line.DefectCount;
            _counts.Text = $"정상 {line.NormalCount:N0}개     불량 {line.DefectCount:N0}개     판정 불가 {line.UndeterminedCount:N0}개     정상률 {(total == 0 ? 0 : line.NormalCount * 100.0 / total):0.0}%";
            UpdateMetricCards(line);
            var latest = inspections.FirstOrDefault();
            if (!_runningInspection && _camera == null && _recognizedVideo.Image == null)
            {
                _result.Text = latest == null ? "검사 결과 대기 중" : latest.DetectionConfidence.HasValue
                    ? $"최근 YOLO 결과: {ResultText(latest.Result)} · 검출 신뢰도 {latest.DetectionConfidence:P1}\r\n{latest.Reason} · {latest.InspectedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                    : $"최근 판정: {ResultText(latest.Result)}\r\n{latest.Reason}    |    {latest.InspectedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
                _result.ForeColor = latest?.Result == InspectionResultType.Defect ? UserTheme.Red : UserTheme.Text;
            }
            _connection.Text = $"연결됨 · 서버 {_api.BaseAddress.Authority} · {DateTime.Now:HH:mm:ss}";
            ShowIssue();
        }
        catch (Exception ex) { await HandleServerFailureAsync(ex, "조회 실패"); if (showError && !IsDisposed && !SessionExpired) MessageBox.Show(this, ex.Message, "조회 오류"); }
        finally { _loading = false; if (!IsDisposed) _lines.Enabled = !_remoteExecuting; }
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
        var note = status switch
        {
            IssueStatus.Acknowledged => "사용자가 문제를 확인함",
            IssueStatus.InProgress => "사용자가 점검을 시작함",
            _ => _action.Text.Trim()
        };
        if (note.Length == 0) { MessageBox.Show(this, "점검 또는 처리 내용을 입력하세요."); return; }
        _savingAction = true;
        UpdateIssueActions();
        try
        {
            // 과거 조치 이력은 서버가 관리합니다. 이번에 입력한 내용만 전송합니다.
            await _api.UpdateIssueAsync(issue.Id, new UpdateIssueStatusRequest(status, note));
            if (status == IssueStatus.CompletionRequested)
            {
                _actionDrafts.Remove(issue.Id);
                if (_actionIssueId == issue.Id) _action.Clear();
            }
            await RefreshAfterChangeAsync();
        }
        catch (Exception ex) { await HandleServerFailureAsync(ex, "상태 변경 실패 · 입력 유지됨", refreshIssues: true); }
        finally { _savingAction = false; if (!IsDisposed) UpdateIssueActions(); }
    }

    private async Task SendAiChatAsync()
    {
        var question = _aiInput.Text.Trim(); if (question.Length == 0) return;
        _aiSend.Enabled = false; _aiReplyState.Text = "답변을 작성하고 있습니다...";
        try
        {
            var response = await AiConsultationApi.ConsultAsync(_api, new AiConsultRequest(null, question));
            _aiReplyState.Text = AiReplyStatus.FromResponse(response).Label;
            _conversations.Add(new(Guid.Empty, false, "나", question, DateTimeOffset.Now));
            _conversations.Add(new(Guid.Empty, false, "AI", response.Answer + "\r\n\r\n근거: " + response.ManualSource + (response.NeedAdmin ? "\r\n담당 관리자에게 확인하세요." : ""), DateTimeOffset.Now));
            if (_aiInput.Text.Trim() == question) _aiInput.Clear(); RenderAiChat();
        }
        catch (Exception ex) { await HandleServerFailureAsync(ex, "AI 상담 전송 실패 · 입력 유지"); _aiReplyState.Text = "전송에 실패했습니다. 연결을 확인하고 다시 보내세요."; }
        finally { if (!IsDisposed) _aiSend.Enabled = true; }
    }

    private void RenderAiChat()
    {
        var text = string.Concat(_conversations.Where(x => x.IssueId == Guid.Empty && !x.Admin).Select(x => $"[{x.Sender} · {x.Time.ToLocalTime():MM-dd HH:mm}]\r\n{x.Text}\r\n\r\n"));
        if (text.Length == 0) text = "AI에게 점검 방법과 문제 해결 순서를 질문해 보세요.";
        if (_aiMessages.Text == text) return;
        _aiMessages.Text = text; _aiMessages.SelectionStart = _aiMessages.TextLength; _aiMessages.ScrollToCaret();
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
        bool issueChanged = _actionIssueId != issue?.Id;
        if (issueChanged)
        {
            if (_actionIssueId is Guid oldId) _actionDrafts[oldId] = _action.Text;
            _actionIssueId = issue?.Id;
            _action.Text = issue != null && _actionDrafts.TryGetValue(issue.Id, out var draft) ? draft : string.Empty;
        }
        var detailText = issue == null ? "문제를 선택하세요." :
            $"[{StatusText(issue.Status)}] {issue.Title}\r\n문제 번호: {issue.Id} · 라인: {issue.LineId} · 종류: {(issue.Type == IssueType.ProductionLine ? "생산라인 문제" : "검사 시스템 문제")}\r\n" +
            $"등록자: {issue.CreatedBy} · 등록: {issue.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · 갱신: {issue.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}\r\n" +
            $"증상: {issue.Description}\r\n마지막 조치 메모: {issue.ActionNote ?? "없음"}\r\n" +
            "현재 정보입니다. 누가·언제 변경했는지에 대한 전체 처리 이력은 아직 조회할 수 없습니다.";
        SetIssueDetailText(detailText, preservePosition: !issueChanged);
        ChangeIssueChat();
        UpdateIssueActions();
    }

    private static bool CanChangeIssueStatus(IssueStatus current, IssueStatus next) => (current, next) switch
    {
        (IssueStatus.Occurred, IssueStatus.Acknowledged) => true,
        (IssueStatus.Acknowledged, IssueStatus.InProgress) => true,
        (IssueStatus.InProgress, IssueStatus.CompletionRequested) => true,
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
