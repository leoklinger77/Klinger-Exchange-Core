using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using QuickFix;
using QuickFix.FIX41;
using Serilog;
using QfMessage = QuickFix.Message;

namespace KlingerExchange.Matching.Services;

/// <summary>
/// Async dispatcher for FIX ExecutionReports using System.Reactive.
/// Removes Session.SendToTarget from hot path (~80-100µs savings).
/// Uses dedicated thread scheduler for true async dispatch.
/// </summary>
public sealed class ExecutionReportDispatcher : IDisposable
{
    private readonly ILogger _log = Log.ForContext<ExecutionReportDispatcher>();
    private readonly Subject<PendingReport> _reportStream;
    private readonly EventLoopScheduler _scheduler;
    private readonly IDisposable _subscription;
    private long _dispatched;
    private long _errors;

    public ExecutionReportDispatcher()
    {
        _reportStream = new Subject<PendingReport>();
        
        // Dedicated thread for FIX message dispatch
        _scheduler = new EventLoopScheduler(ts => new Thread(ts)
        {
            Name = "FIX-Report-Dispatch",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        });
        
        // Subscribe with ObserveOn to move processing to dedicated thread
        _subscription = _reportStream
            .ObserveOn(_scheduler)
            .Subscribe(
                onNext: DispatchReport,
                onError: ex => _log.Error(ex, "ExecutionReportDispatcher stream error")
            );
        
        _log.Information("ExecutionReportDispatcher initialized with dedicated thread");
    }

    /// <summary>
    /// Enqueue an ExecutionReport for async dispatch. Returns immediately (~50ns).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnqueueReport(ExecutionReport report, SessionID sessionId, bool returnToPool)
    {
        _reportStream.OnNext(new PendingReport(report, null, sessionId, returnToPool));
    }

    /// <summary>
    /// Enqueue a generic Message (e.g., BusinessReject) for async dispatch. Returns immediately (~50ns).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnqueueReport(QfMessage message, SessionID sessionId, bool returnToPool)
    {
        _reportStream.OnNext(new PendingReport(null, message, sessionId, returnToPool));
    }

    /// <summary>
    /// Dispatch report on background thread
    /// </summary>
    private void DispatchReport(PendingReport pending)
    {
        try
        {
            if (pending.ExecutionReport != null)
            {
                Session.SendToTarget(pending.ExecutionReport, pending.SessionId);
                if (pending.ReturnToPool)
                {
                    ExecutionReportBuilderV2.ReturnToPool(pending.ExecutionReport);
                }
            }
            else if (pending.GenericMessage != null)
            {
                Session.SendToTarget(pending.GenericMessage, pending.SessionId);
            }
            
            Interlocked.Increment(ref _dispatched);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);
            _log.Error(ex, "Failed to dispatch ExecutionReport");
        }
    }

    public (long Dispatched, long Errors) GetStats() => (_dispatched, _errors);

    public void Dispose()
    {
        _reportStream.OnCompleted();
        _subscription.Dispose();
        _scheduler.Dispose();
        _reportStream.Dispose();
        _log.Information("ExecutionReportDispatcher disposed. Dispatched={Dispatched}, Errors={Errors}", 
            _dispatched, _errors);
    }

    private readonly record struct PendingReport(
        ExecutionReport? ExecutionReport, 
        QfMessage? GenericMessage, 
        SessionID SessionId, 
        bool ReturnToPool);
}
