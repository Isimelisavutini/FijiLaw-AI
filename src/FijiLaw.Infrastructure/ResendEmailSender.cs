using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FijiLaw.Infrastructure;

public sealed class ResendEmailSender
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string? _from;
    private readonly string? _publicWebUrl;

    public ResendEmailSender(HttpClient http, string? apiKey, string? from, string? publicWebUrl)
    {
        _http = http;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _from = string.IsNullOrWhiteSpace(from) ? null : from.Trim();
        _publicWebUrl = string.IsNullOrWhiteSpace(publicWebUrl) ? null : publicWebUrl.Trim().TrimEnd('/');
    }

    public bool IsConfigured => _apiKey is not null && _from is not null && _publicWebUrl is not null;

    public async Task<bool> SendVerificationAsync(string email, string token, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        var verifyUrl = $"{_publicWebUrl}/verify-email?token={Uri.EscapeDataString(token)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(new
        {
            from = _from,
            to = new[] { email },
            subject = "Verify your FijiLaw AI email",
            html = $"<!doctype html><html><body style=\"font-family:Arial,Helvetica,sans-serif;color:#16231c\"><h1>Verify your FijiLaw AI email</h1><p>Confirm your email address to use protected member features.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(verifyUrl)}\" style=\"display:inline-block;background:#173f2b;color:#fff;padding:12px 18px;text-decoration:none;border-radius:8px\">Verify email</a></p><p>This link expires in 24 hours. If you did not create a FijiLaw AI account, you can ignore this email.</p></body></html>",
            text = $"Verify your FijiLaw AI email: {verifyUrl}\n\nThis link expires in 24 hours."
        });

        using var response = await _http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }
}
