using KlingerExchange.Matching.Domain.Enums;

namespace KlingerExchange.Matching.Domain;

public sealed record ValidationResult {
    public ValidationStatus Status { get; init; }
    public string? RejectReason { get; init; }
    public int? RejectCode { get; init; }

    private ValidationResult(ValidationStatus status, string? rejectReason, int? rejectCode) {
        Status = status;
        RejectReason = rejectReason;
        RejectCode = rejectCode;
    }

    public static ValidationResult Valid() => new(ValidationStatus.Valid, null, null);

    public static ValidationResult Rejected(string reason, int code) =>
        new(ValidationStatus.Rejected, reason, code);
}
