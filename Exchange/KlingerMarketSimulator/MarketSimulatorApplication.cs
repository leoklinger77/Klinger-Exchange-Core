using QuickFix;
using Serilog;

namespace KlingerSimulator;

public class MarketSimulatorApplication : IApplication {
    private readonly ILogger _log = Log.ForContext<MarketSimulatorApplication>();
    private SessionID? _sessionId;
    private bool _isConnected;

    public bool IsConnected => _isConnected;
    public SessionID? SessionId => _sessionId;

    public void OnCreate(SessionID sessionID) {
        _log.Information("OnCreate {SessionId}", sessionID);
        _sessionId = sessionID;
    }

    public void OnLogon(SessionID sessionID) {
        _log.Information("✓ OnLogon {SessionId} - CONNECTED!", sessionID);
        _isConnected = true;
    }

    public void OnLogout(SessionID sessionID) {
        _log.Warning("✗ OnLogout {SessionId} - DISCONNECTED", sessionID);
        _isConnected = false;
    }

    public void ToAdmin(Message message, SessionID sessionID) {
    }

    public void FromAdmin(Message message, SessionID sessionID) {
    }

    public void ToApp(Message message, SessionID sessionID) {
    }

    public void FromApp(Message message, SessionID sessionID) {
        var msgType = message.Header.GetString(QuickFix.Fields.Tags.MsgType);

        if (msgType == QuickFix.Fields.MsgType.EXECUTION_REPORT) {
            var execReport = (QuickFix.FIX41.ExecutionReport)message;
            var execType = execReport.ExecType.Value;

            if (execType == QuickFix.Fields.ExecType.FILL || execType == QuickFix.Fields.ExecType.PARTIAL_FILL) {
                _log.Debug("Fill received: {ClOrdId} {ExecType}",
                    execReport.ClOrdID.Value,
                    execType);
            }
        }
    }
}
