using Serilog;
using System.Collections.Concurrent;

namespace KlingerSimulator.UI;

public sealed class InteractiveConsole : IDisposable {
    private readonly ILogger _log = Log.ForContext<InteractiveConsole>();
    private readonly MarketSimulator _simulator;
    private readonly ConcurrentQueue<string> _messageQueue = new();

    // Control state
    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private volatile int _intensity = 10;
    private volatile int _ordersPerSecond;
    private long _totalOrdersSent;
    private long _totalPairsGenerated;

    // UI refresh
    private Task? _uiTask;
    private CancellationTokenSource? _cts;

    // Change detection for smart refresh
    private int _lastOrdersPerSecond;
    private long _lastTotalOrders;
    private long _lastTotalPairs;
    private int _lastIntensity;
    private bool _lastPausedState;
    private bool _isConnected;
    private int _lastMessageCount;
    private bool _forceRedraw = true;

    public InteractiveConsole(MarketSimulator simulator) {
        _simulator = simulator;
    }

    public void Start() {
        _log.Information("Starting Interactive Console");

        Console.Clear();
        Console.CursorVisible = false;

        // Set buffer size to window size to prevent scrolling and flickering
        try {
            var width = Math.Max(80, Console.WindowWidth);
            var height = Math.Max(24, Console.WindowHeight);
            Console.SetBufferSize(width, height);
        } catch { /* Ignore if fails */ }

        _isRunning = true;
        _cts = new CancellationTokenSource();

        // Start UI refresh task
        _uiTask = Task.Run(() => UILoop(_cts.Token));

        // Start input handler
        Task.Run(() => InputLoop(_cts.Token));

        _log.Information("Interactive Console started");
    }

    public void Stop() {
        _isRunning = false;
        _cts?.Cancel();
        _uiTask?.Wait(TimeSpan.FromSeconds(2));

        Console.CursorVisible = true;
        Console.Clear();

        _log.Information("Interactive Console stopped");
    }

    public void UpdateStats(int ordersPerSecond, long totalOrders, long totalPairs, bool isConnected) {
        _ordersPerSecond = ordersPerSecond;
        _totalOrdersSent = totalOrders;
        _totalPairsGenerated = totalPairs;
        _isConnected = isConnected;
    }

    public void LogMessage(string message) {
        _messageQueue.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}");

