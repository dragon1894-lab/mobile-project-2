using System.Drawing.Drawing2D;
using ProductionLine.Shared;

namespace ProductionLine.UserApp;

public sealed partial class UserMainForm
{
    private static readonly Color UiInk = Color.FromArgb(23, 39, 58);
    private static readonly Color UiMuted = Color.FromArgb(100, 116, 139);
    private static readonly Color UiAccent = Color.FromArgb(0, 119, 117);
    private static readonly Color UiCanvas = Color.FromArgb(242, 245, 249);
    private readonly List<Button> _navigation = new();
    private readonly Label _pageTitle = new() { Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 22, FontStyle.Bold), ForeColor = UiInk };
    private readonly Label _pageSubtitle = new() { Dock = DockStyle.Fill, ForeColor = UiMuted };
    private readonly Label[] _metricValues = Enumerable.Range(0, 4).Select(_ => new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 23, FontStyle.Bold), ForeColor = UiInk }).ToArray();
    private readonly string[] _titles = { "실시간 검사", "검사 현황과 이력", "문제 및 조치", "AI 어시스턴트", "관리자 상담", "단체 채팅" };
    private readonly string[] _subtitles = { "카메라를 연결하고 제품의 상태를 확인하세요.", "라인 누적 현황과 개별 검사 기록을 한눈에 확인하세요.", "문제를 확인하고 처리한 내용을 기록하세요.", "선택한 문제에 맞는 점검 방법과 근거를 확인하세요.", "해결되지 않은 문제를 관리자에게 전달하세요.", "관리자와 모든 작업자가 함께 보는 대화방입니다." };

    private Control BuildModernShell()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 206));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(17, 34, 52), Padding = new Padding(18, 28, 18, 20) };
        var brand = new Panel { Dock = DockStyle.Top, Height = 108 };
        brand.Controls.Add(new Label { Text = "생산라인 운영 시스템", Dock = DockStyle.Top, Height = 30, ForeColor = Color.FromArgb(171, 190, 209), Font = new Font("맑은 고딕", 9) });
        brand.Controls.Add(new Label { Text = "LINE / OPS", Dock = DockStyle.Top, Height = 40, ForeColor = Color.White, Font = new Font("맑은 고딕", 18, FontStyle.Bold) });
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 400, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 14, 0, 0) };
        var names = new[] { "01   실시간 검사", "02   검사 현황 · 이력", "03   문제 · 조치", "04   AI 상담", "05   관리자 상담", "06   단체 채팅" };
        for (int i = 0; i < names.Length; i++)
        {
            var index = i;
            var button = new Button { Text = names[i], Width = 170, Height = 49, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(171, 190, 209), BackColor = sidebar.BackColor, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(10, 0, 0, 0), Cursor = Cursors.Hand, Tag = "navigation" };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => _pages.SelectedIndex = index;
            _navigation.Add(button); nav.Controls.Add(button);
        }
        var account = new Panel { Dock = DockStyle.Bottom, Height = 125 };
        account.Controls.Add(new Label { Text = $"{_api.CurrentUser?.Name}\r\n작업자 워크스페이스", Dock = DockStyle.Top, Height = 63, ForeColor = Color.FromArgb(193, 209, 223) });
        var logout = Button("로그아웃", () => { LogoutRequested = true; Close(); });
        logout.Dock = DockStyle.Bottom; logout.Tag = "navigation";
        logout.FlatStyle = FlatStyle.Flat; logout.FlatAppearance.BorderSize = 0;
        logout.BackColor = Color.FromArgb(34, 53, 72); logout.ForeColor = Color.White;
        account.Controls.Add(logout);
        sidebar.Controls.Add(nav); sidebar.Controls.Add(brand); sidebar.Controls.Add(account);
        shell.Controls.Add(sidebar, 0, 0);

        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(22, 16, 22, 10), BackColor = UiCanvas };
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 222));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        toolbar.Controls.Add(new Label { Text = "WORKSPACE   /   작업자", Dock = DockStyle.Fill, ForeColor = UiMuted, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _lines.Dock = DockStyle.Fill; _lines.Margin = new Padding(4, 9, 12, 9);
        toolbar.Controls.Add(_lines, 1, 0);
        var refresh = Button("새로고침", async () => await RefreshAsync()); refresh.AutoSize = false; refresh.Dock = DockStyle.Fill; refresh.Margin = new Padding(0, 4, 0, 8);
        toolbar.Controls.Add(refresh, 2, 0);
        workspace.Controls.Add(toolbar, 0, 0);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(6, 8, 0, 0) };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 49)); heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(_pageTitle, 0, 0); heading.Controls.Add(_pageSubtitle, 0, 1);
        workspace.Controls.Add(heading, 0, 1);
        workspace.Controls.Add(_pages, 0, 2);
        _connection.ForeColor = UiMuted; _connection.Font = new Font("맑은 고딕", 9);
        workspace.Controls.Add(_connection, 0, 3);
        shell.Controls.Add(workspace, 1, 0);
        _pages.SelectedIndexChanged += (_, _) => UpdateNavigation();
        UpdateNavigation();
        return shell;
    }

    private void UpdateNavigation()
    {
        var index = Math.Max(0, _pages.SelectedIndex);
        _pageTitle.Text = _titles[index]; _pageSubtitle.Text = _subtitles[index];
        for (int i = 0; i < _navigation.Count; i++)
        {
            _navigation[i].BackColor = i == index ? UiAccent : Color.FromArgb(17, 34, 52);
            _navigation[i].ForeColor = i == index ? Color.White : Color.FromArgb(171, 190, 209);
        }
    }

    private Control Surface(string? title, Control content)
    {
        var card = new RoundedSurface { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12), Margin = new Padding(0, 0, 0, 10) };
        content.Dock = DockStyle.Fill;
        card.Controls.Add(content);
        if (title != null)
            card.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 28, ForeColor = UiMuted, Font = new Font("맑은 고딕", 9, FontStyle.Bold) });
        return card;
    }

    private Control BuildMetricCards()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        var labels = new[] { "정상", "불량", "판정 불가", "정상률" };
        var accents = new[] { UiAccent, Color.FromArgb(205, 68, 79), Color.FromArgb(184, 125, 31), UiInk };
        for (int i = 0; i < 4; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _metricValues[i].ForeColor = accents[i];
            var card = Surface(labels[i], _metricValues[i]);
            card.Margin = new Padding(i == 0 ? 0 : 5, 0, i == 3 ? 0 : 5, 12);
            row.Controls.Add(card, i, 0);
        }
        return row;
    }

    private void UpdateMetricCards(LineStatusDto line)
    {
        _metricValues[0].Text = line.NormalCount.ToString("N0");
        _metricValues[1].Text = line.DefectCount.ToString("N0");
        _metricValues[2].Text = line.UndeterminedCount.ToString("N0");
        var total = line.NormalCount + line.DefectCount;
        _metricValues[3].Text = $"{(total == 0 ? 0 : line.NormalCount * 100.0 / total):0.0}%";
    }

    private static void ApplyModernControls(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is TabPage page) { page.BackColor = UiCanvas; page.Padding = Padding.Empty; }
            if (control is Button button && !Equals(button.Tag, "navigation"))
            {
                bool primary = button.Text.Contains("시작") || button.Text.Contains("보내기") || button.Text == "카메라 연결" || button.Text == "완료 요청";
                button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = primary ? 0 : 1;
                button.FlatAppearance.BorderColor = Color.FromArgb(215, 224, 233);
                button.BackColor = primary ? UiAccent : Color.White;
                button.ForeColor = primary ? Color.White : UiInk;
                button.Font = new Font("맑은 고딕", 9, FontStyle.Bold);
                button.Cursor = Cursors.Hand;
                button.MinimumSize = new Size(0, 34);
                button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 99, 98) : Color.FromArgb(231, 239, 245);
                void UpdateButtonPalette()
                {
                    button.BackColor = !button.Enabled ? Color.FromArgb(231, 236, 242) : primary ? UiAccent : Color.White;
                    button.ForeColor = !button.Enabled ? UiMuted : primary ? Color.White : UiInk;
                }
                button.EnabledChanged += (_, _) => UpdateButtonPalette();
                UpdateButtonPalette();
            }
            if (control is TextBox text) { text.BorderStyle = BorderStyle.FixedSingle; text.BackColor = text.ReadOnly ? Color.FromArgb(248, 250, 252) : Color.White; text.ForeColor = UiInk; }
            if (control is RichTextBox rich) { rich.BorderStyle = BorderStyle.None; rich.BackColor = Color.White; rich.ForeColor = UiInk; }
            if (control is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
            if (control is DataGridView grid)
            {
                grid.BorderStyle = BorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                grid.GridColor = Color.FromArgb(233, 238, 244); grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                grid.ColumnHeadersHeight = 42; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(246, 248, 251), ForeColor = UiMuted, Font = new Font("맑은 고딕", 9, FontStyle.Bold), Padding = new Padding(8, 0, 0, 0) };
                grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = UiInk, SelectionBackColor = Color.FromArgb(224, 243, 240), SelectionForeColor = UiInk, Padding = new Padding(8, 0, 0, 0), Font = new Font("맑은 고딕", 9.5f) };
                grid.RowTemplate.Height = 40; grid.AllowUserToResizeRows = false;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 254);
            }
            ApplyModernControls(control);
        }
    }
}

