using ProductionLine.Shared;

namespace ProductionLine.UserApp;

internal static class UserTheme
{
    public static readonly Color Navy = Color.FromArgb(20, 39, 66);
    public static readonly Color Blue = Color.FromArgb(32, 102, 190);
    public static readonly Color PaleBlue = Color.FromArgb(235, 243, 253);
    public static readonly Color Surface = Color.White;
    public static readonly Color Canvas = Color.FromArgb(244, 247, 251);
    public static readonly Color Border = Color.FromArgb(218, 225, 234);
    public static readonly Color Text = Color.FromArgb(34, 45, 60);
    public static readonly Color Muted = Color.FromArgb(103, 116, 135);
    public static readonly Color Green = Color.FromArgb(22, 145, 95);
    public static readonly Color Red = Color.FromArgb(211, 69, 77);
    public static readonly Color Amber = Color.FromArgb(221, 145, 27);
}

public sealed class UserLoginForm : Form
{
    private readonly TextBox _id = Input("worker1");
    private readonly TextBox _password = Input("worker123!", true);
    private readonly Label _status = new()
    {
        AutoSize = false,
        Height = 36,
        Dock = DockStyle.Top,
        ForeColor = UserTheme.Red,
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Button _loginButton;

    public UserLoginForm()
    {
        Text = "생산라인 검사 시스템 · 사용자 로그인";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 560);
        MinimumSize = new Size(820, 520);
        Font = new Font("맑은 고딕", 10);
        BackColor = UserTheme.Canvas;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = UserTheme.Navy, Padding = new Padding(52) };
        var brandContent = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        brandContent.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        brandContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brandContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brandContent.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        brandContent.Controls.Add(new Panel(), 0, 0);
        brandContent.Controls.Add(new Label
        {
            Text = "SMART LINE",
            AutoSize = true,
            ForeColor = Color.FromArgb(108, 177, 255),
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);
        brandContent.Controls.Add(new Label
        {
            Text = "생산라인 검사 및\r\n문제 대응 시스템",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 24, FontStyle.Bold),
            Margin = Padding.Empty
        }, 0, 2);
        brand.Controls.Add(brandContent);

        var loginArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var loginCard = new TableLayoutPanel
        {
            Width = 330,
            Height = 365,
            ColumnCount = 1,
            RowCount = 10,
            Anchor = AnchorStyles.None,
            BackColor = Color.White
        };
        loginCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        loginCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        loginCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        loginCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        loginCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        loginCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        loginCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        loginCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        loginCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        loginCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        loginCard.Controls.Add(new Label
        {
            Text = "사용자 로그인",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 22, FontStyle.Bold),
            ForeColor = UserTheme.Text
        }, 0, 0);
        loginCard.Controls.Add(new Label
        {
            Text = "관리자가 등록한 계정으로 로그인하세요.",
            AutoSize = true,
            ForeColor = UserTheme.Muted,
            Margin = new Padding(0, 8, 0, 22)
        }, 0, 1);
        loginCard.Controls.Add(FieldLabel("사용자 ID"), 0, 2);
        loginCard.Controls.Add(_id, 0, 3);
        loginCard.Controls.Add(FieldLabel("비밀번호"), 0, 5);
        loginCard.Controls.Add(_password, 0, 6);
        _loginButton = PrimaryButton("로그인");
        _loginButton.Click += LoginClicked;
        loginCard.Controls.Add(_loginButton, 0, 8);
        loginCard.Controls.Add(_status, 0, 9);
        loginArea.Controls.Add(loginCard);
        loginArea.Resize += (_, _) =>
            loginCard.Location = new Point((loginArea.ClientSize.Width - loginCard.Width) / 2, (loginArea.ClientSize.Height - loginCard.Height) / 2);

        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(loginArea, 1, 0);
        Controls.Add(layout);
        AcceptButton = _loginButton;
    }

    private async void LoginClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_id.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            _status.Text = "아이디와 비밀번호를 입력하세요.";
            return;
        }

        try
        {
            _loginButton.Enabled = false;
            _loginButton.Text = "서버 연결 중...";
            _status.Text = string.Empty;
            using var api = new ProductionLineApiClient();
            var user = await api.LoginAsync(_id.Text.Trim(), _password.Text);
            if (user.Role != UserRole.User)
                throw new InvalidOperationException("일반 사용자 계정으로 로그인하세요.");

            Hide();
            using var main = new UserMainForm(api);
            main.ShowDialog(this);
            if (main.LogoutRequested)
            {
                _password.Clear();
                _status.Text = "로그아웃되었습니다.";
                Show();
                _password.Focus();
            }
            else
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            _loginButton.Enabled = true;
            _loginButton.Text = "로그인";
        }
    }

    private static TextBox Input(string text, bool password = false) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        UseSystemPasswordChar = password,
        Font = new Font("맑은 고딕", 11),
        Margin = Padding.Empty
    };

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Dock = DockStyle.Fill,
        ForeColor = UserTheme.Text,
        Font = new Font("맑은 고딕", 9, FontStyle.Bold),
        TextAlign = ContentAlignment.BottomLeft
    };

    private static Button PrimaryButton(string text) => StyledButton(text, UserTheme.Blue, Color.White);

    internal static Button StyledButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold),
            Margin = Padding.Empty
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}

