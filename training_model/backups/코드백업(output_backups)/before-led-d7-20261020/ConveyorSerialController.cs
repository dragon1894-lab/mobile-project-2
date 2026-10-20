using System.Diagnostics;
using System.IO.Ports;

namespace ProductionLine.UserApp;

internal interface IConveyorController : IDisposable
{
    bool IsRunning { get; }
    Task RunAsync();
    Task StopAsync();
    Task PingAsync();
}

internal interface IConveyorTransport : IDisposable
{
    void WriteLine(string value);
    string ReadLine();
}

/// <summary>모든 명령은 아두이노의 응답을 확인한 후에만 성공 처리합니다.</summary>
internal sealed class ConveyorSerialController(IConveyorTransport transport) : IConveyorController
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _commands = new(1, 1);
    private bool _disposed;
    public bool IsRunning { get; private set; }

    public static string[] GetPortNames() => SerialPort.GetPortNames().OrderBy(value => value).ToArray();

    public static async Task<ConveyorSerialController> ConnectAsync(string portName)
    {
        var serial = new SerialPort(portName, 9600)
        {
            NewLine = "\n", ReadTimeout = 150, WriteTimeout = 500,
            DtrEnable = true, RtsEnable = false
        };
        ConveyorSerialController? controller = null;
        try
        {
            await Task.Run(serial.Open);
            // 아두이노는 USB 시리얼 연결 시 리셋될 수 있으므로 부팅을 기다립니다.
            await Task.Delay(2000);
            controller = new ConveyorSerialController(new SerialTransport(serial));
            await controller.HandshakeAsync();
            await controller.StopAsync();
            return controller;
        }
        catch
        {
            if (controller != null) controller.Dispose();
            else serial.Dispose();
            throw;
        }
    }

    internal Task HandshakeAsync() => SendAsync("HELLO", value => value is "PL_CONVEYOR_PUL_DIR_V3 STOPPED" or "PL_CONVEYOR_PUL_DIR_V3 RUNNING");
    public Task RunAsync() => SendAsync("RUN", value => value == "RUNNING");
    public Task StopAsync() => SendAsync("STOP", value => value == "STOPPED");
    public Task PingAsync() => SendAsync("PING", value => value is "RUNNING" or "STOPPED");

    private async Task SendAsync(string command, Func<string, bool> accepts)
    {
        await _commands.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                lock (_gate)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    transport.WriteLine(command);
                    var timeout = Stopwatch.StartNew();
                    while (timeout.ElapsedMilliseconds < 1200)
                    {
                        string reply;
                        try { reply = transport.ReadLine().Trim(); }
                        catch (TimeoutException) { continue; }
                        if (reply.StartsWith("ERROR")) throw new IOException("아두이노 명령 오류: " + reply);
                        if (command == "HELLO" && reply.StartsWith("PL_CONVEYOR_") && !reply.StartsWith("PL_CONVEYOR_PUL_DIR_V3 "))
                            throw new IOException("이전 핀 설정의 펌웨어입니다. 아두이노 설정·업로드에서 PUL 5번 / DIR 6번 / 80µs 코드를 다시 업로드하세요. 수신: " + reply);
                        if (!accepts(reply)) continue;
                        IsRunning = reply == "RUNNING" || reply.EndsWith(" RUNNING");
                        return;
                    }
                    throw new TimeoutException($"아두이노 {command} 응답을 확인하지 못했습니다. 아두이노 설정·업로드에서 컨베이어 코드를 업로드했는지 확인하세요.");
                }
            });
        }
        finally { _commands.Release(); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            // 종료 시 정지를 요청합니다. 전달되지 않아도 펌웨어의 통신 타임아웃이 정지시킵니다.
            try { transport.WriteLine("STOP"); } catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException) { }
            transport.Dispose();
        }
    }

    private sealed class SerialTransport(SerialPort serial) : IConveyorTransport
    {
        public void WriteLine(string value) => serial.WriteLine(value);
        public string ReadLine() => serial.ReadLine();
        public void Dispose() => serial.Dispose();
    }
}
