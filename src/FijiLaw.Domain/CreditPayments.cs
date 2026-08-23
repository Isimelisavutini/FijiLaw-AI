namespace FijiLaw.Domain;

public sealed record CreditPaymentOrder(
    Guid Id,
    Guid UserId,
    string PlanCode,
    string PackageCode,
    int Credits,
    decimal AmountFjd,
    string Currency,
    string Provider,
    string Status,
    string? ProviderSessionId,
    string? CheckoutUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record PaymentCheckoutSession(string Provider, string SessionId, string CheckoutUrl);
public sealed record PaymentVerificationResult(bool Authorised, string State, string? ProviderReference = null);
