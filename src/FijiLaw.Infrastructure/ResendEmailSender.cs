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

    public Task<bool> SendVerificationAsync(string email, string token, CancellationToken ct = default)
    {
        var verifyUrl = $"{_publicWebUrl}/verify-email?token={Uri.EscapeDataString(token)}";
        return SendAsync(
            email,
            "Verify your FijiLaw AI email",
            "Verify your FijiLaw AI email",
            "Confirm your email address to use protected member features.",
            "Verify email",
            verifyUrl,
            "This link expires in 24 hours. If you did not create a FijiLaw AI account, you can ignore this email.",
            ct);
    }

    public Task<bool> SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
    {
        var resetUrl = $"{_publicWebUrl}/reset-password?token={Uri.EscapeDataString(token)}";
        return SendAsync(
            email,
            "Reset your FijiLaw AI password",
            "Reset your FijiLaw AI password",
            "A password reset was requested for your FijiLaw AI account.",
            "Reset password",
            resetUrl,
            "This link expires in 30 minutes. If you did not request a password reset, you can ignore this email.",
            ct);
    }

    private async Task<bool> SendAsync(string email, string subject, string heading, string intro, string actionLabel, string actionUrl, string footer, CancellationToken ct)
    {
        if (!IsConfigured) return false;

        var safeUrl = System.Net.WebUtility.HtmlEncode(actionUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(new
        {
            from = _from,
            to = new[] { email },
            subject,
            html = $"<!doctype html><html><body style=\"font-family:Arial,Helvetica,sans-serif;color:#16231c\"><h1>{heading}</h1><p>{intro}</p><p><a href=\"{safeUrl}\" style=\"display:inline-block;background:#173f2b;color:#fff;padding:12px 18px;text-decoration:none;border-radius:8px\">{actionLabel}</a></p><p>{footer}</p></body></html>",
            text = $"{heading}\n\n{intro}\n\n{actionUrl}\n\n{footer}"
        });

        using var response = await _http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }
}
