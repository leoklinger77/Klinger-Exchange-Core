namespace KlingerExchange.Matching.Validation;

/// <summary>
/// Status de validação de ordem
/// </summary>
public enum ValidationStatus
{
    Valid,
    Rejected
}

/// <summary>
/// Resultado de validação de ordem
/// </summary>
public sealed record ValidationResult
{
    public ValidationStatus Status { get; init; }
    public string? RejectReason { get; init; }
    public int? RejectCode { get; init; }

    private ValidationResult(ValidationStatus status, string? rejectReason, int? rejectCode)
    {
        Status = status;
        RejectReason = rejectReason;
        RejectCode = rejectCode;
    }

    public static ValidationResult Valid() => new(ValidationStatus.Valid, null, null);

    public static ValidationResult Rejected(string reason, int code) => 
        new(ValidationStatus.Rejected, reason, code);
}

/// <summary>
/// FIX Business Reject Codes (FIX 4.1 standard + custom)
/// </summary>
public static class BusinessRejectCode
{
    // FIX Standard codes (380=BusinessRejectReason)
    public const int Other = 0;
    public const int UnknownID = 1;
    public const int UnknownSecurity = 2;
    public const int UnsupportedMessageType = 3;
    public const int ApplicationNotAvailable = 4;
    public const int ConditionallyRequiredFieldMissing = 5;
    
    // Custom codes (5000+)
    public const int MarketClosed = 5001;
    public const int InstrumentSuspended = 5002;
    public const int PriceOutOfBand = 5003;
    public const int OrderTypeNotAllowed = 5004;
    public const int QuantityBelowMinimum = 5005;
    public const int QuantityAboveMaximum = 5006;
    public const int InvalidLotSize = 5007;
    public const int InstrumentNotFound = 5008;
}
