using _01_AdminApp.Services;
using ProductionLine.Shared;

namespace _01_AdminApp;

public sealed class IssueMessagesForm : Form
{
    private readonly ProductionLineApiClient _api;
    private readonly ComboBox _issues = new() { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _history = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _draft = new() { Dock = DockStyle.Fill, Multiline = true, MaxLength = 2000, ScrollBars = ScrollBars.Vertical };
    private readonly Button _send = new() { Text = "답변 보내기", AutoSize = true };
    private readonly Button _refresh = new() { Text = "새로고침", AutoSize = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };
    private Guid? _loadedId;
    private long? _lastId;
    private bool _busy, _binding;
    private readonly HashSet<long> _seen = new();
    private IssueDto? Selected => (_issues.SelectedItem as Choice)?.Issue;

    public IssueMessagesForm(ProductionLineApiClient api)
    {
        _api = api;
        Text = "작업자 문의 / 관리자 답변";
        Font = new Font("맑은 고딕", 10);
        ClientSize = new Size(1000, 720);
        MinimumSize = new Size(850, 600);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 6 };
        foreach (var height in new[] { 45F, 45F }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 90F, 42F, 38F }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.Controls.Add(new Label { Text = "작업자 문의 / 관리자 답변", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 20, FontStyle.Bold) }, 0, 0);
        var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        filter.Controls.Add(_issues); filter.Controls.Add(_refresh);
        root.Controls.Add(filter, 0, 1);
        root.Controls.Add(_history, 0, 2);
        root.Controls.Add(_draft, 0, 3);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        actions.Controls.Add(_send);
        actions.Controls.Add(new Label { Text = "5초마다 새 메시지 확인 · 최대 2,000자 · 종결 문제는 읽기만 가능", AutoSize = true });
        root.Controls.Add(actions, 0, 4); root.Controls.Add(_status, 0, 5);
        Controls.Add(root);
        _issues.SelectedIndexChanged += async (_, _) => { if (!_binding) await RefreshAsync(false); };
        _refresh.Click += async (_, _) => await RefreshAsync(true);
        _send.Click += async (_, _) => await SendAsync();
        _timer.Tick += async (_, _) => await RefreshAsync(true);
        Shown += async (_, _) => { await RefreshAsync(true); if (!IsDisposed) _timer.Start(); };
        FormClosed += (_, _) => _timer.Dispose();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _issues.Enabled = _refresh.Enabled = !_busy;
        _send.Enabled = _draft.Enabled = !_busy && Selected is { Status: not IssueStatus.Closed };
    }

    private async Task RefreshAsync(bool refreshIssues)
    {
        if (_busy || IsDisposed || Disposing) return;
        _busy = true; UpdateButtons();
        try
        {
            if (refreshIssues)
            {
                var selected = Selected?.Id;
                var issues = await ServerCall.Run(() => _api.GetIssuesAsync());
                if (IsDisposed || Disposing) return;
                _binding = true;
                try
                {
                    _issues.Items.Clear();
                    foreach (var issue in issues.OrderByDescending(i => i.UpdatedAt)) _issues.Items.Add(new Choice(issue));
                    _issues.SelectedIndex = -1;
                    for (int i = 0; i < _issues.Items.Count; i++)
                        if (_issues.Items[i] is Choice choice && choice.Issue.Id == selected) { _issues.SelectedIndex = i; break; }
                    if (_issues.SelectedIndex < 0 && _issues.Items.Count > 0) _issues.SelectedIndex = 0;
                }
                finally { _binding = false; }
            }
            var issueId = Selected?.Id;
            if (_loadedId != issueId)
            {
                _loadedId = issueId; _lastId = null; _seen.Clear(); _history.Clear(); _draft.Clear();
            }
            if (issueId == null) { _status.Text = "문의할 문제가 없습니다."; return; }
            var messages = await ServerCall.Run(() => _api.GetIssueMessagesAsync(issueId.Value, _lastId));
            if (IsDisposed || Disposing) return;
            Append(messages);
            _status.Text = Selected!.Status == IssueStatus.Closed ? "종결된 문제입니다. 메시지는 읽기만 가능합니다." : $"새 메시지 확인: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { if (!IsDisposed && !Disposing) _status.Text = ex.Message; }
        finally { _busy = false; if (!IsDisposed && !Disposing) UpdateButtons(); }
    }

    private void Append(IEnumerable<IssueMessageDto> messages)
    {
        foreach (var message in messages.OrderBy(m => m.Id))
        {
            if (!_seen.Add(message.Id)) continue;
            _history.AppendText($"[{message.CreatedAt.LocalDateTime:MM-dd HH:mm:ss}] {message.SenderName} ({(message.SenderRole == UserRole.Admin ? "관리자" : "작업자")})\r\n{message.Message}\r\n\r\n");
            _lastId = Math.Max(_lastId ?? 0, message.Id);
        }
    }

    private async Task SendAsync()
    {
        if (_busy || Selected is not { Status: not IssueStatus.Closed } issue) return;
        string text = _draft.Text.Trim();
        if (text.Length is < 1 or > 2000) { _status.Text = "메시지를 1~2,000자로 입력하세요."; return; }
        _busy = true; UpdateButtons();
        string? failure = null;
        try
        {
            // Fetch from the previous cursor afterwards, so messages sent by a
            // worker concurrently are not skipped by advancing to our reply ID.
            await ServerCall.Run(() => _api.SendIssueMessageAsync(issue.Id, text));
            if (!IsDisposed && !Disposing) _draft.Clear();
        }
        catch (Exception ex) { failure = "전송 결과 확인 필요: " + ex.Message + " (목록과 대화를 확인한 후 재시도하세요.)"; }
        finally { _busy = false; if (!IsDisposed && !Disposing) UpdateButtons(); }
        if (!IsDisposed && !Disposing) await RefreshAsync(true);
        if (!IsDisposed && !Disposing && failure != null) _status.Text = failure;
    }

    private sealed record Choice(IssueDto Issue)
    {
        public override string ToString() => $"LINE {Issue.LineId} · {Issue.Title} · {UiText.Status(Issue.Status)} · {Issue.Id.ToString()[..8]}";
    }
}

internal static class UiText
{
    public static string Status(IssueStatus status) => status switch
    {
        IssueStatus.Occurred => "발생", IssueStatus.Acknowledged => "확인",
        IssueStatus.InProgress => "조치 중", IssueStatus.CompletionRequested => "완료 요청",
        IssueStatus.Closed => "종결", _ => status.ToString()
    };
}
