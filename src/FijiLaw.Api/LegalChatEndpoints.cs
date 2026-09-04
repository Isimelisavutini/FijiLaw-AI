using System.Text.RegularExpressions;
using FijiLaw.AI;
using FijiLaw.Domain;
using FijiLaw.Infrastructure;
using Npgsql;

namespace FijiLaw.Api;

public sealed record DashboardLegalChatRequest(Guid? ConversationId, string Message, bool QwenDataProcessingConsent);

public static class LegalChatEndpoints
{
    public static WebApplication MapLegalChatEndpoints(this WebApplication app, string? databaseUrl)
    {
        app.MapGet("/api/chat/conversations", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var access = await ResolveDashboardMemberAsync(request, context, databaseUrl, ct);
            if (access.Error is not null) return access.Error;
            var store = context.RequestServices.GetRequiredService<PostgresLegalChatStore>();
            return Results.Ok(new { items = await store.ListAsync(access.Member!.UserId, ct) });
        }).RequireRateLimiting("chat");

        app.MapGet("/api/chat/conversations/{conversationId:guid}", async (Guid conversationId, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var access = await ResolveDashboardMemberAsync(request, context, databaseUrl, ct);
            if (access.Error is not null) return access.Error;
            var store = context.RequestServices.GetRequiredService<PostgresLegalChatStore>();
            var messages = await store.GetMessagesAsync(access.Member!.UserId, conversationId, ct);
            return messages is null ? Results.NotFound(new { error = "Conversation not found." }) : Results.Ok(new { conversationId, messages });
        }).RequireRateLimiting("chat");

        app.MapPost("/api/chat/messages", async (DashboardLegalChatRequest body, HttpRequest request, HttpContext context, ILegalAgent agent, ILanguageModelProvider modelProvider, CancellationToken ct) =>
        {
            var access = await ResolveDashboardMemberAsync(request, context, databaseUrl, ct);
            if (access.Error is not null) return access.Error;
            if (!body.QwenDataProcessingConsent)
                return Results.BadRequest(new { error = "Consent is required before legal chat content can be sent to Qwen in Singapore and stored in FijiLaw chat history." });

            var member = access.Member!;
            var message = body.Message?.Trim();
            if (string.IsNullOrWhiteSpace(message)) return Results.BadRequest(new { error = "Message is required." });
            if (message.Length > 8000) return Results.BadRequest(new { error = "Message must not exceed 8,000 characters." });

            var chatStore = context.RequestServices.GetRequiredService<PostgresLegalChatStore>();
            IReadOnlyList<LegalChatMessage>? previousMessages = null;
            if (body.ConversationId is not null)
            {
                previousMessages = await chatStore.GetMessagesAsync(member.UserId, body.ConversationId.Value, ct);
                if (previousMessages is null) return Results.NotFound(new { error = "Conversation not found." });
            }
            var modelInput = BuildModelInput(message, previousMessages ?? Array.Empty<LegalChatMessage>());

            var credits = FijiLawCreditCatalog.PriceFor(FijiLawCreditCatalog.DashboardLegalChat);
            var correlationId = Guid.NewGuid().ToString("N");
            var walletStore = context.RequestServices.GetRequiredService<ICreditWalletStore>();
            var reservation = await walletStore.ReserveAsync(member.UserId, member.PlanCode, credits, FijiLawCreditCatalog.DashboardLegalChat, correlationId, ct);
            if (reservation is null)
            {
                var wallet = await walletStore.GetWalletAsync(member.UserId, member.PlanCode, ct);
                return Results.Json(new { error = "Not enough FijiLaw Credits for this legal chat message.", creditsRequired = credits, balance = wallet.Balance, buyCreditsUrl = "/credits" }, statusCode: StatusCodes.Status402PaymentRequired);
            }

            try
            {
                var triage = await agent.TriageAsync(new LegalTriageRequest(modelInput, Language: "en"), ct);
                var exchange = await chatStore.SaveExchangeAsync(member.UserId, body.ConversationId, CreateTitle(message), message, FormatAssistant(triage), modelProvider.Name, correlationId, ct);
                if (exchange is null)
                {
                    await walletStore.RefundAsync(reservation, "Conversation does not belong to the authenticated user.", ct);
                    return Results.NotFound(new { error = "Conversation not found." });
                }
                await walletStore.CompleteAsync(reservation, ct);
                var wallet = await walletStore.GetWalletAsync(member.UserId, member.PlanCode, ct);
                context.Response.Headers["X-FijiLaw-Credits-Used"] = credits.ToString();
                return Results.Ok(new
                {
                    exchange.Conversation,
                    messages = new[] { exchange.UserMessage, exchange.AssistantMessage },
                    triage,
                    creditsUsed = credits,
                    wallet,
                    processing = new { provider = modelProvider.Name, region = "Singapore", historyPrivateToAccount = true }
                });
            }
            catch (ArgumentException ex)
            {
                await walletStore.RefundAsync(reservation, ex.Message, ct);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch
            {
                await walletStore.RefundAsync(reservation, "Dashboard legal chat failed before completion.", ct);
                throw;
            }
        }).RequireRateLimiting("chat");

        return app;
    }

