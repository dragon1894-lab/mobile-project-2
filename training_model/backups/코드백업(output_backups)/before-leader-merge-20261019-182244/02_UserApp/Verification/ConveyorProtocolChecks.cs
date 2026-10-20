using ProductionLine.UserApp;

namespace ProductionLine.Verification;

internal static class ConveyorProtocolChecks
{
    public static async Task RunAsync()
    {
        var transport = new SimulatedTransport();
        using var controller = new ConveyorSerialController(transport);
        await controller.HandshakeAsync();
        Check(!controller.IsRunning, "conveyor identity handshake without moving hardware");
        var run = controller.RunAsync();
        var stop = controller.StopAsync();
        await Task.WhenAll(run, stop);
        Check(!controller.IsRunning && transport.Commands.TakeLast(2).SequenceEqual(["RUN", "STOP"]),
            "RUN and STOP preserve order and require device acknowledgments");
        await controller.RunAsync();
        Check(controller.IsRunning, "RUNNING acknowledgment updates state");
        transport.MotorRunning = false;
        await controller.PingAsync();
        Check(!controller.IsRunning, "heartbeat sees device watchdog stop");
        transport.FailCommand = true;
        try { await controller.StopAsync(); throw new InvalidOperationException("Expected STOP failure"); }
        catch (IOException) { Console.WriteLine("PASS: device command error is not treated as successful stop"); }

        using var silent = new ConveyorSerialController(new SilentTransport());
        try { await silent.HandshakeAsync(); throw new InvalidOperationException("Expected identity failure"); }
        catch (TimeoutException) { Console.WriteLine("PASS: incompatible firmware cannot pass handshake"); }
        var oldTransport = new SimulatedTransport { Identity = "PL_CONVEYOR_V1 STOPPED" };
        using var oldController = new ConveyorSerialController(oldTransport);
        try { await oldController.HandshakeAsync(); throw new InvalidOperationException("Expected old PWM firmware rejection"); }
        catch (IOException) { Check(!oldTransport.Commands.Contains("RUN"), "old PWM firmware rejected before motor RUN"); }
        var wrongPins = new SimulatedTransport { Identity = "PL_CONVEYOR_PUL_DIR_V2 STOPPED" };
        using var wrongPinController = new ConveyorSerialController(wrongPins);
        try { await wrongPinController.HandshakeAsync(); throw new InvalidOperationException("Expected wrong pin firmware rejection"); }
        catch (IOException ex) { Check(!wrongPins.Commands.Contains("RUN") && ex.Message.Contains("5번") && ex.Message.Contains("6번"),
            "previous 2/3 pin firmware is rejected with correct upload guidance before RUN"); }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class SimulatedTransport : IConveyorTransport
    {
        private readonly Queue<string> _responses = new();
        public List<string> Commands { get; } = [];
        public bool MotorRunning { get; set; }
        public bool FailCommand { get; set; }
        public string Identity { get; set; } = "PL_CONVEYOR_PUL_DIR_V3 STOPPED";
        public void WriteLine(string value)
        {
            Commands.Add(value);
            if (FailCommand) { _responses.Enqueue("ERROR SYNTHETIC"); return; }
            if (value == "RUN") MotorRunning = true;
            if (value == "STOP") MotorRunning = false;
            _responses.Enqueue(value == "HELLO" ? Identity : MotorRunning ? "RUNNING" : "STOPPED");
        }
        public string ReadLine()
        {
            if (_responses.Count > 0) return _responses.Dequeue();
            Thread.Sleep(25);
            throw new TimeoutException();
        }
        public void Dispose() { }
    }

    private sealed class SilentTransport : IConveyorTransport
    {
        public void WriteLine(string value) { }
        public string ReadLine() { Thread.Sleep(25); throw new TimeoutException(); }
        public void Dispose() { }
    }
}
