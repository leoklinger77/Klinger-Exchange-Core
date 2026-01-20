using KlingerClient.Services;
using KlingerBroker.Ui.Docking.MyOrders;
using WeifenLuo.WinFormsUI.Docking;

namespace KlingerClient.Docking.MyOrders;

public sealed class MyOrdersDock : DockContent
{
    private readonly MyOrdersControl _control;
    private readonly int _id;

    public MyOrdersDock(int id = 0)
    {
        _id = id;
        _control = new MyOrdersControl();

        Name = $"{DockContentIds.MyOrders}_{_id}";
        Text = _id > 0 ? $"MyOrders #{_id}" : "MyOrders";
        HideOnClose = true;

        _control.Dock = DockStyle.Fill;
        Controls.Add(_control);
    }

    public void AddExecutionReport(ExecutionReportEvent er)
    {
        _control.AddExecutionReport(er);
    }

    [Obsolete("Use AddExecutionReport(ExecutionReportEvent) instead")]
    public void AddInfo(string symbol, string side, int qty, decimal price, string status)
    {
        // Deprecated - kept for compatibility
    }

    protected override string GetPersistString() => $"{DockContentIds.MyOrders}:{_id}";
}
