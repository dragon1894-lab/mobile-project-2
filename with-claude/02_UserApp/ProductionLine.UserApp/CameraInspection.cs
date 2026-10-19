using ProductionLine.Shared;
using ProductionLine.Vision;

namespace ProductionLine.UserApp;

public sealed partial class UserMainForm
{
    private ICameraFeed? _camera;
    private Task _cameraClosed = Task.CompletedTask;
    private readonly NumericUpDown _cameraIndex = new() { Minimum = 0, Maximum = 9, Width = 55 };
    private readonly Button _cameraConnect = new() { Text = "카메라 연결", AutoSize = true };
    private readonly Button _inspectionStart = new() { Text = "검사 시작", AutoSize = true };
    private readonly Label _inspectionState = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Text = "검사 대기 · 정상 기준 영상을 등록하세요." };
    private readonly System.Windows.Forms.Timer _inspectionTimer = new() { Interval = 1500 };
    private byte[]? _reference;
    private long _displayedFrame, _inspectedFrame;
    private bool _runningInspection, _inspectionBusy;
    private int _inspectionGeneration;

    private Control CameraControls()
    {
        var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        buttons.WrapContents = false;
        buttons.AutoScroll = true;
        buttons.Controls.Add(new Label { Text = "카메라 번호", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        buttons.Controls.Add(_cameraIndex);
        buttons.Controls.Add(_cameraConnect);
        buttons.Controls.Add(Button("연결 해제", DisconnectCamera));
        buttons.Controls.Add(Button("정상 기준 등록", SetReference));
        buttons.Controls.Add(_inspectionStart);
        buttons.Controls.Add(Button("검사 중지", () => StopInspection("검사 중지 · 전송 중인 1건은 완료될 수 있습니다.")));
        foreach (Control control in buttons.Controls)
            if (control is Button button)
            {
                var width = button.GetPreferredSize(Size.Empty).Width;
                button.AutoSize = false;
                button.Size = new Size(width, 34);
                button.Margin = new Padding(3, 2, 3, 2);
            }
        container.Controls.Add(buttons, 0, 0);
        container.Controls.Add(_inspectionState, 0, 1);
        _cameraConnect.Click += (_, _) => ConnectCamera();
        _inspectionStart.Click += async (_, _) => await StartInspectionAsync();
        _inspectionTimer.Tick += async (_, _) => await InspectCameraAsync();
        return container;
    }

    private void ConnectCamera()
    {
        if (_camera != null) return;
        if (!_cameraClosed.IsCompleted) { _videoStatus.Text = "이전 카메라 연결을 해제하는 중입니다. 잠시 후 다시 연결하세요."; return; }
        _reference = null;
        _displayedFrame = _inspectedFrame = 0;
        _camera = new CameraSession((int)_cameraIndex.Value);
        _cameraConnect.Enabled = false; _cameraIndex.Enabled = false;
        _videoStatus.Text = "카메라 연결 중...";
        _frames.Interval = 50;
    }

    private void DisconnectCamera()
    {
        StopInspection("검사 대기 · 카메라와 정상 기준을 다시 등록하세요.");
        var old = _camera; _camera = null;
        if (old != null) { old.Dispose(); _cameraClosed = old.Completion; }
        _reference = null;
        _cameraConnect.Enabled = true; _cameraIndex.Enabled = true;
        _video.Image?.Dispose(); _video.Image = null;
        _videoStatus.Text = "카메라 연결 해제됨";
        _frames.Interval = 500;
    }

    private void DisplayCameraFrame()
    {
        if (_camera == null) return;
        if (_camera.Error is string error)
        {
            DisconnectCamera(); _videoStatus.Text = error; return;
        }
        var frame = _camera.Latest;
        if (frame == null) return;
        if ((DateTime.UtcNow - frame.ReceivedUtc).TotalSeconds > 3)
        {
            StopInspection("영상 수신 지연으로 검사 중지");
            _videoStatus.Text = "카메라 영상이 3초 이상 갱신되지 않았습니다."; return;
        }
        if (frame.Sequence == _displayedFrame) return;
        using var stream = new MemoryStream(frame.Jpeg);
        using var source = Image.FromStream(stream);
        var old = _video.Image; _video.Image = new Bitmap(source); old?.Dispose();
        _displayedFrame = frame.Sequence;
        _videoStatus.Text = $"카메라 {_cameraIndex.Value} 연결됨 · {frame.ReceivedUtc.ToLocalTime():HH:mm:ss} · 프레임 {frame.Sequence}";
    }

    private void SetReference()
    {
        var frame = _camera?.Latest;
        if (frame == null || (DateTime.UtcNow - frame.ReceivedUtc).TotalSeconds > 3)
        { _inspectionState.Text = "먼저 카메라를 연결하고 정상 제품을 화면에 놓으세요."; return; }
        StopInspection("정상 기준 등록 완료 · 검사 시작을 누르세요.");
        _reference = frame.Jpeg.ToArray();
    }

    private async Task StartInspectionAsync()
    {
        if (_runningInspection || _inspectionBusy) return;
        if (_camera?.Latest == null || _reference == null)
        { _inspectionState.Text = "카메라 연결 → 정상 제품 배치 → 정상 기준 등록 순서로 준비하세요."; return; }
        if (_lines.SelectedItem is not LineStatusDto { IsReal: true })
        { _inspectionState.Text = "실제 검사 가능한 라인을 선택하세요."; return; }
        _runningInspection = true; _inspectionStart.Enabled = false;
        _inspectionGeneration++;
        _inspectionState.Text = "검사 중 · 1.5초마다 현재 프레임 1건 검사 (제품 감지 기반 개수 아님)";
        _inspectionTimer.Start();
        await InspectCameraAsync();
    }

    private void StopInspection(string message)
    {
        _runningInspection = false; _inspectionGeneration++;
        _inspectionTimer.Stop(); _inspectionStart.Enabled = !_inspectionBusy;
        _inspectionState.Text = message;
    }

    private async Task InspectCameraAsync()
    {
        if (!_runningInspection || _inspectionBusy) return;
        var frame = _camera?.Latest;
        if (frame == null || _reference == null || (DateTime.UtcNow - frame.ReceivedUtc).TotalSeconds > 3)
        { StopInspection("영상이 없어 검사를 중지했습니다. 카메라 연결을 확인하세요."); return; }
        if (frame.Sequence == _inspectedFrame) return;
        var line = _lineId;
        var generation = _inspectionGeneration;
        var reference = _reference;
        _inspectionBusy = true;
        try
        {
            var analysis = await Task.Run(() => new OpenCvSharpFrameAnalyzer(new VisionInspector()).Inspect(line, frame.Jpeg, reference));
            if (IsDisposed || !_runningInspection || generation != _inspectionGeneration) return;
            // 기존 점수 기반 API를 유지합니다. 서버가 최종 판정·개수·이력을 저장합니다.
            var result = await _api.SimulateInspectionAsync(new InspectionRequest(line, analysis.ShapeScore, analysis.ColorScore, analysis.Result != InspectionResultType.Undetermined));
            _inspectedFrame = frame.Sequence;
            if (IsDisposed || generation != _inspectionGeneration) return;
            _result.Text = $"카메라 검사: {ResultText(result.Result)} · 형태 {result.ShapeScore:P0} / 색상 {result.ColorScore:P0}\r\n{result.Reason} · 촬영 {frame.ReceivedUtc.ToLocalTime():HH:mm:ss}";
            _inspectionState.Text = $"검사 중 · 마지막 저장 {DateTime.Now:HH:mm:ss} · 1.5초 간격 프레임 검사";
            await RefreshAsync(false);
        }
        catch (Exception ex)
        {
            if (!IsDisposed) StopInspection("검사 중지 · 분석/저장 실패: " + ex.Message + " · 자동 재전송하지 않습니다.");
        }
        finally { _inspectionBusy = false; if (!IsDisposed) _inspectionStart.Enabled = !_runningInspection; }
    }
}