public sealed class UserMainForm : Form
{
    private const int LineId = 1;
    private readonly ProductionLineApiClient _api;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 10_000 };
    private readonly Label _operationBadge = Badge("확인 중", UserTheme.Muted);
    private readonly Label _inspectionBadge = Badge("확인 중", UserTheme.Muted);
    private readonly Label _connectionLabel = new() { AutoSize = true, ForeColor = UserTheme.Green, Text = "● 서버 연결됨" };
    private readonly Label _lastUpdated = new() { AutoSize = true, ForeColor = UserTheme.Muted };
    private readonly Label _normalValue = MetricValue("0");
    private readonly Label _defectValue = MetricValue("0");
    private readonly Label _undeterminedValue = MetricValue("0");
    private readonly Label _yieldValue = MetricValue("0.0%");
    private readonly InspectionPreview _preview = new() { Dock = DockStyle.Fill };
    private readonly Label _latestResult = new()
    {
        Dock = DockStyle.Top,
        Height = 52,
        Text = "검사 대기",
        Font = new Font("맑은 고딕", 18, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Label _latestReason = new()
    {
        Dock = DockStyle.Fill,
        ForeColor = UserTheme.Muted,
        AutoEllipsis = true,
        Padding = new Padding(0, 8, 0, 0)
    };
    private readonly ProgressBar _shapeProgress = ScoreBar();
    private readonly ProgressBar _colorProgress = ScoreBar();
    private readonly Label _shapeScore = ScoreLabel();
    private readonly Label _colorScore = ScoreLabel();
    private readonly DataGridView _inspections = InspectionGrid();
    private readonly DataGridView _issues = IssueGrid();
    private readonly Label _selectedIssue = new()
    {
        Dock = DockStyle.Top,
        Height = 54,
        ForeColor = UserTheme.Text,
        Padding = new Padding(4, 8, 4, 4),
        AutoEllipsis = true,
        Text = "문제를 선택하세요."
    };
    private readonly TextBox _question = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        Height = 54,
        PlaceholderText = "선택한 문제에 대해 질문하세요. 예: 어떤 순서로 점검해야 하나요?",
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("맑은 고딕", 9.5f)
    };
    private readonly RichTextBox _answer = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(248, 250, 253),
        ForeColor = UserTheme.Text,
        Font = new Font("맑은 고딕", 9.5f),
        Text = "문제를 선택하면 매뉴얼을 근거로 점검 순서를 안내합니다."
    };
    private readonly Button _askButton = UserLoginForm.StyledButton("AI에게 점검 방법 묻기", UserTheme.Blue, Color.White);
    private readonly Button _adminInquiryButton = UserLoginForm.StyledButton("관리자 문의 등록", Color.FromArgb(230, 235, 242), UserTheme.Text);
    private bool _loading;

    public bool LogoutRequested { get; private set; }

    public UserMainForm(ProductionLineApiClient api)
    {
        _api = api;
        Text = "생산라인 검사 및 문제 대응 시스템 · 사용자";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 780);
        Font = new Font("맑은 고딕", 9.5f);
        BackColor = UserTheme.Canvas;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildStatusStrip(), 0, 1);
        root.Controls.Add(BuildWorkspace(), 0, 2);
        Controls.Add(root);

        _inspections.CellFormatting += InspectionCellFormatting;
        _issues.CellFormatting += IssueCellFormatting;
        _issues.SelectionChanged += (_, _) => ShowSelectedIssue();
        _askButton.Click += AskAi;
        _adminInquiryButton.Click += RegisterAdminInquiry;
        _refreshTimer.Tick += async (_, _) => await LoadAllAsync(false);
        Shown += async (_, _) =>
        {
            await LoadAllAsync();
            _refreshTimer.Start();
        };
        FormClosed += (_, _) => _refreshTimer.Dispose();
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = UserTheme.Navy, Padding = new Padding(24, 0, 18, 0) };
        header.Controls.Add(new Label
        {
            Text = "LINE 1  |  검사 운영 화면",
            Dock = DockStyle.Left,
            Width = 520,
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 19, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 500,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 15, 0, 13),
            BackColor = UserTheme.Navy
        };
        var logout = UserLoginForm.StyledButton("로그아웃", Color.FromArgb(41, 63, 91), Color.White);
        logout.Size = new Size(90, 38);
        logout.Margin = new Padding(12, 0, 0, 0);
        logout.Click += (_, _) => { LogoutRequested = true; Close(); };
        right.Controls.Add(logout);
        right.Controls.Add(new Label
        {
            Text = $"{_api.CurrentUser?.Name}  ·  작업자",
            AutoSize = false,
            Width = 230,
            Height = 38,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleRight
        });
        header.Controls.Add(right);
        return header;
    }

    private Control BuildStatusStrip()
    {
        var strip = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 8, 24, 6) };
        var left = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false };
        left.Controls.Add(new Label { Text = "라인 상태", AutoSize = true, ForeColor = UserTheme.Muted, Margin = new Padding(0, 4, 8, 0) });
        left.Controls.Add(_operationBadge);
        left.Controls.Add(new Label { Text = "검사 상태", AutoSize = true, ForeColor = UserTheme.Muted, Margin = new Padding(20, 4, 8, 0) });
        left.Controls.Add(_inspectionBadge);
        strip.Controls.Add(left);

        var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _lastUpdated.Margin = new Padding(18, 4, 0, 0);
        _connectionLabel.Margin = new Padding(0, 4, 0, 0);
        right.Controls.Add(_lastUpdated);
        right.Controls.Add(_connectionLabel);
        strip.Controls.Add(right);
        return strip;
    }

    private Control BuildWorkspace()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 830,
            SplitterWidth = 8,
            BackColor = UserTheme.Canvas,
            Padding = new Padding(16, 14, 8, 16),
            FixedPanel = FixedPanel.Panel2
        };
        split.Panel1.Padding = new Padding(0, 0, 8, 0);
        split.Panel2.Padding = new Padding(8, 0, 0, 0);
        split.Panel1.Controls.Add(BuildInspectionArea());
        split.Panel2.Controls.Add(BuildResponseArea());
        return split;
    }

    private Control BuildInspectionArea()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 0, 0, 10) };
        for (var i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.Controls.Add(MetricCard("정상", _normalValue, UserTheme.Green), 0, 0);
        metrics.Controls.Add(MetricCard("불량", _defectValue, UserTheme.Red), 1, 0);
        metrics.Controls.Add(MetricCard("판정 불가", _undeterminedValue, UserTheme.Amber), 2, 0);
        metrics.Controls.Add(MetricCard("정상률", _yieldValue, UserTheme.Blue), 3, 0);
        layout.Controls.Add(metrics, 0, 0);

        var inspectionSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 520, SplitterWidth = 8, BackColor = UserTheme.Canvas };
        inspectionSplit.Panel1.Padding = new Padding(0, 0, 4, 0);
        inspectionSplit.Panel2.Padding = new Padding(4, 0, 0, 0);
        inspectionSplit.Panel1.Controls.Add(Card("실시간 검사 화면", BuildCameraPanel(), "CAM-01  ·  1920×1080"));
        inspectionSplit.Panel2.Controls.Add(Card("최근 판정", BuildLatestResultPanel()));
        layout.Controls.Add(inspectionSplit, 0, 1);
        layout.Controls.Add(Card("검사 이력", _inspections, "최근 100건"), 0, 2);
        return layout;
    }

    private Control BuildCameraPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(21, 27, 36), Padding = new Padding(12) };
        panel.Controls.Add(_preview);
        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            ColumnCount = 4,
            BackColor = Color.FromArgb(21, 27, 36),
            Padding = new Padding(0, 6, 0, 0)
        };
        for (var i = 0; i < 4; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        buttons.Controls.Add(ActionButton("정상 검사", UserTheme.Green, (_, _) => Inspect(0.96, 0.94, true)), 0, 0);
        buttons.Controls.Add(ActionButton("형태 불량", UserTheme.Red, (_, _) => Inspect(0.62, 0.93, true)), 1, 0);
        buttons.Controls.Add(ActionButton("색상 불량", UserTheme.Red, (_, _) => Inspect(0.94, 0.55, true)), 2, 0);
        buttons.Controls.Add(ActionButton("판정 불가", UserTheme.Amber, (_, _) => Inspect(0, 0, false)), 3, 0);
        panel.Controls.Add(buttons);
        return panel;
    }

    private Control BuildLatestResultPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18, 12, 18, 14) };
        panel.Controls.Add(_latestReason);
        panel.Controls.Add(ScoreRow("색상", _colorProgress, _colorScore));
        panel.Controls.Add(ScoreRow("형태·윤곽", _shapeProgress, _shapeScore));
        panel.Controls.Add(_latestResult);
        return panel;
    }

    private Control BuildResponseArea()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.Controls.Add(Card("라인 문제 및 조치", BuildIssuePanel(), "사용자 조치 → 관리자 종결"), 0, 0);
        var aiCard = Card("매뉴얼 기반 상담 AI", BuildAiPanel(), "검출 판정과 분리");
        aiCard.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(aiCard, 0, 1);
        return layout;
    }

    private Control BuildIssuePanel()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12, 8, 12, 12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(_issues, 0, 0);
        layout.Controls.Add(_selectedIssue, 0, 1);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        for (var i = 0; i < 3; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        actions.Controls.Add(ActionButton("문제 등록", UserTheme.Red, CreateIssue), 0, 0);
        actions.Controls.Add(ActionButton("점검 시작", UserTheme.Blue, StartAction), 1, 0);
        actions.Controls.Add(ActionButton("완료 요청", UserTheme.Green, RequestCompletion), 2, 0);
        layout.Controls.Add(actions, 0, 2);
        return layout;
    }

    private Control BuildAiPanel()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _answer.Padding = new Padding(10);
        layout.Controls.Add(_answer, 0, 0);
        layout.Controls.Add(_question, 0, 1);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        _askButton.Margin = new Padding(0, 0, 4, 0);
        _adminInquiryButton.Margin = new Padding(4, 0, 0, 0);
        actions.Controls.Add(_askButton, 0, 0);
        actions.Controls.Add(_adminInquiryButton, 1, 0);
        layout.Controls.Add(actions, 0, 3);
        return layout;
    }

    private async Task LoadAllAsync(bool showError = true)
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var linesTask = _api.GetLinesAsync();
            var inspectionsTask = _api.GetInspectionsAsync(LineId);
            var issuesTask = _api.GetIssuesAsync();
            await Task.WhenAll(linesTask, inspectionsTask, issuesTask);

            var line = linesTask.Result.FirstOrDefault(x => x.Id == LineId) ?? linesTask.Result.FirstOrDefault();
            var inspections = inspectionsTask.Result;
            var issues = issuesTask.Result.Where(x => x.LineId == LineId).ToList();
            UpdateLineSummary(line);
            _inspections.DataSource = inspections;
            _issues.DataSource = issues;
            UpdateLatestInspection(inspections.FirstOrDefault());
            ShowSelectedIssue();
            _connectionLabel.Text = "● 서버 연결됨";
            _connectionLabel.ForeColor = UserTheme.Green;
            _lastUpdated.Text = $"마지막 갱신  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _connectionLabel.Text = "● 서버 연결 실패";
            _connectionLabel.ForeColor = UserTheme.Red;
            if (showError)
                MessageBox.Show(ex.Message, "통신 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateLineSummary(LineStatusDto? line)
    {
        if (line is null) return;
        SetBadge(_operationBadge, line.OperationStatus, line.OperationStatus.Contains("가동") ? UserTheme.Green : UserTheme.Red);
        SetBadge(_inspectionBadge, line.InspectionStatus,
            line.InspectionStatus.Contains("확인") ? UserTheme.Amber : UserTheme.Blue);
        _normalValue.Text = line.NormalCount.ToString("N0");
        _defectValue.Text = line.DefectCount.ToString("N0");
        _undeterminedValue.Text = line.UndeterminedCount.ToString("N0");
        var determined = line.NormalCount + line.DefectCount;
        _yieldValue.Text = determined == 0 ? "0.0%" : $"{line.NormalCount * 100.0 / determined:0.0}%";
    }

    private void UpdateLatestInspection(InspectionDto? item)
    {
        _preview.Result = item?.Result;
        _preview.Invalidate();
        if (item is null)
        {
            _latestResult.Text = "검사 대기";
            _latestResult.ForeColor = UserTheme.Muted;
            _latestReason.Text = "검사를 실행하면 판정 결과와 기준 점수가 표시됩니다.";
            SetScore(_shapeProgress, _shapeScore, 0);
            SetScore(_colorProgress, _colorScore, 0);
            return;
        }

        _latestResult.Text = item.Result switch
        {
            InspectionResultType.Normal => "● 정상",
            InspectionResultType.Defect => "● 불량",
            _ => "● 판정 불가"
        };
        _latestResult.ForeColor = ResultColor(item.Result);
        _latestReason.Text = $"{item.InspectedAt:HH:mm:ss}  |  {item.Reason}";
        SetScore(_shapeProgress, _shapeScore, item.ShapeScore);
        SetScore(_colorProgress, _colorScore, item.ColorScore);
    }

    private async void Inspect(double shape, double color, bool acquired)
    {
        try
        {
            _preview.IsInspecting = true;
            _preview.Invalidate();
            var result = await _api.SimulateInspectionAsync(new InspectionRequest(LineId, shape, color, acquired));
            UpdateLatestInspection(result);
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "검사 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _preview.IsInspecting = false;
            _preview.Invalidate();
        }
    }

    private async void CreateIssue(object? sender, EventArgs e)
    {
        using var dialog = new IssueReportDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            await _api.CreateIssueAsync(new CreateIssueRequest(LineId, dialog.IssueType, dialog.IssueTitle, dialog.Description));
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "문제 등록 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void StartAction(object? sender, EventArgs e) =>
        await ChangeSelectedIssue(IssueStatus.InProgress, "사용자가 문제를 확인하고 점검을 시작함");

    private async void RequestCompletion(object? sender, EventArgs e) =>
        await ChangeSelectedIssue(IssueStatus.CompletionRequested, "점검 및 조치 완료, 관리자 확인 요청");

    private async Task ChangeSelectedIssue(IssueStatus status, string note)
    {
        if (SelectedIssue() is not { } issue)
        {
            MessageBox.Show("처리할 문제를 목록에서 선택하세요.", "문제 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (issue.Status == IssueStatus.Closed)
        {
            MessageBox.Show("이미 관리자가 종결한 문제입니다.");
            return;
        }
        try
        {
            await _api.UpdateIssueAsync(issue.Id, new UpdateIssueStatusRequest(status, note));
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "상태 변경 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void AskAi(object? sender, EventArgs e)
    {
        if (SelectedIssue() is not { } issue)
        {
            MessageBox.Show("상담할 문제를 먼저 선택하세요.", "문제 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(_question.Text))
        {
            MessageBox.Show("질문을 입력하세요.", "상담 AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _question.Focus();
            return;
        }

        try
        {
            _askButton.Enabled = false;
            _askButton.Text = "매뉴얼 확인 중...";
            var response = await _api.ConsultAiAsync(new AiConsultRequest(issue.Id, _question.Text.Trim()));
            _answer.Text = $"[질문]\r\n{_question.Text.Trim()}\r\n\r\n[AI 점검 안내]\r\n{response.Answer}\r\n\r\n[근거]\r\n{response.ManualSource}";
            _answer.SelectionStart = 0;
            _answer.SelectionLength = 4;
            _answer.SelectionFont = new Font(_answer.Font, FontStyle.Bold);
            _answer.SelectionColor = UserTheme.Blue;
            _adminInquiryButton.BackColor = response.NeedAdmin ? UserTheme.Amber : Color.FromArgb(230, 235, 242);
            _adminInquiryButton.ForeColor = response.NeedAdmin ? Color.White : UserTheme.Text;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AI 상담 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _askButton.Enabled = true;
            _askButton.Text = "AI에게 점검 방법 묻기";
        }
    }

    private async void RegisterAdminInquiry(object? sender, EventArgs e)
    {
        var selected = SelectedIssue();
        if (selected is null)
        {
            MessageBox.Show("관리자에게 전달할 문제를 먼저 선택하세요.");
            return;
        }
        try
        {
            var description = $"관련 문제: {selected.Title}\n사용자 질문: {_question.Text.Trim()}\n상담 내용: {_answer.Text}";
            await _api.CreateIssueAsync(new CreateIssueRequest(LineId, IssueType.ProductionLine,
                $"관리자 확인 요청 · {selected.Title}", description));
            await LoadAllAsync();
            MessageBox.Show("관리자 확인 목록에 문의를 등록했습니다.", "등록 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "문의 등록 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowSelectedIssue()
    {
        var issue = SelectedIssue();
        _selectedIssue.Text = issue is null
            ? "문제를 선택하세요."
            : $"{StatusText(issue.Status)}  |  {issue.Title}\r\n{issue.Description}";
    }

    private IssueDto? SelectedIssue() => _issues.CurrentRow?.DataBoundItem as IssueDto;

    private static Panel Card(string title, Control content, string? caption = null)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = UserTheme.Surface, Margin = Padding.Empty };
        var header = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(14, 0, 14, 0) };
        header.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Left,
            Width = 260,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("맑은 고딕", 11, FontStyle.Bold),
            ForeColor = UserTheme.Text
        });
        if (!string.IsNullOrEmpty(caption))
        {
            header.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Right,
                Width = 230,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UserTheme.Muted,
                Font = new Font("맑은 고딕", 8.5f)
            });
        }
        content.Dock = DockStyle.Fill;
        card.Controls.Add(content);
        card.Controls.Add(header);
        return card;
    }

    private static Panel MetricCard(string title, Label value, Color accent)
    {
        var outer = new Panel { Dock = DockStyle.Fill, BackColor = UserTheme.Canvas, Padding = new Padding(0, 0, 8, 0) };
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 10, 12, 8) };
        card.Controls.Add(value);
        card.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = UserTheme.Muted,
            Font = new Font("맑은 고딕", 9, FontStyle.Bold)
        });
        card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent });
        outer.Controls.Add(card);
        return outer;
    }

    private static Label MetricValue(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.BottomRight,
        ForeColor = UserTheme.Text,
        Font = new Font("맑은 고딕", 20, FontStyle.Bold)
    };

    private static Label Badge(string text, Color color) => new()
    {
        Text = text,
        AutoSize = false,
        Height = 28,
        Width = 92,
        BackColor = color,
        ForeColor = Color.White,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
        Margin = Padding.Empty
    };

    private static void SetBadge(Label badge, string text, Color color)
    {
        badge.Text = text;
        badge.BackColor = color;
    }

    private static Button ActionButton(string text, Color color, EventHandler handler)
    {
        var button = UserLoginForm.StyledButton(text, color, Color.White);
        button.Margin = new Padding(3, 0, 3, 0);
        button.Click += handler;
        return button;
    }

    private static Control ScoreRow(string title, ProgressBar bar, Label score)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 2, Padding = new Padding(0, 4, 0, 4) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        row.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 22, ForeColor = UserTheme.Muted }, 0, 0);
        row.Controls.Add(score, 1, 0);
        row.Controls.Add(bar, 0, 1);
        row.SetColumnSpan(bar, 2);
        return row;
    }

    private static ProgressBar ScoreBar() => new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Height = 13 };
    private static Label ScoreLabel() => new() { Text = "0%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight, ForeColor = UserTheme.Text };

    private static void SetScore(ProgressBar bar, Label label, double score)
    {
        var value = Math.Clamp((int)Math.Round(score * 100), 0, 100);
        bar.Value = value;
        label.Text = $"{value}%";
    }

    private static DataGridView InspectionGrid()
    {
        var grid = BaseGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시간", DataPropertyName = nameof(InspectionDto.InspectedAt), FillWeight = 22 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "판정", DataPropertyName = nameof(InspectionDto.Result), FillWeight = 18 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "형태·윤곽", DataPropertyName = nameof(InspectionDto.ShapeScore), FillWeight = 18 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "색상", DataPropertyName = nameof(InspectionDto.ColorScore), FillWeight = 18 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "판정 사유", DataPropertyName = nameof(InspectionDto.Reason), FillWeight = 58 });
        return grid;
    }

    private static DataGridView IssueGrid()
    {
        var grid = BaseGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "발생 시각", DataPropertyName = nameof(IssueDto.CreatedAt), FillWeight = 34 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "문제", DataPropertyName = nameof(IssueDto.Title), FillWeight = 52 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "상태", DataPropertyName = nameof(IssueDto.Status), FillWeight = 30 });
        return grid;
    }

    private static DataGridView BaseGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            GridColor = UserTheme.Border,
            RowTemplate = { Height = 34 },
            EnableHeadersVisualStyles = false
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(239, 243, 248);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = UserTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9, FontStyle.Bold);
        grid.ColumnHeadersHeight = 36;
        grid.DefaultCellStyle.SelectionBackColor = UserTheme.PaleBlue;
        grid.DefaultCellStyle.SelectionForeColor = UserTheme.Text;
        return grid;
    }

    private static void InspectionCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0) return;
        var property = grid.Columns[e.ColumnIndex].DataPropertyName;
        if (property == nameof(InspectionDto.InspectedAt) && e.Value is DateTimeOffset time)
        {
            e.Value = time.ToString("MM-dd HH:mm:ss");
            e.FormattingApplied = true;
        }
        else if (property == nameof(InspectionDto.Result) && e.Value is InspectionResultType result)
        {
            e.Value = ResultText(result);
            e.CellStyle!.ForeColor = ResultColor(result);
            e.CellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            e.FormattingApplied = true;
        }
        else if ((property == nameof(InspectionDto.ShapeScore) || property == nameof(InspectionDto.ColorScore)) && e.Value is double score)
        {
            e.Value = score.ToString("P0");
            e.FormattingApplied = true;
        }
    }

    private static void IssueCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0) return;
        var property = grid.Columns[e.ColumnIndex].DataPropertyName;
        if (property == nameof(IssueDto.CreatedAt) && e.Value is DateTimeOffset time)
        {
            e.Value = time.ToString("MM-dd HH:mm");
            e.FormattingApplied = true;
        }
        else if (property == nameof(IssueDto.Status) && e.Value is IssueStatus status)
        {
            e.Value = StatusText(status);
            e.CellStyle!.ForeColor = status == IssueStatus.Closed ? UserTheme.Green : UserTheme.Amber;
            e.FormattingApplied = true;
        }
    }

    private static string ResultText(InspectionResultType result) => result switch
    {
        InspectionResultType.Normal => "정상",
        InspectionResultType.Defect => "불량",
        _ => "판정 불가"
    };

    private static Color ResultColor(InspectionResultType result) => result switch
    {
        InspectionResultType.Normal => UserTheme.Green,
        InspectionResultType.Defect => UserTheme.Red,
        _ => UserTheme.Amber
    };

    private static string StatusText(IssueStatus status) => status switch
    {
        IssueStatus.Occurred => "발생",
        IssueStatus.Acknowledged => "확인",
        IssueStatus.InProgress => "조치 중",
        IssueStatus.CompletionRequested => "완료 요청",
        IssueStatus.Closed => "종결",
        _ => status.ToString()
    };
}

