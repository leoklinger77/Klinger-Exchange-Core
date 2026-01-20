using System.Reactive.Subjects;

namespace KlingerExchange.Matching.Events;

public sealed class MetricsEventBus : IDisposable
{
    private readonly Subject<OrderMetricsEvent> _subject;
    private readonly IDisposable _subscription;

    public IObserver<OrderMetricsEvent> Events => _subject;

    public MetricsEventBus(Action<OrderMetricsEvent> onNext)
    {
        _subject = new Subject<OrderMetricsEvent>();
        _subscription = _subject.Subscribe(onNext);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subject?.Dispose();
    }
}
