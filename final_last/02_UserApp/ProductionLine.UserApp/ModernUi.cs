using System.Drawing.Drawing2D;
using ProductionLine.Shared;

namespace ProductionLine.UserApp;

public sealed partial class UserMainForm
{
    private static readonly Color UiInk = Color.FromArgb(25, 42, 61);
    private static readonly Color UiMuted = Color.FromArgb(107, 122, 140);
    private static readonly Color UiAccent = Color.FromArgb(0, 128, 122);
    private static readonly Color UiCanvas = Color.FromArgb(243, 246, 250);
    private readonly List<Button> _navigation = new();
    private readonly Label _lineName = new() { Dock = DockStyle.Fill, Text = "라인 정보 확인 중", TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, Font = new Font("맑은 고딕", 10, FontStyle.Bold), ForeColor = UiInk };
    private readonly Label _pageTitle = new() { Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 24, FontStyle.Bold), ForeColor = UiInk };
    private readonly Label _pageSubtitle = new() { Dock = DockStyle.Fill, ForeColor = UiMuted, Font = new Font("맑은 고딕", 9.5f) };
    private readonly Label[] _metricValues = Enumerable.Range(0, 4).Select(_ => new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 23, FontStyle.Bold), ForeColor = UiInk }).ToArray();
    private readonly string[] _titles = { "실시간 검사", "검사 현황과 이력", "문제 및 조치", "AI 상담", "단체 채팅" };
    private readonly string[] _subtitles = { "카메라 영상과 최근 검사 결과를 확인하세요.", "누적 검사 현황과 제품별 기록을 확인하세요.", "문제를 선택하고 점검 및 처리 내용을 남기세요.", "문제 등록 없이 점검 방법과 궁금한 내용을 바로 질문하세요.", "관리자와 작업자가 함께 소식을 공유하는 공간입니다." };

    private Control BuildModernShell()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = UiCanvas };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(17, 31, 48), Padding = new Padding(16, 26, 16, 18) };
        var brand = new Panel { Dock = DockStyle.Top, Height = 118 };
        brand.Controls.Add(new Label { Text = "생산라인 운영 시스템", Dock = DockStyle.Top, Height = 28, ForeColor = Color.FromArgb(150, 174, 196), Font = new Font("맑은 고딕", 8.5f) });
        brand.Controls.Add(new Label { Text = "LINE / OPS", Dock = DockStyle.Top, Height = 40, ForeColor = Color.White, Font = new Font("맑은 고딕", 18, FontStyle.Bold) });
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 310, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var names = new[] { "01   실시간 검사", "02   검사 현황 · 이력", "03   문제 · 조치", "04   AI 상담", "05   단체 채팅" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var button = new Button { Text = names[i], Width = 160, Height = 46, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, Font = new Font("맑은 고딕", 9.5f), ForeColor = Color.FromArgb(163, 184, 205), BackColor = sidebar.BackColor, Margin = new Padding(0, 0, 0, 9), Padding = new Padding(10, 0, 0, 0), Cursor = Cursors.Hand, Tag = "navigation" };
            button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => _pages.SelectedIndex = index;
            _navigation.Add(button); nav.Controls.Add(button);
        }
        var account = new Panel { Dock = DockStyle.Bottom, Height = 113 };
        account.Controls.Add(new Label { Text = $"{_api.CurrentUser?.Name}\r\n작업자 워크스페이스", Dock = DockStyle.Top, Height = 56, ForeColor = Color.FromArgb(165, 188, 208), Font = new Font("맑은 고딕", 9) });
        var logout = Button("로그아웃", () => { LogoutRequested = true; Close(); });
        logout.Dock = DockStyle.Bottom; logout.Height = 38; logout.Tag = "navigation"; logout.FlatStyle = FlatStyle.Flat; logout.FlatAppearance.BorderSize = 0;
        logout.BackColor = Color.FromArgb(32, 49, 68); logout.ForeColor = Color.White;
        account.Controls.Add(logout); sidebar.Controls.Add(nav); sidebar.Controls.Add(brand); sidebar.Controls.Add(account); shell.Controls.Add(sidebar, 0, 0);

        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(24, 16, 24, 10), BackColor = UiCanvas };
        foreach (var height in new[] { 44, 90 }) workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252));
        toolbar.Controls.Add(new Label { Text = "LINE OPERATIONS   /   작업자", Dock = DockStyle.Fill, ForeColor = UiMuted, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 9) }, 0, 0);
        var lineCard = new RoundedSurface { Dock = DockStyle.Fill, Padding = new Padding(10, 2, 10, 2), Margin = new Padding(0, 0, 0, 6) };
        lineCard.Controls.Add(_lineName); toolbar.Controls.Add(lineCard, 1, 0); workspace.Controls.Add(toolbar, 0, 0);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(2, 8, 0, 6) };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(_pageTitle, 0, 0); heading.Controls.Add(_pageSubtitle, 0, 1); workspace.Controls.Add(heading, 0, 1);
        workspace.Controls.Add(_pages, 0, 2); _connection.ForeColor = UiMuted; _connection.Font = new Font("맑은 고딕", 8.5f); _connection.Padding = new Padding(4, 0, 0, 0);
        workspace.Controls.Add(_connection, 0, 3); shell.Controls.Add(workspace, 1, 0);
        _pages.SelectedIndexChanged += (_, _) => UpdateNavigation(); UpdateNavigation(); return shell;
    }

    private void UpdateNavigation()
    {
        int index = Math.Clamp(_pages.SelectedIndex, 0, _titles.Length - 1);
        _pageTitle.Text = _titles[index]; _pageSubtitle.Text = _subtitles[index];
        for (int i = 0; i < _navigation.Count; i++)
        {
            _navigation[i].BackColor = i == index ? UiAccent : Color.FromArgb(17, 31, 48);
            _navigation[i].ForeColor = i == index ? Color.White : Color.FromArgb(163, 184, 205);
        }
    }

    private Control Surface(string? title, Control content)
    {
        var card = new RoundedSurface { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 14, 12), Margin = new Padding(0, 0, 0, 10) };
        content.Dock = DockStyle.Fill; card.Controls.Add(content);
        if (title != null) card.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 28, ForeColor = UiInk, Font = new Font("맑은 고딕", 10, FontStyle.Bold) });
        return card;
    }

    private Control BuildMetricCards()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var labels = new[] { "정상", "불량", "판정 불가", "정상률" };
        var accents = new[] { UiAccent, Color.FromArgb(218, 77, 90), Color.FromArgb(181, 132, 48), UiInk };
        for (int i = 0; i < 4; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); _metricValues[i].ForeColor = accents[i];
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(new Label { Text = labels[i], Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = UiInk, Font = new Font("맑은 고딕", 10, FontStyle.Bold) }, 0, 0);
            _metricValues[i].Margin = Padding.Empty;
            content.Controls.Add(_metricValues[i], 0, 1);
            var card = Surface(null, content); card.Margin = new Padding(i == 0 ? 0 : 5, 0, i == 3 ? 0 : 5, 10); row.Controls.Add(card, i, 0);
        }
        return row;
    }

    private void UpdateMetricCards(LineStatusDto line)
    {
        _metricValues[0].Text = line.NormalCount.ToString("N0"); _metricValues[1].Text = line.DefectCount.ToString("N0"); _metricValues[2].Text = line.UndeterminedCount.ToString("N0");
        int total = line.NormalCount + line.DefectCount; _metricValues[3].Text = $"{(total == 0 ? 0 : line.NormalCount * 100.0 / total):0.0}%";
    }

    private static void ApplyModernControls(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is TabPage page) { page.BackColor = UiCanvas; page.Padding = Padding.Empty; }
            if (control is Button button)
            {
                void Shape() { if (button.Width < 2 || button.Height < 2) return; using var path = UiGeometry.Round(new Rectangle(0, 0, button.Width, button.Height), 7); var previous = button.Region; button.Region = new Region(path); previous?.Dispose(); }
                button.SizeChanged += (_, _) => Shape(); Shape();
                if (!Equals(button.Tag, "navigation"))
                {
                    bool primary = button.Text.Contains("시작") || button.Text.Contains("보내기") || button.Text.Contains("전송") || button.Text is "카메라 연결" or "완료 요청" or "새 문제 등록";
                    button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = primary ? 0 : 1; button.FlatAppearance.BorderColor = Color.FromArgb(216, 225, 233);
                    button.Font = new Font("맑은 고딕", 9, FontStyle.Bold); button.Cursor = Cursors.Hand; button.MinimumSize = new Size(0, 32);
                    button.Margin = new Padding(0, 0, 8, 0);
                    // Let WinForms include the font, padding and border in its preferred size.
                    // A text-only width clipped longer captions after the theme added padding.
                    button.Padding = new Padding(10, 0, 10, 0);
                    if (button.Dock == DockStyle.None)
                    {
                        button.MinimumSize = new Size(86, 34);
                        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                        button.AutoSize = true;
                        button.Size = button.GetPreferredSize(Size.Empty);
                    }
                    void Palette() { button.BackColor = !button.Enabled ? Color.FromArgb(232, 237, 242) : primary ? UiAccent : Color.White; button.ForeColor = !button.Enabled ? UiMuted : primary ? Color.White : UiInk; }
                    button.EnabledChanged += (_, _) => Palette(); Palette();
                    button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 111, 106) : Color.FromArgb(233, 243, 244);
                }
            }
            if (control is TextBox text) { text.BorderStyle = BorderStyle.FixedSingle; text.BackColor = text.ReadOnly ? Color.FromArgb(248, 250, 252) : Color.White; text.ForeColor = UiInk; }
            if (control is RichTextBox rich) { rich.BorderStyle = BorderStyle.None; rich.BackColor = Color.White; rich.ForeColor = UiInk; }
            if (control is ComboBox combo) { combo.FlatStyle = FlatStyle.Flat; combo.Font = new Font("맑은 고딕", 9); }
            if (control is DataGridView grid)
            {
                grid.BorderStyle = BorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; grid.GridColor = Color.FromArgb(235, 239, 244); grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; grid.ColumnHeadersHeight = 40; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(246, 248, 251), ForeColor = UiMuted, Font = new Font("맑은 고딕", 9, FontStyle.Bold), Padding = new Padding(8, 0, 0, 0) };
                grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = UiInk, SelectionBackColor = Color.FromArgb(226, 244, 240), SelectionForeColor = UiInk, Padding = new Padding(8, 0, 0, 0), Font = new Font("맑은 고딕", 9.5f) };
                grid.RowTemplate.Height = 40; grid.AllowUserToResizeRows = false; grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 254);
            }
            ApplyModernControls(control);
        }
    }
}