        // Keep only last 100 messages
        while (_messageQueue.Count > 100) {
            _messageQueue.TryDequeue(out _);
        }
    }

    private async Task UILoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _isRunning) {
            try {
                var currentMessageCount = _messageQueue.Count;
                var hasChanges = _forceRedraw ||
                    _ordersPerSecond != _lastOrdersPerSecond ||
                    _totalOrdersSent != _lastTotalOrders ||
                    _totalPairsGenerated != _lastTotalPairs ||
                    _intensity != _lastIntensity ||
                    _isPaused != _lastPausedState ||
                    currentMessageCount != _lastMessageCount;

                if (hasChanges) {
                    RenderUI();

                    _lastOrdersPerSecond = _ordersPerSecond;
                    _lastTotalOrders = _totalOrdersSent;
                    _lastTotalPairs = _totalPairsGenerated;
                    _lastIntensity = _intensity;
                    _lastPausedState = _isPaused;
                    _lastMessageCount = currentMessageCount;
                    _forceRedraw = false;
                }

                await Task.Delay(100, ct);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _log.Error(ex, "Error in UI loop");
            }
        }
    }

    private void RenderUI() {
        Console.Clear();
        Console.SetCursorPosition(0, 0);

        var width = Math.Max(80, Console.WindowWidth);
        var height = Math.Max(24, Console.WindowHeight);

        // Header
        PrintLine("╔" + new string('═', Math.Max(0, width - 2)) + "╗", ConsoleColor.Cyan);
        var titleText = " KLINGER MARKET SIMULATOR - Interactive Control";
        PrintLine("║" + titleText + new string(' ', Math.Max(0, width - titleText.Length - 2)) + "║", ConsoleColor.Cyan);
        PrintLine("╠" + new string('═', Math.Max(0, width - 2)) + "╣", ConsoleColor.Cyan);

        // Commands Section
        var commandsHeader = " COMMANDS:";
        PrintLine("║" + commandsHeader + new string(' ', Math.Max(0, width - commandsHeader.Length - 2)) + "║", ConsoleColor.Yellow);
        var cmd1 = "  [SPACE] Pause/Resume  │  [+] Increase Speed  │  [-] Decrease Speed  │  [Q] Quit";
        var cmd2 = "  [1-9]   Set Intensity │  [R] Reset Stats     │  [S] Show Status";
        PrintLine("║" + cmd1 + new string(' ', Math.Max(0, width - cmd1.Length - 2)) + "║", ConsoleColor.White);
        PrintLine("║" + cmd2 + new string(' ', Math.Max(0, width - cmd2.Length - 2)) + "║", ConsoleColor.White);
        PrintLine("╠" + new string('═', Math.Max(0, width - 2)) + "╣", ConsoleColor.Cyan);

        // Status Section
        var status = _isPaused ? "PAUSED" : "RUNNING";
        var statusColor = _isPaused ? ConsoleColor.Yellow : ConsoleColor.Green;

        var statusHeader = " STATUS:";
        PrintLine("║" + statusHeader + new string(' ', Math.Max(0, width - statusHeader.Length - 2)) + "║", ConsoleColor.Yellow);

        var statusLine1 = $"  State: {status}  │  Intensity: {_intensity}%  │  Orders/sec: {_ordersPerSecond,6}";
        var padding1 = new string(' ', Math.Max(0, width - statusLine1.Length - 2));
        PrintColoredLine("║  State: ", status, $"  │  Intensity: {_intensity}%  │  Orders/sec: {_ordersPerSecond,6}{padding1}║", statusColor);

        var statusLine2 = $"  Total Orders: {_totalOrdersSent,12:N0}  │  Total Pairs: {_totalPairsGenerated,12:N0}";
        PrintLine("║" + statusLine2 + new string(' ', Math.Max(0, width - statusLine2.Length - 2)) + "║", ConsoleColor.White);
        PrintLine("╠" + new string('═', Math.Max(0, width - 2)) + "╣", ConsoleColor.Cyan);

        // Performance Graph
        var perfHeader = " PERFORMANCE:";
        PrintLine("║" + perfHeader + new string(' ', Math.Max(0, width - perfHeader.Length - 2)) + "║", ConsoleColor.Yellow);
        RenderGraph(width - 4);
        PrintLine("╠" + new string('═', Math.Max(0, width - 2)) + "╣", ConsoleColor.Cyan);

        // Recent Messages
        var activityHeader = " ACTIVITY LOG:";
        PrintLine("║" + activityHeader + new string(' ', Math.Max(0, width - activityHeader.Length - 2)) + "║", ConsoleColor.Yellow);

        // Calculate available lines for messages
        var availableLines = Math.Max(1, height - 16);
        RenderMessages(availableLines, width - 4);

        PrintLine("╚" + new string('═', Math.Max(0, width - 2)) + "╝", ConsoleColor.Cyan);
    }

    private void RenderGraph(int width) {
        width = Math.Max(10, width);

        var barWidth = (int)(width * (_intensity / 100.0));
        var bar = new string('█', Math.Max(0, Math.Min(barWidth, width)));
        var empty = new string('░', Math.Max(0, width - bar.Length));

        Console.Write("║ ");

        if (_intensity < 30)
            Console.ForegroundColor = ConsoleColor.Green;
        else if (_intensity < 70)
            Console.ForegroundColor = ConsoleColor.Yellow;
        else
            Console.ForegroundColor = ConsoleColor.Red;

        Console.Write(bar);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(empty);
        Console.ResetColor();
        Console.WriteLine(" ║");
    }

    private void RenderMessages(int maxLines, int width) {
        maxLines = Math.Max(1, maxLines);
        width = Math.Max(10, width);

        var messages = _messageQueue.ToArray();
        var startIndex = Math.Max(0, messages.Length - maxLines);

        for (int i = 0; i < maxLines; i++) {
            var index = startIndex + i;
            if (index < messages.Length) {
                var msg = messages[index];
                if (msg.Length > width)
                    msg = msg.Substring(0, Math.Max(0, width - 3)) + "...";

                var msgLine = " " + msg;
                var padding = new string(' ', Math.Max(0, width - msg.Length));
                PrintLine("║" + msgLine + padding + " ║", ConsoleColor.Gray);
            } else {
                PrintLine("║ " + new string(' ', Math.Max(0, width)) + " ║", ConsoleColor.Gray);
            }
        }
    }

    private void PrintLine(string text, ConsoleColor color) {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private void PrintColoredLine(string prefix, string colored, string suffix, ConsoleColor color) {
        Console.Write(prefix);
        Console.ForegroundColor = color;
        Console.Write(colored);
        Console.ResetColor();
        Console.WriteLine(suffix);
    }

    private async Task InputLoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _isRunning) {
            try {
                if (System.Console.KeyAvailable) {
                    var key = System.Console.ReadKey(true);
                    HandleInput(key);
                }

                await Task.Delay(50, ct);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _log.Error(ex, "Error in input loop");
            }
        }
    }

    private void HandleInput(ConsoleKeyInfo key) {
        switch (key.Key) {
            case ConsoleKey.Spacebar:
                _isPaused = !_isPaused;
                LogMessage(_isPaused ? "⏸️  PAUSED" : "▶️  RESUMED");
                break;

            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                if (_intensity < 100) {
                    _intensity = Math.Min(100, _intensity + 10);
                    LogMessage($"🔼 Intensity increased to {_intensity}%");
                }
                break;

            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                if (_intensity > 0) {
                    _intensity = Math.Max(0, _intensity - 10);
                    LogMessage($"🔽 Intensity decreased to {_intensity}%");
                }
                break;

            case ConsoleKey.D1: SetIntensity(10); break;
            case ConsoleKey.D2: SetIntensity(20); break;
            case ConsoleKey.D3: SetIntensity(30); break;
            case ConsoleKey.D4: SetIntensity(40); break;
            case ConsoleKey.D5: SetIntensity(50); break;
            case ConsoleKey.D6: SetIntensity(60); break;
            case ConsoleKey.D7: SetIntensity(70); break;
            case ConsoleKey.D8: SetIntensity(80); break;
            case ConsoleKey.D9: SetIntensity(90); break;
            case ConsoleKey.D0: SetIntensity(100); break;

            case ConsoleKey.R:
                _totalOrdersSent = 0;
                _totalPairsGenerated = 0;
                LogMessage("🔄 Statistics reset");
                break;

            case ConsoleKey.S:
                LogMessage($"📊 Status: {(_isPaused ? "PAUSED" : "RUNNING")} | Intensity: {_intensity}% | Orders/sec: {_ordersPerSecond}");
                break;

            case ConsoleKey.Q:
                LogMessage("👋 Shutting down...");
                _isRunning = false;
                _simulator.Stop();
                break;
        }
    }

    private void SetIntensity(int value) {
        _intensity = value;
        LogMessage($"🎚️  Intensity set to {_intensity}%");
    }

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;
    public int Intensity => _intensity;

    public void Dispose() {
        Stop();
        _cts?.Dispose();
    }
}