internal sealed class InspectionPreview : Control
{
    public InspectionResultType? Result { get; set; }
    public bool IsInspecting { get; set; }

    public InspectionPreview()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(14, 20, 28);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var area = ClientRectangle;
        using var gridPen = new Pen(Color.FromArgb(31, 45, 59), 1);
        for (var x = 0; x < area.Width; x += 42) g.DrawLine(gridPen, x, 0, x, area.Height);
        for (var y = 0; y < area.Height; y += 42) g.DrawLine(gridPen, 0, y, area.Width, y);

        var conveyor = new Rectangle(28, Math.Max(40, area.Height - 74), Math.Max(80, area.Width - 56), 36);
        using var conveyorBrush = new SolidBrush(Color.FromArgb(52, 63, 76));
        g.FillRectangle(conveyorBrush, conveyor);
        using var rollerPen = new Pen(Color.FromArgb(114, 128, 143), 2);
        for (var x = conveyor.Left + 18; x < conveyor.Right; x += 38)
            g.DrawEllipse(rollerPen, x, conveyor.Top + 8, 20, 20);

        var size = Math.Min(150, Math.Max(90, area.Height - 115));
        var product = new Rectangle((area.Width - size) / 2, Math.Max(20, (area.Height - size) / 2 - 10), size, size);
        var color = Result switch
        {
            InspectionResultType.Normal => UserTheme.Green,
            InspectionResultType.Defect => UserTheme.Red,
            InspectionResultType.Undetermined => UserTheme.Amber,
            _ => Color.FromArgb(66, 130, 194)
        };
        using var productBrush = new SolidBrush(Color.FromArgb(55, color));
        using var productPen = new Pen(color, 3);
        g.FillRoundedRectangle(productBrush, product, 18);
        g.DrawRoundedRectangle(productPen, product, 18);
        using var crossPen = new Pen(Color.FromArgb(140, color), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        g.DrawLine(crossPen, product.Left - 24, product.Top, product.Right + 24, product.Top);
        g.DrawLine(crossPen, product.Left - 24, product.Bottom, product.Right + 24, product.Bottom);
        g.DrawLine(crossPen, product.Left, product.Top - 18, product.Left, product.Bottom + 18);
        g.DrawLine(crossPen, product.Right, product.Top - 18, product.Right, product.Bottom + 18);

        var status = IsInspecting ? "IMAGE PROCESSING..." : "LIVE  ●";
        using var statusFont = new Font("Consolas", 9, FontStyle.Bold);
        using var statusBrush = new SolidBrush(IsInspecting ? UserTheme.Amber : Color.FromArgb(92, 219, 158));
        g.DrawString(status, statusFont, statusBrush, 12, 10);
        using var guideBrush = new SolidBrush(Color.FromArgb(168, 182, 198));
        g.DrawString("형태·윤곽 / 색상 검사 영역", Font, guideBrush, 12, area.Height - 25);
    }
}

