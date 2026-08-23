using FijiLaw.AI;
using FijiLaw.Domain;
using FijiLaw.Infrastructure;
using Npgsql;

namespace FijiLaw.Api;

public sealed record CreditCheckoutRequest(string PackageCode);
public sealed record AdminCreditGrantRequest(Guid UserId, string PlanCode, int Credits, string Reason);

public static class CreditEndpoints
{
    public static WebApplication MapCreditEndpoints(this WebApplication app, string? databaseUrl)
    {
        var guestTrials = new GuestTriageTrialStore(databaseUrl);
        guestTrials.EnsureCreatedAsync().GetAwaiter().GetResult();

        app.MapGet("/api/credits/catalog", (WindcavePaymentGateway gateway) => Results.Ok(new
        {
            currency = "FJD",
            terminology = "FijiLaw Credits",
            packages = FijiLawCreditCatalog.Packages,
            services = FijiLawCreditCatalog.Services,
            paymentProvider = gateway.IsConfigured ? gateway.ProviderName : null,
            paymentCheckoutReady = gateway.IsConfigured,
            includedByPlan = new Dictionary<string, int>
            {
                [MembershipPlans.Free] = FijiLawCreditCatalog.IncludedCredits(MembershipPlans.Free),
                [MembershipPlans.PersonalPlus] = FijiLawCreditCatalog.IncludedCredits(MembershipPlans.PersonalPlus),
                [MembershipPlans.LawyerProfessional] = FijiLawCreditCatalog.IncludedCredits(MembershipPlans.LawyerProfessional),
                [MembershipPlans.FirmStarter] = FijiLawCreditCatalog.IncludedCredits(MembershipPlans.FirmStarter),
                [MembershipPlans.FirmProfessional] = FijiLawCreditCatalog.IncludedCredits(MembershipPlans.FirmProfessional),
                [MembershipPlans.FirmPremium] = FijiLawCreditCatalog.IncludedCredits(MembershipPlans.FirmPremium),
                [MembershipPlans.Institutional] = FijiLawCreditCatalog.IncludedCredits(MembershipPlans.Institutional)
            },
            note = "FijiLaw Credits are FijiLaw usage units and are not OpenAI API tokens."
        }));

        app.MapGet("/api/credits/wallet", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            var wallet = await context.RequestServices.GetRequiredService<ICreditWalletStore>().GetWalletAsync(member.UserId, member.PlanCode, ct);
            return Results.Ok(new { wallet, planCode = member.PlanCode, demo = string.IsNullOrWhiteSpace(databaseUrl) });
        });

