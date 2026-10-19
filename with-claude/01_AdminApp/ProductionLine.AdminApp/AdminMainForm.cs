using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using _01_AdminApp.Services;
using ProductionLine.ChatUI;
using ProductionLine.Shared;

namespace _01_AdminApp
{
    public sealed class AdminMainForm : Form
    {
        private readonly ProductionLineApiClient? _api;
        private readonly ServerConnectionControl _connection;
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 5000 };
        private readonly IDashboardService _service;
        private readonly ILineDetailService _detailService;
        private readonly IUserService _userService;
        private readonly Panel _lineList = new Panel();
        private readonly Dictionary<int, LineStatusCard> _cards = new Dictionary<int, LineStatusCard>();
        private readonly Button _refresh = new Button();
        private readonly Button _play = new Button();
        private readonly Label _status = new Label();
        private readonly Label[] _values = Enumerable.Range(0, 6).Select(_ => ValueLabel()).ToArray();
        private readonly System.Windows.Forms.Timer _animation = new System.Windows.Forms.Timer();
        private GroupChatPanel? _groupChat;
        private Form? _chatWindow;
        private bool _loading;
        private bool _layingOutCards;
        private bool _playing = true;

        public AdminMainForm(UserDto user, IDashboardService service,
            ILineDetailService detailService, IUserService userService, ProductionLineApiClient? api = null)
        {
            _api = api;
            _connection = new ServerConnectionControl(api?.BaseAddress);
            _connection.SetSessionStatus("관리자 인증 확인 대기");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _detailService = detailService ?? throw new ArgumentNullException(nameof(detailService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            Text = "생산라인 통합 관리자";
            ClientSize = new Size(1280, 950);
            MinimumSize = new Size(1050, 820);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("맑은 고딕", 10F);
            BackColor = Color.FromArgb(242, 246, 250);
            BuildScreen(user);
            _animation.Interval = 50;
            _animation.Tick += (_, _) =>
            {
                if (!_playing || !Enabled || WindowState == FormWindowState.Minimized) return;
                foreach (var card in _cards.Values)
                    if (card.Bounds.IntersectsWith(_lineList.ClientRectangle)) card.Animate();
            };
            _refreshTimer.Tick += async (_, _) => { if (Enabled) await RefreshAsync(); };
            Shown += async (_, _) =>
            {
                _groupChat?.Start();
                await RefreshAsync();
                if (!IsDisposed) { _animation.Start(); _refreshTimer.Start(); }
            };
        }

        private void BuildScreen(UserDto user)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1,
                RowCount = 7, Margin = Padding.Empty
            };
            foreach (float h in new[] { 65F, 102F, 32F, 48F })
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            header.Controls.Add(new Label
            {
                Text = "전체 생산라인 현황", Dock = DockStyle.Fill,
                Font = new Font("맑은 고딕", 23F, FontStyle.Bold),
                ForeColor = Color.FromArgb(22, 50, 79), TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            header.Controls.Add(new Label
            {
                Text = $"관리자  {user.Name} ({user.Id})", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true
            }, 1, 0);
            root.Controls.Add(header, 0, 0);
            var totals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
            string[] names = { "전체 라인", "가동 라인", "실제 검사 수", "실제 불량률", "미종결 문제", "완료 요청" };
            for (int i = 0; i < 6; i++)
            {
                totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6));
                totals.Controls.Add(Metric(names[i], _values[i]), i, 0);
            }
            root.Controls.Add(totals, 0, 1);
            root.Controls.Add(new Label
            {
                Text = "검사 수·불량률은 실제 라인만 집계  |  가상 수량은 각 라인 카드에 별도 표시",
                Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            _refresh.Text = "새로고침";
            _refresh.AutoSize = true;
            _refresh.Click += async (_, _) => await RefreshAsync();
            toolbar.Controls.Add(_refresh);
            AddMenu(toolbar, "사용자 관리", () => new UserManagementForm(_userService, _service));
            AddMenu(toolbar, "검사 결과 / 문제 조회", () => new InspectionIssueForm(_service, _detailService));
            var completion = new Button { Text = "완료 요청 확인", AutoSize = true };
            completion.Click += async (_, _) =>
            {
                using var form = new CompletionRequestForm(_service);
                form.ShowDialog(this);
                await RefreshAsync();
            };
            toolbar.Controls.Add(completion);
            AddMenu(toolbar, "검사 이력 / 통계", () => new InspectionHistoryForm(_service, _detailService));
            _play.Text = "애니메이션 일시정지";
            _play.AutoSize = true;
            _play.Click += (_, _) =>
            {
                _playing = !_playing;
                _play.Text = _playing ? "애니메이션 일시정지" : "애니메이션 재생";
                foreach (var card in _cards.Values) card.SetPlaying(_playing);
            };
            toolbar.Controls.Add(_play);
            if (_api != null) AddMenu(toolbar, "문의 / 답변", () => new IssueMessagesForm(_api));
            if (_api != null)
            {
                // 단체 채팅: 창을 닫아도 숨기기만 하고 2초마다 새 메시지를 확인해 버튼에 안 읽은 수를 표시
                _groupChat = new GroupChatPanel(_api) { Dock = DockStyle.Fill };
                // 채팅판을 처음부터 숨긴 창에 넣어 둬야 창이 닫혀 있을 때 안 읽은 수가 올라감
                _chatWindow = new Form
                {
                    Text = "단체 채팅", Size = new Size(560, 720), MinimumSize = new Size(420, 480),
                    StartPosition = FormStartPosition.CenterParent, Font = Font
                };
                _chatWindow.Controls.Add(_groupChat);
                _chatWindow.FormClosing += (_, e) =>
                {
                    if (e.CloseReason != CloseReason.UserClosing) return;
                    e.Cancel = true;
                    _chatWindow.Hide();
                };
                var chatButton = new Button { Text = "단체 채팅", AutoSize = true };
                chatButton.Click += (_, _) => ShowGroupChat();
                _groupChat.UnreadChanged += (_, _) => chatButton.Text = _groupChat.UnreadCount > 0
                    ? $"단체 채팅 ({_groupChat.UnreadCount})" : "단체 채팅";
                toolbar.Controls.Add(chatButton);
            }
            root.Controls.Add(toolbar, 0, 3);
            _lineList.Dock = DockStyle.Fill;
            _lineList.AutoScroll = true;
            _lineList.Padding = new Padding(0, 0, 0, 8);
            _lineList.SizeChanged += (_, _) => ResizeCards();
            root.Controls.Add(_lineList, 0, 4);
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.DimGray;
            root.Controls.Add(_status, 0, 5);
            _connection.Dock = DockStyle.Fill;
            root.Controls.Add(_connection, 0, 6);
            Controls.Add(root);
        }

        private async Task RefreshAsync()
        {
            if (_loading || IsDisposed) return;
            _loading = true;
            _refresh.Enabled = false;
            _status.Text = "라인 현황과 문제를 조회하는 중입니다...";
            try
            {
                var linesTask = _service.GetLinesAsync();
                var issuesTask = _service.GetIssuesAsync();
                await Task.WhenAll(linesTask, issuesTask);
                if (IsDisposed || Disposing) return;
                var lines = (await linesTask).OrderBy(x => x.Id).ToList();
                var issues = await issuesTask;
                _connection.SetSessionStatus($"관리자 인증·데이터 조회 정상 · {DateTime.Now:HH:mm:ss}");
                var real = lines.Where(x => x.IsReal).ToList();
                long normal = real.Sum(x => (long)x.NormalCount);
                long defect = real.Sum(x => (long)x.DefectCount);
                long undecided = real.Sum(x => (long)x.UndeterminedCount);
                _values[0].Text = lines.Count.ToString("N0");
                _values[1].Text = lines.Count(x => x.OperationStatus == "가동").ToString("N0");
                _values[2].Text = (normal + defect + undecided).ToString("N0");
                _values[3].Text = normal + defect == 0 ? "-" : $"{defect * 100.0 / (normal + defect):0.0}%";
                _values[4].Text = issues.Count(x => x.Status != IssueStatus.Closed).ToString("N0");
                _values[5].Text = issues.Count(x => x.Status == IssueStatus.CompletionRequested).ToString("N0");
                _lineList.SuspendLayout();
                try
                {
                    var ids = lines.Select(x => x.Id).ToHashSet();
                    foreach (int id in _cards.Keys.Where(id => !ids.Contains(id)).ToList())
                    {
                        var old = _cards[id]; _lineList.Controls.Remove(old); old.Dispose(); _cards.Remove(id);
                    }
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        if (!_cards.TryGetValue(line.Id, out var card))
                        {
                            card = new LineStatusCard();
                            card.DetailRequested += OpenLineDetail;
                            _cards.Add(line.Id, card);
                            _lineList.Controls.Add(card);
                        }
                        card.UpdateLine(line,
                            issues.Count(x => x.LineId == line.Id && x.Status != IssueStatus.Closed),
                            issues.Count(x => x.LineId == line.Id && x.Status == IssueStatus.CompletionRequested));
                        card.SetPlaying(_playing);
                        _lineList.Controls.SetChildIndex(card, i);
                    }
                    ResizeCards();
                }
                finally { _lineList.ResumeLayout(true); }
                _status.Text = lines.Count == 0 ? "등록된 생산라인이 없습니다." :
                    $"조회 완료 {DateTime.Now:HH:mm:ss}  |  수치는 5초마다 자동 갱신됩니다. 애니메이션은 실제 제품 위치·판정을 나타내지 않습니다.";
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing) { _status.Text = "조회 실패 (기존 표시 유지): " + ex.Message; _connection.SetSessionStatus("데이터 조회 실패 · 마지막 표시 값은 최신이 아닐 수 있습니다."); }
            }
            finally
            {
                _loading = false;
                if (!IsDisposed && !Disposing) _refresh.Enabled = true;
            }
        }

