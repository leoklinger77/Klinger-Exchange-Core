using KlingerBroker.Oms.Service;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using Serilog;

namespace KlingerBroker.Oms;

public sealed class FixInitiator : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<FixInitiator>();
    private IInitiator? _initiator;

    public IOrderRouter Router { get; }

    public FixInitiator(IOrderRouter router)
    {
        Router = router;
    }

    public void Start()
    {
        if (_initiator is not null)
            return;

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var settingsPath = Path.Combine(AppContext.BaseDirectory, "Oms", "manifest", "settings.cfg");
        if (!File.Exists(settingsPath))
            throw new FileNotFoundException($"FIX settings.cfg not found at: {settingsPath}");

        Logger.Information("Loading FIX settings from {Path}", settingsPath);

        var settings = new SessionSettings(settingsPath);
        IApplication application = new ClientFixApplication(Router);
        IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
        ILogFactory logFactory = new NullLogFactory();

        _initiator = new SocketInitiator(
            application,
            storeFactory,
            settings,
            logFactory);

        _initiator.Start();
        Logger.Information("FIX Initiator started");
    }

    public void Stop()
    {
        var initiator = _initiator;
        _initiator = null;

        try
        {
            initiator?.Stop();
            Logger.Information("FIX Initiator stopped");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error stopping FIX Initiator");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