    private static string BuildModelInput(string message, IReadOnlyList<LegalChatMessage> previousMessages)
    {
        if (previousMessages.Count == 0) return message;
        var context = string.Join("\n\n", previousMessages.TakeLast(8).Select(x => $"{(x.Role == "user" ? "User" : "FijiLaw AI")}: {x.Content}"));
        if (context.Length > 12000) context = context[^12000..];
        return $"Use the prior conversation only as context. Reassess all legal claims against verified Fiji sources.\n\n{context}\n\nCurrent user question:\n{message}";
    }

    private static string CreateTitle(string message)
    {
        var oneLine = Regex.Replace(message, @"\s+", " ").Trim();
        return oneLine.Length <= 72 ? oneLine : oneLine[..69] + "...";
    }

    private static string FormatAssistant(LegalTriageResult result)
    {
        var sections = new List<string> { result.Guidance };
        if (result.Authorities.Count > 0)
            sections.Add("Verified sources:\n" + string.Join("\n", result.Authorities.Select(x => $"- {x.Title}{(string.IsNullOrWhiteSpace(x.Provision) ? "" : $", {x.Provision}")}{(string.IsNullOrWhiteSpace(x.SourceUrl) ? "" : $" - {x.SourceUrl}")}")));
        if (result.NextSteps.Count > 0)
            sections.Add("Next steps:\n" + string.Join("\n", result.NextSteps.Select((x, i) => $"{i + 1}. {x}")));
        sections.Add(result.Disclaimer);
        return string.Join("\n\n", sections);
    }

    private static async Task<(AuthenticatedMember? Member, IResult? Error)> ResolveDashboardMemberAsync(HttpRequest request, HttpContext context, string? databaseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return (null, Results.Problem("Legal chat history requires PostgreSQL.", statusCode: StatusCodes.Status503ServiceUnavailable));
        var header = request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(token, ct);
        if (userId is null) return (null, Results.Unauthorized());
        var repository = context.RequestServices.GetRequiredService<PostgresMembershipRepository>();
        var memberAccess = await repository.GetAccessAsync(userId.Value, ct);
        if (memberAccess is null) return (null, Results.Unauthorized());

        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT email,display_name,email_verified,phone_number,phone_verified,identity_verified_at FROM app_users WHERE id=@id AND status='active'", connection);
        command.Parameters.AddWithValue("id", userId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (null, Results.Unauthorized());
        var member = new AuthenticatedMember(userId.Value, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetBoolean(2), memberAccess.Roles, memberAccess.Permissions, memberAccess.PlanCode, memberAccess.SubscriptionStatus, memberAccess.CurrentPeriodEnd,
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5));
        var decision = MembershipAuthorization.CanAccessDashboard(member);
        return decision.Allowed
            ? (member, null)
            : (null, Results.Json(new { error = decision.Reason }, statusCode: StatusCodes.Status403Forbidden));
    }
}