internal static class UiGeometry
{
    public static GraphicsPath Round(Rectangle rect, int radius)
    {
        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)); var path = new GraphicsPath();
        if (d < 1) { path.AddRectangle(rect); return path; }
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
}

internal sealed class PageHost : TabControl
{
    public PageHost() { Appearance = TabAppearance.FlatButtons; ItemSize = new Size(0, 1); SizeMode = TabSizeMode.Fixed; TabStop = false; }
    protected override void WndProc(ref Message m) { if (m.Msg == 0x1328 && !DesignMode) { m.Result = new IntPtr(1); return; } base.WndProc(ref m); }
}

internal sealed class RoundedSurface : Panel
{
    public RoundedSurface() { DoubleBuffered = true; BackColor = Color.White; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var canvas = new SolidBrush(Color.FromArgb(243, 246, 250)); e.Graphics.FillRectangle(canvas, e.ClipRectangle); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiGeometry.Round(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 10);
        using var brush = new SolidBrush(Color.White); e.Graphics.FillPath(brush, path);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiGeometry.Round(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 10);
        using var pen = new Pen(Color.FromArgb(225, 232, 239)); e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class CameraCanvas : PictureBox
{
    protected override void OnPaint(PaintEventArgs pe)
    {
        base.OnPaint(pe); if (Image != null) return;
        var g = pe.Graphics; using var canvas = new SolidBrush(Color.FromArgb(19, 33, 48)); g.FillRectangle(canvas, ClientRectangle); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var grid = new Pen(Color.FromArgb(29, 47, 64)); for (int x = 0; x < Width; x += 56) g.DrawLine(grid, x, 0, x, Height); for (int y = 0; y < Height; y += 56) g.DrawLine(grid, 0, y, Width, y);
        using var title = new Font("맑은 고딕", 14, FontStyle.Bold); using var subtitle = new Font("맑은 고딕", 9);
        TextRenderer.DrawText(g, "카메라 연결 대기", title, new Rectangle(0, Height / 2 - 24, Width, 34), Color.FromArgb(205, 221, 234), TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, "아래 카메라 연결 버튼을 눌러 시작하세요", subtitle, new Rectangle(0, Height / 2 + 16, Width, 24), Color.FromArgb(132, 158, 182), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
    }
}
