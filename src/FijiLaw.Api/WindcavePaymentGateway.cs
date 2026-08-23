using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FijiLaw.Domain;

namespace FijiLaw.Api;

public sealed class WindcavePaymentGateway
{
    private readonly HttpClient _http;
    private readonly string? _username;
    private readonly string? _apiKey;
    private readonly string _apiBase;
    private readonly string? _publicApiUrl;
    private readonly string? _publicWebUrl;

    public WindcavePaymentGateway(HttpClient http, string? username, string? apiKey, string? apiBase, string? publicApiUrl, string? publicWebUrl)
    {
        _http = http;
        _username = username;
        _apiKey = apiKey;
        _apiBase = string.IsNullOrWhiteSpace(apiBase) ? "https://sec.windcave.com/api/v1" : apiBase.TrimEnd('/');
        _publicApiUrl = publicApiUrl?.TrimEnd('/');
        _publicWebUrl = publicWebUrl?.TrimEnd('/');
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_publicApiUrl) && !string.IsNullOrWhiteSpace(_publicWebUrl);
    public string ProviderName => "windcave";

    public async Task<PaymentCheckoutSession> CreateCheckoutAsync(CreditPaymentOrder order, string email, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Windcave payment gateway is not configured.");

        var payload = new
        {
            type = "purchase",
            amount = order.AmountFjd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            currency = order.Currency,
            merchantReference = order.Id.ToString("N"),
            language = "en",
            methods = new[] { "card" },
            callbackUrls = new
            {
                approved = $"{_publicWebUrl}/credits?payment=approved&order={order.Id}",
                declined = $"{_publicWebUrl}/credits?payment=declined&order={order.Id}",
                cancelled = $"{_publicWebUrl}/credits?payment=cancelled&order={order.Id}"
            },
            notificationUrl = $"{_publicApiUrl}/api/credits/payment/notify?orderId={order.Id}",
            customer = new { email }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiBase}/sessions");
        ApplyAuth(request);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Windcave checkout session could not be created ({(int)response.StatusCode}).");

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var sessionId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        string? checkoutUrl = null;
        if (root.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in links.EnumerateArray())
            {
                var rel = link.TryGetProperty("rel", out var relElement) ? relElement.GetString() : null;
                if (!string.Equals(rel, "hpp", StringComparison.OrdinalIgnoreCase)) continue;
                checkoutUrl = link.TryGetProperty("href", out var hrefElement) ? hrefElement.GetString() : null;
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(checkoutUrl))
            throw new InvalidOperationException("Windcave returned an incomplete checkout session.");

        return new PaymentCheckoutSession(ProviderName, sessionId!, checkoutUrl!);
    }

    public async Task<PaymentVerificationResult> VerifyAsync(CreditPaymentOrder order, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Windcave payment gateway is not configured.");
        if (string.IsNullOrWhiteSpace(order.ProviderSessionId)) return new PaymentVerificationResult(false, "missing-session");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBase}/sessions/{Uri.EscapeDataString(order.ProviderSessionId)}");
        ApplyAuth(request);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return new PaymentVerificationResult(false, $"provider-http-{(int)response.StatusCode}");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var state = root.TryGetProperty("state", out var stateElement) ? stateElement.GetString() ?? "unknown" : "unknown";
        var authorised = false;
        string? providerReference = order.ProviderSessionId;

        if (root.TryGetProperty("transactions", out var transactions) && transactions.ValueKind == JsonValueKind.Array)
        {
            foreach (var transaction in transactions.EnumerateArray())
            {
                if (transaction.TryGetProperty("authorised", out var authorisedElement) && authorisedElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    authorised = authorisedElement.GetBoolean();
                    if (authorised)
                    {
                        if (transaction.TryGetProperty("id", out var txId) && !string.IsNullOrWhiteSpace(txId.GetString())) providerReference = txId.GetString();
                        break;
                    }
                }
            }
        }

        return new PaymentVerificationResult(authorised, state, providerReference);
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_apiKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
