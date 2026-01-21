namespace KlingerExchange.EventStore.StructModels;

public enum EventType : byte {
    OrderAccepted = 1,
    OrderFilled = 2,
    OrderPartiallyFilled = 3,
    OrderCancelled = 4,
    Trade = 5
}
