namespace ProductionLine.UserApp;

public sealed partial class UserMainForm
{
    private IConveyorController? _conveyor;
    private readonly ComboBox _conveyorPort = new() { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _conveyorConnect = new() { Text = "벨트 연결", AutoSize = true };
    private readonly Button _conveyorRun = new() { Text = "벨트 구동", AutoSize = true, Enabled = false };
    private readonly Label _conveyorState = new() { AutoSize = true, Text = "벨트 미연결 · 처음에는 아두이노 설정·업로드, 이후에는 COM 선택 후 벨트 연결을 누르세요." };
    private readonly System.Windows.Forms.Timer _conveyorHeartbeat = new() { Interval = 500 };
    private bool _conveyorCommandBusy;
    private bool _conveyorHeartbeatBusy;
    private Control BuildConveyorControls()
    {
        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
        controls.Controls.Add(new Label { Text = "아두이노 COM", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        controls.Controls.Add(_conveyorPort);
        controls.Controls.Add(Button("포트 갱신", RefreshConveyorPorts));
        controls.Controls.Add(_conveyorConnect);
        controls.Controls.Add(Button("벨트 연결 해제", async () => await DisconnectConveyorAsync()));
        controls.Controls.Add(_conveyorRun);
        _arduinoSetup.Click += async (_, _) => await OpenArduinoSetupAsync();
        _conveyorConnect.Click += async (_, _) => await ConnectConveyorAsync();
        _conveyorRun.Click += async (_, _) => await RunConveyorAsync();
        _conveyorHeartbeat.Tick += async (_, _) => await PingConveyorAsync();
        RefreshConveyorPorts();
        return controls;
    }

    private void RefreshConveyorPorts()
    {
        if (_conveyor != null || _conveyorCommandBusy) return;
        try
        {
            var previous = _conveyorPort.SelectedItem as string ?? ArduinoPortSettings.Load();
            _conveyorPort.DataSource = ConveyorSerialController.GetPortNames();
            _conveyorPort.SelectedIndex = -1;
            if (previous != null && _conveyorPort.Items.Contains(previous)) _conveyorPort.SelectedItem = previous;
        }
        catch (Exception ex) { _conveyorState.Text = "COM 포트 조회 실패: " + ex.Message; }
    }

    private async Task ConnectConveyorAsync()
    {
        if (_conveyor != null || _conveyorCommandBusy) return;
        if (_conveyorPort.SelectedItem is not string port)
        { _conveyorState.Text = "Mega 2560의 USB를 연결하고 COM 포트를 선택하세요. 처음에는 아두이노 설정·업로드를 누르세요."; return; }
        _conveyorCommandBusy = true;
        _conveyorConnect.Enabled = _conveyorPort.Enabled = false;
        _conveyorState.Text = "아두이노 연결 확인 중...";
        try
        {
            var controller = await ConveyorSerialController.ConnectAsync(port);
            if (IsDisposed) { controller.Dispose(); return; }
            _conveyor = controller;
            ArduinoPortSettings.Save(port);
            _conveyorRun.Enabled = true;
            _conveyorState.Text = $"{port} 연결됨 · 정지 확인 · 검사 시작 후 벨트 구동을 누르세요.";
            _conveyorHeartbeat.Start();
        }
        catch (Exception ex) { if (!IsDisposed) _conveyorState.Text = "벨트 연결 실패: " + ex.Message; }
        finally
        {
            _conveyorCommandBusy = false;
            if (!IsDisposed) _conveyorConnect.Enabled = _conveyorPort.Enabled = _conveyor == null;
        }
    }

    private async Task RunConveyorAsync()
    {
        if (_conveyor == null || _conveyorCommandBusy || _arduinoSetupBusy) return;
        _conveyorCommandBusy = true;
        _conveyorRun.Enabled = false;
        try
        {
            await _conveyor.RunAsync();
            if (!IsDisposed) _conveyorState.Text = "PUL/DIR 구동 응답 확인 · 연속 검출 · 인식돼도 벨트는 계속 이동합니다.";
        }
        catch (Exception ex) { if (!IsDisposed) FailConveyor("구동 응답 확인 실패: " + ex.Message); }
        finally { _conveyorCommandBusy = false; if (!IsDisposed) _conveyorRun.Enabled = _conveyor != null; }
    }

    private async Task StopConveyorAsync(string message)
    {
        var controller = _conveyor;
        if (controller == null) return;
        try
        {
            await controller.StopAsync();
            if (!IsDisposed && ReferenceEquals(controller, _conveyor)) _conveyorState.Text = message;
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                FailConveyor("벨트 정지 응답을 확인하지 못했습니다: " + ex.Message);
                StopInspection("정지 응답 확인 실패 · 벨트 실제 상태를 확인하세요.");
            }
            throw;
        }
    }

    private async Task PingConveyorAsync()
    {
        if (_conveyor == null || _conveyorHeartbeatBusy || _conveyorCommandBusy) return;
        var controller = _conveyor;
        _conveyorHeartbeatBusy = true;
        try { await controller.PingAsync(); }
        catch (Exception ex)
        {
            if (!IsDisposed && ReferenceEquals(controller, _conveyor))
            {
                FailConveyor("아두이노 통신 오류 · 통신 중단 3초 후 정지: " + ex.Message);
                StopInspection("벨트 통신 오류로 검사 중지");
            }
        }
        finally { _conveyorHeartbeatBusy = false; }
    }

    private async Task DisconnectConveyorAsync()
    {
        if (_conveyorCommandBusy || _conveyor == null) return;
        var controller = _conveyor;
        _conveyorCommandBusy = true;
        _conveyorHeartbeat.Stop();
        var confirmed = false;
        try { await StopConveyorAsync("벨트 정지 응답 확인"); confirmed = true; }
        catch (Exception) { /* 연결 해제 후에도 펌웨어 통신 타임아웃이 적용됩니다. */ }
        finally
        {
            controller.Dispose();
            _conveyor = null;
            _conveyorCommandBusy = false;
            if (!IsDisposed)
            {
                _conveyorRun.Enabled = false;
                _conveyorConnect.Enabled = _conveyorPort.Enabled = true;
                _conveyorState.Text = confirmed ? "벨트 정지 응답 확인 후 연결 해제" : "벨트 연결 해제 · 정지 응답 미확인 · 실제 상태를 확인하세요.";
            }
        }
    }

    private void FailConveyor(string message)
    {
        _conveyorHeartbeat.Stop();
        _conveyor?.Dispose();
        _conveyor = null;
        _conveyorRun.Enabled = false;
        _conveyorConnect.Enabled = _conveyorPort.Enabled = true;
        _conveyorState.Text = message;
    }

}
