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
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 1000 };
        private readonly IDashboardService _service;
        private readonly ILineDetailService _detailService;
        private readonly IUserService _userService;
        private readonly Panel _lineList = new Panel();
        private readonly Dictionary<int, LineStatusCard> _cards = new Dictionary<int, LineStatusCard>();
        private readonly Button _refresh = new Button();
        private readonly Button _play = new Button();
        private readonly Label _status = new Label();
        private readonly Label[] _values = Enumerable.Range(0, 5).Select(_ => ValueLabel()).ToArray();
        private readonly System.Windows.Forms.Timer _animation = new System.Windows.Forms.Timer();
        private GroupChatPanel? _groupChat;
        private Form? _chatWindow;
        private bool _loading;
        private bool _layingOutCards;
        private bool _playing = true;
        private bool _stoppingAll, _stopUnconfirmed;
        private string? _stopStatus;
        private int _simulationTicks;
        // 보고서 기간 = 로그인해서 이 화면이 열린 시각 ~ 앱을 끄는(또는 [보고서 저장]을 누른) 시각 (김하균)
        private readonly DateTimeOffset _sessionStartedAt = DateTimeOffset.Now;
        private bool _reportSavedOnClose;

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
                if (!Enabled || WindowState == FormWindowState.Minimized) return;
                if (_playing) _simulationTicks++;
                foreach (var card in _cards.Values) card.Animate();
            };
            _refreshTimer.Tick += async (_, _) => { if (Enabled) await RefreshAsync(); };
            Shown += async (_, _) =>
            {
                _groupChat?.Start();
                await RefreshAsync();
                if (!IsDisposed) { _animation.Start(); _refreshTimer.Start(); }
            };
        }

        private void SetSimulationPlaying(bool playing)
        {
            _playing = playing;
            _play.Text = playing ? "전체 정지" : "재생";
            foreach (var card in _cards.Values) card.SetSimulationPlaying(playing);
            _values[1].Text = _cards.Values.Count(card => card.IsActive).ToString("N0");
        }

        private async Task StopAllLinesAsync()
        {
            if (_stoppingAll) return;
            _stoppingAll = true;
            _play.Enabled = false;
            SetSimulationPlaying(false);
            _stopStatus = "가상 라인 정지 · 실제 검사·벨트 정지 응답 확인 중...";
            _status.Text = _stopStatus;
            try
            {
                if (_api == null) throw new InvalidOperationException("서버 연결이 없어 실제 벨트 정지를 확인할 수 없습니다.");
                var snapshot = await _api.GetRemoteLineAsync(1);
                if (snapshot.Worker is not { IsOnline: true } worker)
                    throw new InvalidOperationException("작업자가 오프라인입니다. 현장에서 벨트 상태를 확인하세요.");
                var command = await _api.CreateRemoteCommandAsync(1,
                    new(Guid.NewGuid(), worker.ClientId, RemoteAction.StopAll));
                var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
                while (true)
                {
                    if (IsDisposed || Disposing) return;
                    if (command.Status == RemoteCommandStatus.Succeeded) break;
                    if (command.Status is RemoteCommandStatus.Failed or RemoteCommandStatus.Expired or RemoteCommandStatus.Cancelled or RemoteCommandStatus.Unknown)
                        throw new InvalidOperationException(command.Message);
                    if (DateTimeOffset.UtcNow >= deadline)
                        throw new TimeoutException("작업자의 정지 응답이 없습니다. 명령 실행 이력과 현장 장비를 확인하세요.");
                    await Task.Delay(250);
                    var current = await _api.GetRemoteLineAsync(1);
                    command = current.Commands.FirstOrDefault(c => c.Id == command.Id)
                        ?? throw new InvalidOperationException("정지 명령의 처리 결과를 확인할 수 없습니다.");
                }
                _stopUnconfirmed = false;
                _stopStatus = "전체 정지 완료 · 실제 검사·벨트 정지 응답 확인 · 가상 라인 정지";
            }
            catch (Exception ex)
            {
                _stopUnconfirmed = true;
                _stopStatus = "가상 라인 정지 · 실제 벨트 정지 미확인: " + ex.Message;
            }
            finally
            {
                _stoppingAll = false;
                if (!IsDisposed && !Disposing)
                {
                    _play.Enabled = true;
                    if (_stopUnconfirmed) _play.Text = "전체 정지 재시도";
                    _status.Text = _stopStatus;
                }
            }
        }

        private void BuildScreen(UserDto user)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1,
                RowCount = 7, Margin = Padding.Empty
            };
            foreach (float h in new[] { 55F, 96F, 24F, 44F })
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
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
            var totals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            string[] names = { "전체 라인", "구동 중인 라인", "실제 검사 수", "실제 불량률", "미종결 문제" };
            for (int i = 0; i < 5; i++)
            {
                totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 5));
                totals.Controls.Add(Metric(names[i], _values[i]), i, 0);
            }
            root.Controls.Add(totals, 0, 1);
            root.Controls.Add(new Label
            {
                Text = "검사 수·불량률은 실제 라인만 집계  |  라인 2·3·4는 예시 데이터  |  전체 정지: 실제 검사·벨트 및 가상 라인 정지",
                Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            _refresh.Text = "새로고침";
            _refresh.AutoSize = true;
            _refresh.Click += async (_, _) => await RefreshAsync();
            toolbar.Controls.Add(_refresh);
            if (_api != null)
            {
                var remote = new Button { Text = "작업자 원격 운영 · 그래프", AutoSize = true };
                remote.Click += (_, _) => { using var form = new RemoteWorkerForm(_api); form.ShowDialog(this); };
                toolbar.Controls.Add(remote);
            }
            AddMenu(toolbar, "사용자 관리", () => new UserManagementForm(_userService, _service));
            AddMenu(toolbar, "검사 결과 / 문제 조회", () => new InspectionIssueForm(_service, _detailService));
            var completion = new Button { Text = "완료 요청 확인", AutoSize = true };
            completion.Click += async (_, _) =>
            {
                using var form = new CompletionRequestForm(_service, _api);
                form.ShowDialog(this);
                await RefreshAsync();
            };
            toolbar.Controls.Add(completion);
            AddMenu(toolbar, "검사 이력 / 통계", () => new InspectionHistoryForm(_service, _detailService));
            if (_api != null)
            {
                // 생산 검사 보고서: 지금까지(로그인~지금)를 CSV로 저장. 앱을 끌 때도 자동 저장됩니다.
                var report = new Button { Text = "보고서 저장", AutoSize = true };
                report.Click += async (_, _) =>
                {
                    report.Enabled = false;
                    try
                    {
                        var path = await SaveReportAsync();
                        _status.Text = "보고서 저장 완료: " + path;
                        MessageBox.Show(this, "보고서를 저장했습니다.\r\n" + path, "보고서 저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "보고서를 저장하지 못했습니다.\r\n" + ex.Message, "보고서 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    finally { if (!IsDisposed) report.Enabled = true; }
                };
                toolbar.Controls.Add(report);
            }
            _play.Text = "전체 정지";
            _play.AutoSize = true;
            _play.Enabled = true;
            _play.Click += async (_, _) =>
            {
                if (_stoppingAll) return;
                if (_playing || _stopUnconfirmed)
                {
                    if (MessageBox.Show(this,
                        "실제 라인의 검사·벨트와 모든 가상 라인을 정지할까요?\r\n자동 분류도 해제됩니다. 실제 벨트 재구동은 별도로 해야 합니다.",
                        "전체 정지 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                    await StopAllLinesAsync();
                }
                else
                {
                    SetSimulationPlaying(true);
                    _stopStatus = "가상 라인 재생 중 · 실제 벨트 재구동 명령은 보내지 않았습니다. 대표 라인은 장비 상태를 따릅니다.";
                    _status.Text = _stopStatus;
                }
            };
            toolbar.Controls.Add(_play);
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
            _lineList.AutoScroll = false;
            _lineList.Padding = Padding.Empty;
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

        // 1번 라인 보고서를 서버에서 받아 문서\생산라인 보고서 폴더에 CSV(엑셀용 UTF-8 BOM)로 저장하고 경로를 돌려줍니다.
        private async Task<string> SaveReportAsync()
        {
            if (_api == null) throw new InvalidOperationException("서버 연결이 없습니다.");
            var to = DateTimeOffset.Now;
            var csv = await _api.GetLineReportCsvAsync(1, _sessionStartedAt, to);
            var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "생산라인 보고서");
            System.IO.Directory.CreateDirectory(folder);
            var path = System.IO.Path.Combine(folder, $"1번라인_생산검사보고서_{_sessionStartedAt:yyyyMMdd_HHmm}-{to:yyyyMMdd_HHmm}.csv");
            System.IO.File.WriteAllText(path, csv, new System.Text.UTF8Encoding(true));
            return path;
        }

        // 앱을 끄면 먼저 보고서를 자동 저장하고 나서 닫습니다. 저장에 실패해도 안내만 하고 닫습니다.
        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || _reportSavedOnClose || _api == null) return;
            e.Cancel = true;
            _reportSavedOnClose = true;
            Enabled = false;
            _status.Text = "앱을 끄기 전에 보고서를 저장하는 중입니다...";
            try
            {
                var path = await SaveReportAsync();
                MessageBox.Show(this, "보고서를 자동 저장했습니다.\r\n" + path, "보고서 자동 저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "보고서를 자동 저장하지 못했습니다.\r\n" + ex.Message, "보고서 자동 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Close();
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
                var lines = (await linesTask).OrderBy(x => x.Id).Select(SimulationLine).ToList();
                var issues = await issuesTask;
                _connection.SetSessionStatus($"관리자 인증·데이터 조회 정상 · {DateTime.Now:HH:mm:ss}");
                var real = lines.Where(x => x.IsReal).ToList();
                long normal = real.Sum(x => (long)x.NormalCount);
                long defect = real.Sum(x => (long)x.DefectCount);
                long undecided = real.Sum(x => (long)x.UndeterminedCount);
                _values[0].Text = lines.Count.ToString("N0");
                _values[1].Text = lines.Count(line => line.IsReal
                    ? line.Hardware is { IsFresh: true, BeltRunning: true, EmergencyStopped: false } : _playing).ToString("N0");
                _values[2].Text = (normal + defect + undecided).ToString("N0");
                _values[3].Text = normal + defect == 0 ? "-" : $"{defect * 100.0 / (normal + defect):0.0}%";
                _values[4].Text = issues.Count(x => x.Status != IssueStatus.Closed).ToString("N0");
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
                            card.UseCompactLayout();
                            card.DetailRequested += OpenLineDetail;
                            _cards.Add(line.Id, card);
                            _lineList.Controls.Add(card);
                        }
                        card.UpdateLine(line,
                            issues.Count(x => x.LineId == line.Id && x.Status != IssueStatus.Closed),
                            issues.Count(x => x.LineId == line.Id && x.Status == IssueStatus.CompletionRequested));
                        card.SetSimulationPlaying(_playing);
                        _lineList.Controls.SetChildIndex(card, i);
                    }
                    ResizeCards();
                }
                finally { _lineList.ResumeLayout(true); }
                _status.Text = _stopStatus ?? (lines.Count == 0 ? "등록된 생산라인이 없습니다." :
                    $"가상 라인 {(_playing ? "재생 중" : "정지")} · {DateTime.Now:HH:mm:ss}\r\n대표 라인은 실제 장비 상태 연동 · 라인 2·3·4의 수량과 제품 이동은 시연용입니다.");
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing) { _status.Text = "조회 실패 (기존 데이터 표시 유지) · 시뮬레이션 재생은 계속 제어할 수 있습니다: " + ex.Message; _connection.SetSessionStatus("데이터 조회 실패 · 마지막 표시 값은 최신이 아닐 수 있습니다."); }
            }
            finally
            {
                _loading = false;
                if (!IsDisposed && !Disposing) _refresh.Enabled = true;
            }
        }

        private LineStatusDto SimulationLine(LineStatusDto line)
        {
            if (line.IsReal) return line;
            var seed = line.Id switch { 2 => (12,0), 3 => (0,2), _ => (9,2) };
            _cards.TryGetValue(line.Id,out var card);
            return line with
            {
                OperationStatus = _playing ? "가동" : "정지", InspectionStatus = RotaryGateSimulation.ProfileDescription(line.Id),
                NormalCount = seed.Item1 + (card?.SimulationNormalCount ?? 0),
                DefectCount = seed.Item2 + (card?.SimulationDefectCount ?? 0), UndeterminedCount = 0,
                UpdatedAt = DateTimeOffset.Now, Hardware = null
            };
        }

        private void ResizeCards()
        {
            if (_layingOutCards) return;
            _layingOutCards = true;
            try
            {
                const int gap = 12;
                var cards = _cards.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
                var rows = Math.Max(1,(cards.Length+1)/2);
                int width = Math.Max(1,(_lineList.ClientSize.Width-gap)/2);
                int height = Math.Max(1,(_lineList.ClientSize.Height-gap*(rows-1))/rows);
                for (int i=0;i<cards.Length;i++)
                {
                    cards[i].SetBounds((i%2)*(width+gap),(i/2)*(height+gap),width,height);
                }
                _lineList.AutoScrollMinSize = Size.Empty;
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
            using var form = new LineDetailForm(line.Id, _service, _detailService, _api);
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