internal sealed class PageHost : TabControl
{
    public PageHost() { Appearance = TabAppearance.FlatButtons; ItemSize = new Size(0, 1); SizeMode = TabSizeMode.Fixed; TabStop = false; }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x1328 && !DesignMode) { m.Result = new IntPtr(1); return; }
        base.WndProc(ref m);
    }
}

internal sealed class RoundedSurface : Panel
{
    public RoundedSurface() { DoubleBuffered = true; BackColor = Color.White; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(228, 234, 241));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

internal sealed class CameraCanvas : PictureBox
{
    protected override void OnPaint(PaintEventArgs pe)
    {
        base.OnPaint(pe);
        if (Image != null) return;
        var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(36, 58, 79));
        for (int x = 0; x < Width; x += 56) g.DrawLine(pen, x, 0, x, Height);
        for (int y = 0; y < Height; y += 56) g.DrawLine(pen, 0, y, Width, y);
        using var title = new Font("맑은 고딕", 17, FontStyle.Bold);
        using var subtitle = new Font("맑은 고딕", 10);
        TextRenderer.DrawText(g, "카메라 연결 대기", title, new Rectangle(0, Height / 2 - 35, Width, 40), Color.FromArgb(204, 218, 230), TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, "아래 카메라 연결 버튼으로 실시간 영상을 시작하세요", subtitle, new Rectangle(0, Height / 2 + 12, Width, 30), Color.FromArgb(139, 163, 185), TextFormatFlags.HorizontalCenter);
    }
}
