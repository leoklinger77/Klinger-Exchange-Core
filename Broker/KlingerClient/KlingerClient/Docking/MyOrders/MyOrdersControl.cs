using KlingerClient.Services;

namespace KlingerBroker.Ui.Docking.MyOrders;

public partial class MyOrdersControl : UserControl
{
    public MyOrdersControl()
    {
        InitializeComponent();
        SetupControls();
    }

    private void SetupControls()
    {
        // Enable double buffering
        typeof(DataGridView).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null, _grid, new object[] { true });
    }

    public void AddExecutionReport(ExecutionReportEvent er)
    {
        if (IsDisposed) return;

        _grid.Rows.Insert(0,
            DateTime.Now.ToString("HH:mm:ss.fff"),
            er.Symbol ?? "-",
            er.ClOrdId,
            er.OrderId ?? "-",
            er.ExecType,
            er.OrdStatus,
            er.CumQty,
            er.LeavesQty,
            er.LastPx?.ToString("0.00") ?? "-",
            er.LastQty?.ToString() ?? "-",
            er.Text ?? "");
    }
}
