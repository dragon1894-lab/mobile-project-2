using System.Net;
using ProductionLine.Shared;

namespace ProductionLine.UserApp;

public sealed partial class UserMainForm
{
    private readonly System.Windows.Forms.Timer _messageRefresh = new() { Interval = 5000 };
    private readonly Dictionary<Guid, SortedDictionary<long, IssueMessageDto>> _serverMessages = new();
    private readonly Dictionary<Guid, string> _adminDrafts = new();
    private readonly HashSet<Guid> _messagesLoaded = new();
    private readonly Dictionary<Guid, long> _messageCursors = new();
    private Guid? _adminDraftIssue;
    private bool _pollingMessages, _sendingMessage;

    private void UpdateAdminAvailability()
    {
        var issue = _adminIssue.SelectedItem as IssueDto;
        _adminSend.Enabled = !_sendingMessage && issue != null && issue.Status != IssueStatus.Closed;
        _adminInput.ReadOnly = _sendingMessage || issue == null || issue.Status == IssueStatus.Closed;
    }

    private void ChangeAdminIssue()
    {
        var id = (_adminIssue.SelectedItem as IssueDto)?.Id;
        if (_adminDraftIssue != id)
        {
            if (_adminDraftIssue is Guid previous) _adminDrafts[previous] = _adminInput.Text;
            _adminDraftIssue = id;
            _adminInput.Text = id is Guid current && _adminDrafts.TryGetValue(current, out var draft) ? draft : string.Empty;
        }
        RenderAdminMessages();
        UpdateAdminAvailability();
    }

    private void MergeAdminMessages(Guid issueId, IEnumerable<IssueMessageDto> messages)
    {
        if (!_serverMessages.TryGetValue(issueId, out var cache))
            _serverMessages[issueId] = cache = new();
        foreach (var message in messages.Where(x => x.IssueId == issueId)) cache[message.Id] = message;
    }

    private async Task PollAdminMessagesAsync()
    {
        if (_pollingMessages || IsDisposed || SessionExpired || _adminIssue.SelectedItem is not IssueDto issue) return;
        _pollingMessages = true;
        try
        {
            long? afterId = _messageCursors.TryGetValue(issue.Id, out var cursor) ? cursor : null;
            var messages = await _api.GetIssueMessagesAsync(issue.Id, afterId);
            if (IsDisposed) return;
            MergeAdminMessages(issue.Id, messages);
            if (messages.Count > 0) _messageCursors[issue.Id] = Math.Max(afterId ?? 0, messages.Max(x => x.Id));
            _messagesLoaded.Add(issue.Id);
            if ((_adminIssue.SelectedItem as IssueDto)?.Id == issue.Id) RenderAdminMessages();
        }
        catch (Exception ex) { await HandleServerFailureAsync(ex, "관리자 대화 조회 실패"); }
        finally { _pollingMessages = false; }
    }

    private async Task SendAdminMessageAsync()
    {
        if (_sendingMessage || _adminIssue.SelectedItem is not IssueDto issue) return;
        if (issue.Status == IssueStatus.Closed) { UpdateAdminAvailability(); return; }
        var original = _adminInput.Text;
        var text = original.Trim();
        if (text.Length is < 1 or > 2000) { _connection.Text = "메시지는 1~2000자로 입력하세요."; return; }
        _sendingMessage = true;
        UpdateAdminAvailability();
        try
        {
            var message = await _api.SendIssueMessageAsync(issue.Id, text);
            if (IsDisposed) return;
            // 최초 전체 조회 이전의 전송 응답을 마지막 조회 번호로 사용하면 과거 대화를 놓칠 수 있습니다.
            MergeAdminMessages(issue.Id, new[] { message });
            if (_adminDraftIssue == issue.Id && _adminInput.Text == original) _adminInput.Clear();
            if (_adminDrafts.GetValueOrDefault(issue.Id) == original) _adminDrafts.Remove(issue.Id);
            RenderAdminMessages();
            _connection.Text = "관리자에게 전송했습니다. 답장을 5초마다 확인합니다.";
            await PollAdminMessagesAsync();
        }
        catch (Exception ex)
        {
            await HandleServerFailureAsync(ex, "메시지 전송 결과를 확인하지 못했습니다 · 입력 유지됨 · 대화 확인 후 재전송하세요", refreshIssues: true);
        }
        finally { _sendingMessage = false; if (!IsDisposed) UpdateAdminAvailability(); }
    }

    private void RenderAdminMessages()
    {
        _adminMessages.Clear();
        if (_adminIssue.SelectedItem is not IssueDto issue) { _adminMessages.Text = "문제를 먼저 선택하세요."; return; }
        if (_serverMessages.TryGetValue(issue.Id, out var messages))
            foreach (var message in messages.Values)
                _adminMessages.AppendText($"[{(message.SenderRole == UserRole.Admin ? "관리자" : "작업자")} · {message.SenderName} · {message.CreatedAt.ToLocalTime():MM-dd HH:mm}]\r\n{message.Message}\r\n\r\n");
        if (_adminMessages.TextLength == 0) _adminMessages.Text = _messagesLoaded.Contains(issue.Id) ? "아직 대화가 없습니다." : "서버 대화를 확인하고 있습니다.";
        _adminMessages.SelectionStart = _adminMessages.TextLength;
        _adminMessages.ScrollToCaret();
    }

    private async Task HandleServerFailureAsync(Exception error, string context, bool refreshIssues = false)
    {
        if (IsDisposed) return;
        // 현재 공용 클라이언트는 HTTP 상태를 보존하지 않고 서버 메시지만 예외로 전달합니다.
        // HttpRequestException의 상태 또는 현재 서버의 명확한 인증 만료 문구만 처리합니다.
        bool unauthorized = error is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized }
                            || error.Message == "로그인이 필요합니다.";
        if (unauthorized)
        {
            SessionExpired = true;
            LogoutRequested = true;
            _refresh.Stop();
            _messageRefresh.Stop();
            StopInspection("로그인이 필요하여 검사를 중지했습니다.");
            Close();
            return;
        }

        // 404/409/권한 변경/불확실한 통신 결과 후 상태를 재조회합니다. 변경 요청은 재전송하지 않습니다.
        if (refreshIssues) await RefreshAfterChangeAsync();
        if (!IsDisposed) _connection.Text = $"{context}: {error.Message}";
    }

    private async Task RefreshAfterChangeAsync()
    {
        // 자동 조회와 겹쳐도 변경 후 조회가 건너뛰어지지 않도록 기다린 뒤 다시 조회합니다.
        while (_loading && !IsDisposed) await Task.Delay(40);
        if (!IsDisposed) await RefreshAsync(false);
    }
}