        app.MapGet("/api/credits/history", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            var store = context.RequestServices.GetRequiredService<ICreditWalletStore>();
            return Results.Ok(new { items = await store.GetHistoryAsync(member.UserId, 50, ct) });
        });

        app.MapPost("/api/credits/checkout", async (CreditCheckoutRequest body, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            var package = FijiLawCreditCatalog.Packages.FirstOrDefault(x => string.Equals(x.Code, body.PackageCode, StringComparison.OrdinalIgnoreCase));
            if (package is null) return Results.BadRequest(new { error = "Unknown FijiLaw credit package." });

            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                var wallet = await context.RequestServices.GetRequiredService<ICreditWalletStore>().GrantAsync(member.UserId, member.PlanCode, package.Credits, $"Controlled demo top-up: {package.Name}", purchased: true, providerReference: $"demo:{package.Code}", ct);
                return Results.Ok(new { simulated = true, charged = false, package, wallet, message = "Demo top-up completed. No payment was processed." });
            }

            var gateway = context.RequestServices.GetRequiredService<WindcavePaymentGateway>();
            if (!gateway.IsConfigured)
            {
                return Results.Json(new
                {
                    error = "Online credit purchase is prepared for Windcave but merchant credentials are not configured yet.",
                    package,
                    paymentProviderRequired = true,
                    recommendedProvider = "windcave"
                }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var paymentStore = context.RequestServices.GetRequiredService<PostgresCreditPaymentStore>();
            var order = await paymentStore.CreateAsync(member.UserId, member.PlanCode, package, gateway.ProviderName, ct);
            try
            {
                var session = await gateway.CreateCheckoutAsync(order, member.Email, ct);
                await paymentStore.AttachSessionAsync(order.Id, session.SessionId, session.CheckoutUrl, ct);
                return Results.Ok(new
                {
                    simulated = false,
                    charged = false,
                    provider = session.Provider,
                    orderId = order.Id,
                    checkoutUrl = session.CheckoutUrl,
                    package,
                    message = "Redirect the customer to the hosted payment page. Credits are granted only after server-side payment verification."
                });
            }
            catch
            {
                await paymentStore.MarkFailedAsync(order.Id, "failed", ct);
                return Results.Problem("Payment checkout could not be started. Please try again later.", statusCode: 502);
            }
        }).RequireRateLimiting("payment");

        app.MapMethods("/api/credits/payment/notify", new[] { "GET", "POST" }, async (Guid orderId, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var outcome = await ProcessPaymentAsync(orderId, context, ct);
            return Results.Ok(outcome);
        }).RequireRateLimiting("payment-notify");

        app.MapGet("/api/credits/payment/status/{orderId:guid}", async (Guid orderId, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Ok(new { status = "demo", completed = false });
            var paymentStore = context.RequestServices.GetRequiredService<PostgresCreditPaymentStore>();
            var order = await paymentStore.GetAsync(orderId, ct);
            if (order is null) return Results.NotFound();
            if (order.UserId != member.UserId && !member.Roles.Contains(MembershipRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase)) return Results.Forbid();
            var outcome = await ProcessPaymentAsync(orderId, context, ct);
            var wallet = await context.RequestServices.GetRequiredService<ICreditWalletStore>().GetWalletAsync(order.UserId, order.PlanCode, ct);
            return Results.Ok(new { outcome.status, outcome.completed, orderId, wallet });
        }).RequireRateLimiting("payment");

        app.MapPost("/api/admin/credits/grant", async (AdminCreditGrantRequest body, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var actor = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (actor is null) return Results.Unauthorized();
            if (!actor.Roles.Contains(MembershipRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase)) return Results.Forbid();
            if (body.Credits <= 0 || body.Credits > 100000) return Results.BadRequest(new { error = "Credits must be between 1 and 100000." });
            var store = context.RequestServices.GetRequiredService<ICreditWalletStore>();
            var wallet = await store.GrantAsync(body.UserId, body.PlanCode, body.Credits, body.Reason, purchased: false, providerReference: $"admin:{actor.UserId}", ct);
            return Results.Ok(new { granted = true, wallet });
        });

        app.MapGet("/api/legal/guest-trial-status", async (HttpRequest request, CancellationToken ct) =>
        {
            var guestId = request.Headers["X-FijiLaw-Guest-Id"].ToString();
            try
            {
                var status = await guestTrials.GetStatusAsync(guestId, ct);
                return Results.Ok(new
                {
                    status.Used,
                    status.Remaining,
                    status.Limit,
                    status.Exhausted,
                    signUpRequired = status.Exhausted
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireRateLimiting("auth");

        app.MapPost("/api/legal/triage", async (LegalTriageRequest body, HttpRequest request, HttpContext context, ILegalAgent agent, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);

            if (member is null)
            {
                var guestId = request.Headers["X-FijiLaw-Guest-Id"].ToString();
                GuestTriageTrialStatus? trial;
                try
                {
                    trial = await guestTrials.TryReserveAsync(guestId, ct);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message, guestTrial = true });
                }

                if (trial is null)
                {
                    return Results.Json(new
                    {
                        error = "You have used your 3 free FijiLaw AI triage reports. Create a free account or sign in to continue.",
                        guestTrial = true,
                        guestTrialExhausted = true,
                        signUpRequired = true,
                        limit = GuestTriageTrialStore.TrialLimit,
                        remaining = 0,
                        registerUrl = "/account?mode=register",
                        signInUrl = "/account?mode=login"
                    }, statusCode: StatusCodes.Status403Forbidden);
                }

                try
                {
                    var guestResult = await agent.TriageAsync(body, ct);
                    context.Response.Headers["X-FijiLaw-Guest-Trial"] = "true";
                    context.Response.Headers["X-FijiLaw-Guest-Trials-Used"] = trial.Used.ToString();
                    context.Response.Headers["X-FijiLaw-Guest-Trials-Remaining"] = trial.Remaining.ToString();
                    return Results.Ok(guestResult);
                }
                catch (ArgumentException ex)
                {
                    await guestTrials.ReleaseAsync(guestId, ct);
                    return Results.BadRequest(new { error = ex.Message, guestTrial = true });
                }
                catch
                {
                    await guestTrials.ReleaseAsync(guestId, ct);
                    throw;
                }
            }

            var price = FijiLawCreditCatalog.PriceFor(FijiLawCreditCatalog.AdvancedTriage);
            var store = context.RequestServices.GetRequiredService<ICreditWalletStore>();
            var correlation = Guid.NewGuid().ToString("N");
            var reservation = await store.ReserveAsync(member.UserId, member.PlanCode, price, FijiLawCreditCatalog.AdvancedTriage, correlation, ct);
            if (reservation is null)
            {
                var wallet = await store.GetWalletAsync(member.UserId, member.PlanCode, ct);
                return Results.Json(new { error = "Not enough FijiLaw Credits for this Advanced Legal Triage Report.", creditsRequired = price, balance = wallet.Balance, buyCreditsUrl = "/credits" }, statusCode: StatusCodes.Status402PaymentRequired);
            }

            try
            {
                var result = await agent.TriageAsync(body, ct);
                await store.CompleteAsync(reservation, ct);
                context.Response.Headers["X-FijiLaw-Credits-Used"] = price.ToString();
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                await store.RefundAsync(reservation, ex.Message, ct);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch
            {
                await store.RefundAsync(reservation, "AI/legal triage workflow failed before completion.", ct);
                throw;
            }
        }).RequireRateLimiting("auth");

        app.MapPost("/api/legal/documents/analyse", async (IFormFile file, HttpRequest request, HttpContext context, DocumentTextExtractor extractor, ILegalAgent agent, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Json(new { error = "Sign in is required to use FijiLaw AI document analysis.", signInRequired = true }, statusCode: StatusCodes.Status401Unauthorized);
            var price = FijiLawCreditCatalog.PriceFor(FijiLawCreditCatalog.DocumentAnalysis);
            var store = context.RequestServices.GetRequiredService<ICreditWalletStore>();
            var correlation = Guid.NewGuid().ToString("N");
            var reservation = await store.ReserveAsync(member.UserId, member.PlanCode, price, FijiLawCreditCatalog.DocumentAnalysis, correlation, ct);
            if (reservation is null)
            {
                var wallet = await store.GetWalletAsync(member.UserId, member.PlanCode, ct);
                return Results.Json(new { error = "Not enough FijiLaw Credits for document analysis.", creditsRequired = price, balance = wallet.Balance, buyCreditsUrl = "/credits" }, statusCode: StatusCodes.Status402PaymentRequired);
            }

            try
            {
                var text = await extractor.ExtractAsync(file, ct);
                var triageText = $"I uploaded a legal document named '{Path.GetFileName(file.FileName)}'. Analyse the document context and identify the likely Fiji legal area, relevant authorities, important missing information and next steps. Document text:\n{text}";
                var assessment = await agent.TriageAsync(new LegalTriageRequest(triageText, Language: "en"), ct);
                await store.CompleteAsync(reservation, ct);
                context.Response.Headers["X-FijiLaw-Credits-Used"] = price.ToString();
                var preview = text.Length > 1200 ? text[..1200] + "…" : text;
                return Results.Ok(new { fileName = Path.GetFileName(file.FileName), contentType = file.ContentType, characterCount = text.Length, preview, assessment, creditsUsed = price, note = "The uploaded file is processed in memory for this MVP and is not stored by this endpoint." });
            }
            catch (ArgumentException ex)
            {
                await store.RefundAsync(reservation, ex.Message, ct);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch
            {
                await store.RefundAsync(reservation, "Document analysis workflow failed before completion.", ct);
                return Results.Problem("The document could not be read safely. Check that the file is a valid PDF, DOCX or TXT document.", statusCode: 400);
            }
        }).DisableAntiforgery();

        return app;
    }

    private static async Task<(string status, bool completed)> ProcessPaymentAsync(Guid orderId, HttpContext context, CancellationToken ct)
    {
        var store = context.RequestServices.GetRequiredService<PostgresCreditPaymentStore>();
        var gateway = context.RequestServices.GetRequiredService<WindcavePaymentGateway>();
        var order = await store.GetAsync(orderId, ct);
        if (order is null) return ("not-found", false);
        if (order.Status == "completed") return ("completed", true);
        if (!string.Equals(order.Provider, gateway.ProviderName, StringComparison.OrdinalIgnoreCase)) return ("unsupported-provider", false);
        if (!gateway.IsConfigured) return ("provider-not-configured", false);

        var verification = await gateway.VerifyAsync(order, ct);
        if (verification.Authorised)
        {
            var providerReference = $"windcave:{verification.ProviderReference ?? order.ProviderSessionId ?? order.Id.ToString("N")}";
            var granted = await store.CompleteAndGrantAsync(order.Id, providerReference, ct);
            return (granted ? "completed" : "already-processed", granted);
        }

        if (verification.State.EndsWith("-mismatch", StringComparison.OrdinalIgnoreCase))
        {
            await store.MarkFailedAsync(order.Id, "verification_failed", ct);
            return ("verification-failed", false);
        }

        if (string.Equals(verification.State, "complete", StringComparison.OrdinalIgnoreCase))
            await store.MarkFailedAsync(order.Id, "declined", ct);
        return (verification.State, false);
    }

    private static async Task<AuthenticatedMember?> ResolveMemberAsync(HttpRequest request, HttpContext context, string? databaseUrl, CancellationToken ct)
    {
        var token = GetBearerToken(request);
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return context.RequestServices.GetRequiredService<DemoMembershipAuthStore>().Resolve(token);

        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(token, ct);
        if (userId is null) return null;
        var repository = context.RequestServices.GetRequiredService<PostgresMembershipRepository>();
        var access = await repository.GetAccessAsync(userId.Value, ct);
        if (access is null) return null;

        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT email,display_name,email_verified FROM app_users WHERE id=@id AND status='active'", connection);
        command.Parameters.AddWithValue("id", userId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AuthenticatedMember(userId.Value, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetBoolean(2), access.Roles, access.Permissions, access.PlanCode, access.SubscriptionStatus, access.CurrentPeriodEnd);
    }

    private static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}
