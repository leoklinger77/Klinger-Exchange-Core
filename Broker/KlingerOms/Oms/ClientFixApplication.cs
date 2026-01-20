using KlingerBroker.Oms.Dtos;
using KlingerBroker.Oms.Service;
using QuickFix;
using QuickFix.Fields;
using Serilog;
using FixMessage = QuickFix.Message;

namespace KlingerBroker.Oms;

public sealed class ClientFixApplication : IApplication {
    private readonly ILogger _logger = Log.ForContext<ClientFixApplication>();
    private readonly IOrderRouter _router;

    public ClientFixApplication(IOrderRouter router) {
        _router = router;
    }

    public void FromAdmin(FixMessage message, SessionID sessionID) {
        _logger.Information("FromAdmin: {SessionID} {MsgType}", sessionID, GetMsgType(message));
    }

    public void FromApp(FixMessage message, SessionID sessionID) {
        var msgType = GetMsgType(message);
        _logger.Information("FromApp: {SessionID} {MsgType}", sessionID, msgType);

        // 35=8 ExecutionReport
        if (msgType == MsgType.EXECUTION_REPORT) {
            var er = ParseExecutionReport(message);
            _router.OnExecutionReportFromFix(er);
        }
    }

    public void OnCreate(SessionID sessionID) {
        _logger.Information("OnCreate: {SessionID}", sessionID);
        _router.SessionId = sessionID;
    }

    public void OnLogon(SessionID sessionID) {
        _logger.Information("OnLogon: {SessionID}", sessionID);
        _router.SessionId = sessionID;
        _router.SetConnected(true, sessionID.ToString());
    }

    public void OnLogout(SessionID sessionID) {
        _logger.Information("OnLogout: {SessionID}", sessionID);
        _router.SetConnected(false, sessionID.ToString());
    }

    public void ToAdmin(FixMessage message, SessionID sessionID) {
        _logger.Debug("ToAdmin: {SessionID} {MsgType}", sessionID, GetMsgType(message));
    }

    public void ToApp(FixMessage message, SessionID sessionID) {
        _logger.Debug("ToApp: {SessionID} {MsgType}", sessionID, GetMsgType(message));
    }

    private static ExecutionReportEvent ParseExecutionReport(FixMessage msg) {
        string GetStringOrDefault(int tag) {
            try { return msg.GetString(tag); } catch { return string.Empty; }
        }
        int GetIntOrDefault(int tag) {
            try { return msg.GetInt(tag); } catch { return 0; }
        }
        decimal? GetDecimalOrNull(int tag) {
            try { return msg.GetDecimal(tag); } catch { return null; }
        }
        int? GetIntOrNull(int tag) { 
            try { return msg.GetInt(tag); } catch { return null; } 
        }

        return new ExecutionReportEvent(
            ClOrdId: GetStringOrDefault(Tags.ClOrdID),
            OrderId: GetStringOrDefault(Tags.OrderID),
            Symbol: GetStringOrDefault(Tags.Symbol),
            ExecType: GetStringOrDefault(Tags.ExecType),
            OrdStatus: GetStringOrDefault(Tags.OrdStatus),
            CumQty: GetIntOrDefault(Tags.CumQty),
            LeavesQty: GetIntOrDefault(Tags.LeavesQty),
            LastPx: GetDecimalOrNull(Tags.LastPx),
            LastQty: GetIntOrNull(Tags.LastShares),
            Text: GetStringOrDefault(Tags.Text)
        );
    }

    private static string GetMsgType(FixMessage msg) {
        try { 
            return msg.Header.GetString(Tags.MsgType); 
        } catch { return "?"; }
    }
}
