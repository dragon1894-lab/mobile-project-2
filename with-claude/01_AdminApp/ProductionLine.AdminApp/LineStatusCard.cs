using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ProductionLine.Shared;

namespace _01_AdminApp
{
    public sealed class LineStatusCard : UserControl
    {
        private readonly Label _title = new Label();
        private readonly Label _state = new Label();
        private readonly Label _counts = new Label();
        private readonly Label _updated = new Label();
        private readonly Button _detail = new Button();
        private readonly Button _emergency = new Button();
        private readonly ConveyorView _conveyor = new ConveyorView();
        private LineStatusDto? _line;
        public event Action<LineStatusDto>? DetailRequested;

        public LineStatusCard()
        {
            Height = 360;
            Margin = new Padding(0, 0, 0, 14);
            BackColor = Color.White;
            Padding = new Padding(14);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            foreach (float h in new[] { 40F, 50F, 45F }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            _title.Dock = DockStyle.Fill;
            _title.AutoEllipsis = true;
            _title.Font = new Font("맑은 고딕", 15F, FontStyle.Bold);
            _title.ForeColor = Color.FromArgb(22, 50, 79);
            _detail.Text = "상세 보기";
            _detail.Dock = DockStyle.Fill;
            _detail.Click += (_, _) => { if (_line != null) DetailRequested?.Invoke(_line); };
            header.Controls.Add(_title, 0, 0); header.Controls.Add(_detail, 1, 0);
            _state.Dock = _counts.Dock = _updated.Dock = DockStyle.Fill;
            _state.TextAlign = _counts.TextAlign = _updated.TextAlign = ContentAlignment.MiddleLeft;
            _state.AutoEllipsis = _counts.AutoEllipsis = _updated.AutoEllipsis = true;
            _updated.ForeColor = Color.DimGray;
            _conveyor.Dock = DockStyle.Fill;
            _emergency.Text = "긴급 정지 (시뮬레이션)";
            _emergency.Dock = DockStyle.Fill;
            _emergency.BackColor = Color.Firebrick;
            _emergency.ForeColor = Color.White;
            _emergency.UseVisualStyleBackColor = false;
            _emergency.FlatStyle = FlatStyle.Flat;
            _emergency.Click += (_, _) => SetEmergencyStopped(!EmergencyStopped);
            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(_state, 0, 1);
            layout.Controls.Add(_counts, 0, 2);
            layout.Controls.Add(_conveyor, 0, 3);
            layout.Controls.Add(_emergency, 0, 4);
            layout.Controls.Add(_updated, 0, 5);
            Controls.Add(layout);
            _title.DoubleClick += (_, _) => { if (_line != null) DetailRequested?.Invoke(_line); };
        }

        public void UpdateLine(LineStatusDto line, int openIssues, int completionRequests)
        {
            _line = line;
            _title.Text = line.Name + (line.IsReal ? "  [실제]" : "  [가상]");
            _state.Text = $"가동: {line.OperationStatus}   |   검사: {line.InspectionStatus}   |   미종결 문제 {openIssues}건   |   완료 요청 {completionRequests}건";
            _state.ForeColor = line.OperationStatus == "가동" ? Color.SeaGreen : Color.Firebrick;
            long determined = (long)line.NormalCount + line.DefectCount;
            string rate = determined == 0 ? "-" : $"{line.DefectCount * 100.0 / determined:0.0}%";
            _counts.Text = $"정상 {line.NormalCount:N0}   /   불량 {line.DefectCount:N0}   /   판정 불가 {line.UndeterminedCount:N0}   /   불량률 {rate}";
            _updated.Text = $"데이터 갱신: {line.UpdatedAt:yyyy-MM-dd HH:mm:ss}   |   {(line.IsReal ? "실제 라인 현황" : "가상 데이터 - 실제 생산 통계와 구분")}";
            _detail.Text = line.IsReal ? "상세 보기" : "가상 라인 안내";
            _conveyor.Running = line.OperationStatus == "가동";
            _conveyor.Invalidate();
        }
        public void Animate() => _conveyor.Advance();
        public void SetPlaying(bool playing) { _conveyor.Playing = playing; _conveyor.Invalidate(); }
        public bool EmergencyStopped => _conveyor.EmergencyStopped;
        public void SetEmergencyStopped(bool stopped)
        {
            _conveyor.EmergencyStopped = stopped;
            _emergency.Text = stopped ? "긴급 정지 해제 (시뮬레이션)" : "긴급 정지 (시뮬레이션)";
            _emergency.BackColor = stopped ? Color.FromArgb(120, 30, 30) : Color.Firebrick;
            _conveyor.Invalidate();
        }

        private sealed class ConveyorView : Control
        {
            private readonly PusherSimulation _simulation = new PusherSimulation();
            private double _beltPhase;
            public bool Running { get; set; }
            public bool Playing { get; set; } = true;
            public bool EmergencyStopped { get; set; }

            public ConveyorView()
            {
                DoubleBuffered = true;
                BackColor = Color.FromArgb(248, 248, 248);
                SetStyle(ControlStyles.ResizeRedraw, true);
            }
            public void Advance()
            {
                if (!Running || !Playing || EmergencyStopped) return;
                _simulation.Advance(0.05);
                // 판이 밀고 복귀하는 동안 컨베이어도 대기
                if (_simulation.BeltMoving) _beltPhase = (_beltPhase + .06) % 1;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Width < 10 || Height < 10) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var saved = g.Save();
                float scale = Math.Min(Width / 520F, Height / 280F);
                g.TranslateTransform((Width - 520 * scale) / 2, (Height - 280 * scale) / 2);
                g.ScaleTransform(scale, scale);
                using var text = new Font("맑은 고딕", 9F);
                using var small = new Font("맑은 고딕", 8F);
                using var pen = new Pen(Color.FromArgb(120, 120, 120), 1);
                using var frame = new SolidBrush(Color.FromArgb(205, 205, 205));
                using var belt = new SolidBrush(Color.FromArgb(232, 232, 232));
                using var dark = new SolidBrush(Color.FromArgb(125, 131, 136));

                string state = EmergencyStopped ? "긴급 정지" : !Running ? "라인 정지" : !Playing ? "일시정지" : _simulation.StageText;
                Label(g, "직선 라인 · 밀판 분류", text, Color.FromArgb(55, 55, 55), 15, 10);
                Label(g, state, small, EmergencyStopped ? Color.Firebrick : Color.DimGray, 338, 12);
                using (var divider = new Pen(Color.FromArgb(220, 220, 220)))
                    g.DrawLine(divider, 15, 37, 505, 37);

                // 컨베이어 위쪽의 인식 불가 분류함
                using (var bin = new SolidBrush(Color.FromArgb(240, 240, 240)))
                    g.FillRectangle(bin, 348, 59, 64, 43);
                g.DrawRectangle(pen, 348, 59, 64, 43);
                Label(g, "인식 불가 분류함", small, Color.FromArgb(150, 85, 20), 331, 41);
                using (var chute = new SolidBrush(Color.FromArgb(224, 224, 224)))
                    g.FillRectangle(chute, 355, 102, 50, 26);
                g.DrawLine(pen, 355, 102, 355, 126);
                g.DrawLine(pen, 405, 102, 405, 126);

                // 분기 벨트 없이 오른쪽으로 이어지는 직선 컨베이어
                g.FillRectangle(frame, 20, 126, 474, 60);
                g.DrawRectangle(pen, 20, 126, 474, 60);
                g.FillRectangle(belt, 27, 132, 460, 48);
                using (var tread = new Pen(Color.FromArgb(195, 195, 195)))
                    for (float x = 29 + (float)_beltPhase * 22; x < 487; x += 22)
                        g.DrawLine(tread, x, 133, x - 7, 179);
                foreach (float x in new[] { 31F, 482F })
                {
                    g.FillEllipse(dark, x - 7, 139, 14, 34);
                    g.DrawEllipse(pen, x - 7, 139, 14, 34);
                }
                // 분류 구간의 옆 레일은 열어 두어 밀려 나가는 방향 표현
                g.DrawLine(pen, 32, 126, 355, 126);
                g.DrawLine(pen, 405, 126, 482, 126);
                Label(g, "투입 →", small, Color.DimGray, 23, 106);
                Label(g, "통과 →", small, Color.DarkGreen, 444, 106);

                // 카메라 검사 위치
                g.FillRectangle(frame, 181, 84, 7, 104);
                g.FillRectangle(frame, 228, 84, 7, 104);
                g.FillRectangle(frame, 181, 82, 54, 12);
                g.DrawRectangle(pen, 181, 82, 54, 12);
                g.FillRectangle(dark, 195, 94, 27, 18);
                using (var lens = new SolidBrush(Color.FromArgb(65, 70, 74)))
                    g.FillEllipse(lens, 204, 98, 9, 9);
                Label(g, "인식부", small, Color.DimGray, 190, 61);

                // 밀판은 하단에 대기하다 인식 불가일 때 위쪽으로 직선 이동
                float plateY = 190 - 98 * (float)_simulation.PlateExtension;
                g.FillRectangle(frame, 353, 224, 54, 17);
                g.DrawRectangle(pen, 353, 224, 54, 17);
                g.FillRectangle(dark, 377, plateY + 8, 6, 224 - plateY - 8);
                g.FillRectangle(frame, 363, plateY, 34, 8);
                g.DrawRectangle(pen, 363, plateY, 34, 8);
                Label(g, "분류판", small, Color.DimGray, 362, 244);

                if (_simulation.ProductVisible)
                {
                    float x = (float)_simulation.ProductX, y = (float)_simulation.ProductY;
                    Color borderColor = !_simulation.Inspected ? Color.FromArgb(95, 105, 112) :
                        _simulation.IsUnrecognized ? Color.FromArgb(175, 105, 25) : Color.DarkGreen;
                    using var product = new SolidBrush(Color.FromArgb(210, 217, 221));
                    using var border = new Pen(borderColor, 1.7F);
                    g.FillRectangle(product, x - 10, y - 17, 20, 34);
                    g.DrawRectangle(border, x - 10, y - 17, 20, 34);
                    g.DrawLine(pen, x - 6, y - 12, x + 6, y - 12);
                }
                string result = !_simulation.Inspected ? "판정 대기" :
                    _simulation.IsUnrecognized ? "인식 불가 · 밀판 분류" : "통과 · 밀판 대기";
                Label(g, result, small, _simulation.Inspected && _simulation.IsUnrecognized ?
                    Color.FromArgb(160, 95, 20) : Color.DimGray, 16, 207);
                Label(g, "시연: 통과 / 인식 불가 순서로 반복 (실제 데이터와 무관)", small, Color.Gray, 15, 263);
                g.Restore(saved);
            }
            private static void Label(Graphics g, string text, Font font, Color color, float x, float y)
            {
                using var brush = new SolidBrush(color);
                g.DrawString(text, font, brush, x, y);
            }
        }
    }

