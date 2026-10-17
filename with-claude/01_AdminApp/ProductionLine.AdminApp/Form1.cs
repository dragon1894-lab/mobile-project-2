using ProductionLine.Shared;

namespace ProductionLine.AdminApp;

public sealed class AdminLoginForm : Form
{
    private readonly TextBox _id = new() { Text = "admin", Width = 220 };
    private readonly TextBox _password = new() { Text = "admin123!", Width = 220, UseSystemPasswordChar = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.Firebrick };

    public AdminLoginForm()
    {
        Text = "생산라인 관리자 로그인";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(430, 300);
        Font = new Font("맑은 고딕", 10);

        var title = new Label { Text = "관리자 시스템", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true };
        var server = new Label { Text = "유선 이더넷 REST 서버: " + ServerUrl, AutoSize = true, ForeColor = Color.DimGray };
        var login = new Button { Text = "관리자 로그인", Width = 220, Height = 38 };
        login.Click += LoginClicked;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(90, 35, 0, 0)
        };
        panel.Controls.AddRange([title, server, Gap(10), new Label { Text = "관리자 ID", AutoSize = true }, _id,
            new Label { Text = "비밀번호", AutoSize = true }, _password, Gap(5), login, _status]);
        Controls.Add(panel);
        AcceptButton = login;
    }

    private async void LoginClicked(object? sender, EventArgs e)
    {
        try
        {
            _status.Text = "서버에 연결 중...";
            var api = new ProductionLineApiClient();
            var user = await api.LoginAsync(_id.Text.Trim(), _password.Text);
            if (user.Role != UserRole.Admin) throw new InvalidOperationException("관리자 계정만 접근할 수 있습니다.");
            Hide();
            using var main = new AdminMainForm(api);
            main.ShowDialog(this);
            Close();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private static Control Gap(int height) => new Panel { Height = height, Width = 1 };
    private static string ServerUrl => Environment.GetEnvironmentVariable("PRODUCTIONLINE_SERVER_URL") ?? "http://127.0.0.1:5050";
}

public sealed class AdminMainForm : Form
{
    private readonly ProductionLineApiClient _api;
    private readonly DataGridView _lines = Grid();
    private readonly DataGridView _users = Grid();
    private readonly DataGridView _issues = Grid();
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray };

    public AdminMainForm(ProductionLineApiClient api)
    {
        _api = api;
        Text = "생산라인 관리자 대시보드";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1050, 680);
        Font = new Font("맑은 고딕", 9.5f);

        var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(17, 48, 73) };
        header.Controls.Add(new Label
        {
            Text = "  생산라인 통합 관리자", Dock = DockStyle.Left, Width = 400,
            ForeColor = Color.White, Font = new Font(Font.FontFamily, 18, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft
        });
        header.Controls.Add(new Label
        {
            Text = $"관리자: {_api.CurrentUser?.Name}  |  서버: {_api.BaseAddress}  ", Dock = DockStyle.Right,
            Width = 430, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleRight
        });

        var refresh = new Button { Text = "전체 새로고침", AutoSize = true };
        refresh.Click += async (_, _) => await LoadAllAsync();
        var addUser = new Button { Text = "테스트 사용자 추가", AutoSize = true };
        addUser.Click += AddUserClicked;
        var closeIssue = new Button { Text = "선택 문제 종결", AutoSize = true };
        closeIssue.Click += CloseIssueClicked;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 0, 0) };
        toolbar.Controls.AddRange([refresh, addUser, closeIssue, _status]);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(MakePage("전체 라인 현황", _lines));
        tabs.TabPages.Add(MakePage("사용자 관리", _users));
        tabs.TabPages.Add(MakePage("문제 확인·종결", _issues));

        Controls.Add(tabs);
        Controls.Add(toolbar);
        Controls.Add(header);
        Shown += async (_, _) => await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        try
        {
            _status.Text = "데이터 갱신 중...";
            _lines.DataSource = await _api.GetLinesAsync();
            _users.DataSource = await _api.GetUsersAsync();
            _issues.DataSource = await _api.GetIssuesAsync();
            _status.Text = $"마지막 갱신: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "통신 오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async void AddUserClicked(object? sender, EventArgs e)
    {
        var id = "worker" + DateTime.Now.ToString("HHmmss");
        try
        {
            await _api.CreateUserAsync(new CreateUserRequest(id, "신규 작업자", "worker123!", [1]));
            await LoadAllAsync();
            MessageBox.Show($"{id} / worker123! 계정을 만들었습니다.\n사용자 담당자가 편집 화면으로 확장할 수 있는 기본 동작입니다.");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private async void CloseIssueClicked(object? sender, EventArgs e)
    {
        if (_issues.CurrentRow?.DataBoundItem is not IssueDto issue)
        {
            MessageBox.Show("종결할 문제를 선택하세요.");
            return;
        }
        try
        {
            await _api.UpdateIssueAsync(issue.Id, new UpdateIssueStatusRequest(IssueStatus.Closed, "관리자 확인 후 종결"));
            await LoadAllAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, BackgroundColor = Color.White
    };

    private static TabPage MakePage(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        page.Controls.Add(content);
        return page;
    }
}