internal sealed class IssueReportDialog : Form
{
    private readonly ComboBox _type = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _title = new() { Dock = DockStyle.Fill, PlaceholderText = "예: 카메라 영상이 표시되지 않음" };
    private readonly TextBox _description = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        PlaceholderText = "발생 시점, 증상, 확인한 내용을 입력하세요."
    };

    public IssueType IssueType => _type.SelectedIndex == 1 ? IssueType.InspectionSystem : IssueType.ProductionLine;
    public string IssueTitle => _title.Text.Trim();
    public string Description => _description.Text.Trim();

    public IssueReportDialog()
    {
        Text = "라인 문제 등록";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 390);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new Font("맑은 고딕", 9.5f);
        BackColor = UserTheme.Canvas;
        Padding = new Padding(20);
        _type.Items.AddRange(["설비·라인 문제", "검사 시스템 문제"]);
        _type.SelectedIndex = 0;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(DialogLabel("문제 구분"), 0, 0);
        layout.Controls.Add(_type, 0, 1);
        layout.Controls.Add(DialogLabel("문제 제목"), 0, 2);
        layout.Controls.Add(_title, 0, 3);
        layout.Controls.Add(DialogLabel("상세 내용"), 0, 4);
        layout.Controls.Add(_description, 0, 5);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(220, 8, 0, 0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var cancel = UserLoginForm.StyledButton("취소", Color.FromArgb(226, 231, 238), UserTheme.Text);
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Margin = new Padding(0, 0, 5, 0);
        var submit = UserLoginForm.StyledButton("등록", UserTheme.Blue, Color.White);
        submit.Margin = new Padding(5, 0, 0, 0);
        submit.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_title.Text))
            {
                MessageBox.Show("문제 제목을 입력하세요.");
                return;
            }
            DialogResult = DialogResult.OK;
        };
        actions.Controls.Add(cancel, 0, 0);
        actions.Controls.Add(submit, 1, 0);
        layout.Controls.Add(actions, 0, 6);
        Controls.Add(layout);
        AcceptButton = submit;
        CancelButton = cancel;
    }

    private static Label DialogLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.BottomLeft,
        ForeColor = UserTheme.Text,
        Font = new Font("맑은 고딕", 9, FontStyle.Bold)
    };
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
