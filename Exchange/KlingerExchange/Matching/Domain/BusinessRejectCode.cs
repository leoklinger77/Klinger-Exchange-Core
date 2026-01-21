namespace KlingerExchange.Matching.Domain;

public static class BusinessRejectCode {
    
    public const int Other = 0;
    public const int UnknownID = 1;
    public const int UnknownSecurity = 2;
    public const int UnsupportedMessageType = 3;
    public const int ApplicationNotAvailable = 4;
    public const int ConditionallyRequiredFieldMissing = 5;
        
    public const int MarketClosed = 5001;
    public const int InstrumentSuspended = 5002;
    public const int PriceOutOfBand = 5003;
    public const int OrderTypeNotAllowed = 5004;
    public const int QuantityBelowMinimum = 5005;
    public const int QuantityAboveMaximum = 5006;
    public const int InvalidLotSize = 5007;
    public const int InstrumentNotFound = 5008;
}