        private void ResizeCards()
        {
            if (_layingOutCards) return;
            _layingOutCards = true;
            int scrollX = -_lineList.AutoScrollPosition.X;
            try
            {
                _lineList.AutoScrollPosition = Point.Empty;
                const int width = 560;
                int height = Math.Max(360, _lineList.ClientSize.Height - SystemInformation.HorizontalScrollBarHeight - 12);
                int left = 0;
                foreach (var card in _lineList.Controls.OfType<LineStatusCard>())
                {
                    card.SetBounds(left, 0, width, height);
                    left += card.Width + 14;
                }
                _lineList.AutoScrollMinSize = new Size(left, 0);
                _lineList.PerformLayout();
                _lineList.AutoScrollPosition = new Point(scrollX, 0);
            }
            finally { _layingOutCards = false; }
        }
        private void OpenLineDetail(LineStatusDto line)
        {
            if (!line.IsReal)
            {
                MessageBox.Show(this, "가상 라인 현황은 카드에서 확인할 수 있습니다. 상세 검사는 대표 실제 라인을 선택하세요.", "가상 라인");
                return;
            }
            using var form = new LineDetailForm(line.Id, _service, _detailService);
            form.ShowDialog(this);
        }
        private void ShowGroupChat()
        {
            if (_chatWindow == null) return;
            if (_chatWindow.Owner == null) _chatWindow.Owner = this;
            _chatWindow.Show();
            _chatWindow.Activate();
        }
        private void AddMenu(FlowLayoutPanel toolbar, string title, Func<Form> create)
        {
            var button = new Button { Text = title, AutoSize = true };
            button.Click += (_, _) => { using var form = create(); form.ShowDialog(this); };
            toolbar.Controls.Add(button);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _refreshTimer.Stop(); _refreshTimer.Dispose(); _animation.Stop(); _animation.Dispose(); _chatWindow?.Dispose(); _groupChat?.Dispose(); }
            base.Dispose(disposing);
        }
        private static Label ValueLabel() => new Label
        {
            Text = "-", Dock = DockStyle.Fill, AutoEllipsis = true,
            Font = new Font("맑은 고딕", 23F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 100, 180)
        };
        private static Control Metric(string name, Label value)
        {
            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 8, 8, 6),
                Margin = new Padding(0, 3, 8, 5), ColumnCount = 1, RowCount = 2
            };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, ForeColor = Color.DimGray }, 0, 0);
            card.Controls.Add(value, 0, 1);
            return card;
        }
    }
}