    // 화면과 분리한 시연 상태. 데이터베이스/서버 상태를 수정하지 않음.
    internal sealed class PusherSimulation
    {
        internal enum Step { Feeding, Inspecting, ToPusher, Passing, Extending, Holding, Retracting, Gap }
        public Step Stage { get; private set; } = Step.Feeding;
        private double _elapsed;
        public bool IsUnrecognized { get; private set; }
        public bool Inspected => Stage != Step.Feeding && Stage != Step.Inspecting;
        public bool ProductVisible => Stage != Step.Gap;
        public bool BeltMoving => Stage == Step.Feeding || Stage == Step.ToPusher || Stage == Step.Passing;
        private double Progress => Math.Clamp(_elapsed / Duration(Stage), 0, 1);
        public double PlateExtension => Stage switch
        {
            Step.Extending => Smooth(Progress),
            Step.Holding => 1,
            Step.Retracting => 1 - Smooth(Progress),
            _ => 0
        };
        public double ProductX => Stage switch
        {
            Step.Feeding => 40 + 168 * Progress,
            Step.Inspecting => 208,
            Step.ToPusher => 208 + 172 * Progress,
            Step.Passing => 380 + 145 * Progress,
            _ => 380
        };
        public double ProductY => Stage switch
        {
            Step.Extending => 156 - 80 * Math.Clamp((PlateExtension - .16) / .84, 0, 1),
            Step.Holding or Step.Retracting => 76,
            _ => 156
        };
        public string StageText => Stage switch
        {
            Step.Feeding => "물체 이동 중", Step.Inspecting => "인식 중",
            Step.ToPusher => "분류 위치로 이동", Step.Passing => "직진 통과",
            Step.Extending => "분류판 전진", Step.Holding => "분류함 이동 완료",
            Step.Retracting => "분류판 복귀", _ => "다음 물체 대기"
        };
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            while (seconds > 0)
            {
                double amount = Math.Min(seconds, Duration(Stage) - _elapsed);
                _elapsed += amount; seconds -= amount;
                if (_elapsed + 1e-9 < Duration(Stage)) break;
                _elapsed = 0;
                Stage = Stage switch
                {
                    Step.Feeding => Step.Inspecting,
                    Step.Inspecting => Step.ToPusher,
                    Step.ToPusher => IsUnrecognized ? Step.Extending : Step.Passing,
                    Step.Passing => Step.Gap,
                    Step.Extending => Step.Holding,
                    Step.Holding => Step.Retracting,
                    Step.Retracting => Step.Gap,
                    _ => Step.Feeding
                };
                if (Stage == Step.Feeding) IsUnrecognized = !IsUnrecognized;
            }
        }
        private static double Smooth(double value) => value * value * (3 - 2 * value);
        private static double Duration(Step stage) => stage switch
        {
            Step.Feeding => 2.5, Step.Inspecting => .8, Step.ToPusher => 1.8,
            Step.Passing => 1.3, Step.Extending => .85, Step.Holding => .5,
            Step.Retracting => .85, _ => .55
        };
    }
}
