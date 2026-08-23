using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FijiLaw.AI;
using FijiLaw.Api;
using FijiLaw.Domain;
using FijiLaw.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = builder.Configuration["DATABASE_URL"];
var adminApiKey = builder.Configuration["ADMIN_API_KEY"];
var openAiApiKey = builder.Configuration["OPENAI_API_KEY"];
var openAiModel = builder.Configuration["OPENAI_MODEL"] ?? "gpt-5.6-luna";
var resendApiKey = builder.Configuration["RESEND_API_KEY"];
var emailFrom = builder.Configuration["EMAIL_FROM"];
var publicWebUrl = builder.Configuration["PUBLIC_WEB_URL"];
var publicApiUrl = builder.Configuration["PUBLIC_API_URL"];
var windcaveUsername = builder.Configuration["WINDCAVE_API_USERNAME"];
var windcaveApiKey = builder.Configuration["WINDCAVE_API_KEY"];
var windcaveApiBase = builder.Configuration["WINDCAVE_API_BASE"];
var demoAuthEnabled = string.Equals(builder.Configuration["DEMO_AUTH_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
var seedDemoAccounts = string.Equals(builder.Configuration["SEED_DEMO_ACCOUNTS"], "true", StringComparison.OrdinalIgnoreCase);
var demoPassword = demoAuthEnabled ? (builder.Configuration["DEMO_AUTH_PASSWORD"] ?? "FijiLawDemo2026!") : null;
var configuredOrigins = new[]
{
    builder.Configuration["WebOrigin"],
    builder.Configuration["AllowedWebOrigins"]
}
.Where(x => !string.IsNullOrWhiteSpace(x))
.SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
.Select(x => x.TrimEnd('/'))
.ToHashSet(StringComparer.OrdinalIgnoreCase);

if (string.IsNullOrWhiteSpace(databaseUrl))
{
    builder.Services.AddSingleton<ILegalSourceRetriever, CuratedFijiLegalSourceRetriever>();
    builder.Services.AddSingleton<ICreditWalletStore, DemoCreditWalletStore>();
}
else
{
    builder.Services.AddSingleton<ILegalSourceRetriever>(_ => new PostgresLegalSourceRetriever(databaseUrl));
    builder.Services.AddSingleton(_ => new DatabaseInitializer(databaseUrl));
    builder.Services.AddSingleton(_ => new PostgresLegalSourceStore(databaseUrl));
    builder.Services.AddSingleton(_ => new PostgresMembershipInitializer(databaseUrl));
    builder.Services.AddSingleton(_ => new PostgresMembershipRepository(databaseUrl));
    builder.Services.AddSingleton(_ => new PostgresMembershipAuthStore(databaseUrl));
    builder.Services.AddSingleton(_ => new PostgresMembershipSecurityStore(databaseUrl));
    builder.Services.AddSingleton(_ => new PostgresCreditWalletStore(databaseUrl));
    builder.Services.AddSingleton<ICreditWalletStore>(sp => sp.GetRequiredService<PostgresCreditWalletStore>());
    builder.Services.AddSingleton(_ => new PostgresCreditPaymentStore(databaseUrl));
    builder.Services.AddSingleton(sp => new PostgresDemoAccountSeeder(databaseUrl, sp.GetRequiredService<PostgresMembershipAuthStore>()));
}

builder.Services.AddSingleton(new DemoMembershipAuthStore(string.IsNullOrWhiteSpace(databaseUrl) ? demoPassword : null));
builder.Services.AddSingleton(_ => new ResendEmailSender(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, resendApiKey, emailFrom, publicWebUrl));
builder.Services.AddSingleton(_ => new WindcavePaymentGateway(
    new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
    windcaveUsername,
    windcaveApiKey,
    windcaveApiBase,
    publicApiUrl,
    publicWebUrl));

if (string.IsNullOrWhiteSpace(openAiApiKey)) builder.Services.AddSingleton<ILanguageModelProvider, DisabledLanguageModelProvider>();
else builder.Services.AddSingleton<ILanguageModelProvider>(_ => new OpenAIResponsesProvider(new HttpClient { Timeout = TimeSpan.FromSeconds(45) }, openAiApiKey, openAiModel));

builder.Services.AddSingleton<ILegalAgent, LegalAgent>();
builder.Services.AddSingleton<DocumentTextExtractor>();
builder.Services.AddSingleton<LegalServicesDirectory>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.SetIsOriginAllowed(origin => IsAllowedWebOrigin(origin, configuredOrigins)).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("verification", httpContext => RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("payment", httpContext => RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("payment-notify", httpContext => RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

var app = builder.Build();
app.UseCors();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresMembershipInitializer>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresMembershipAuthStore>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresMembershipSecurityStore>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresCreditWalletStore>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresCreditPaymentStore>().EnsureCreatedAsync();
    if (seedDemoAccounts) await scope.ServiceProvider.GetRequiredService<PostgresDemoAccountSeeder>().EnsureSeededAsync();
}

app.MapGet("/health", (ILanguageModelProvider modelProvider, ResendEmailSender emailSender, DemoMembershipAuthStore demoAuth, WindcavePaymentGateway payments) => Results.Ok(new
{
    status = "ok",
    service = "FijiLaw.Api",
    legalSourceStorage = string.IsNullOrWhiteSpace(databaseUrl) ? "curated-official-source-fallback" : "postgresql",
    legalSourceIngestion = string.IsNullOrWhiteSpace(databaseUrl) ? "unavailable" : "available",
    membershipStorage = !string.IsNullOrWhiteSpace(databaseUrl) ? "postgresql" : demoAuth.IsEnabled ? "demo-memory" : "configuration-fallback",
    membershipAuth = !string.IsNullOrWhiteSpace(databaseUrl) ? "available" : demoAuth.IsEnabled ? "demo" : "awaiting-postgresql",
    membershipSecurity = !string.IsNullOrWhiteSpace(databaseUrl) ? "available" : demoAuth.IsEnabled ? "demo" : "awaiting-postgresql",
    creditWallet = !string.IsNullOrWhiteSpace(databaseUrl) ? "postgresql" : demoAuth.IsEnabled ? "demo-memory" : "unavailable",
    creditMetering = "enabled",
    creditPayments = payments.IsConfigured ? "windcave-ready" : "awaiting-windcave-merchant-credentials",
    paymentVerification = "server-side-exact-order-match",
    demoAccountsSeeded = !string.IsNullOrWhiteSpace(databaseUrl) && seedDemoAccounts,
    emailDelivery = emailSender.IsConfigured ? "configured" : "awaiting-resend-config",
    aiProvider = modelProvider.Name,
    aiEnabled = modelProvider.IsEnabled,
    documentAnalysis = "pdf-docx-txt",
    legalServicesDirectory = "available",
    connectivity = "ready"
}));

app.MapGet("/api/membership/plans", async (HttpContext context, CancellationToken ct) =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        var repository = context.RequestServices.GetRequiredService<PostgresMembershipRepository>();
        return Results.Ok(new { items = await repository.GetPlansAsync(ct), source = "postgresql" });
    }

    var fallback = new[]
    {
        new MembershipPlanSummary("free", "Free", "citizen", 0m, 0m, false, Array.Empty<string>()),
        new MembershipPlanSummary("personal_plus", "Personal Plus", "citizen", 20m, 200m, true, new[] { MembershipPermissions.DashboardAccess, MembershipPermissions.CasesCreate, MembershipPermissions.CasesViewOwn, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.DocumentsStore, MembershipPermissions.ReferralsRequest, MembershipPermissions.BillingView }),
        new MembershipPlanSummary("lawyer_professional", "Lawyer Professional", "lawyer", 100m, 1000m, true, new[] { MembershipPermissions.DashboardAccess, MembershipPermissions.CasesManage, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.ReferralsManage, MembershipPermissions.LeadsView, MembershipPermissions.LeadsManage, MembershipPermissions.LawyerProfileManage, MembershipPermissions.AnalyticsView, MembershipPermissions.BillingView }),
        new MembershipPlanSummary("firm_starter", "Law Firm Starter", "law_firm", 200m, 2000m, true, new[] { MembershipPermissions.DashboardAccess, MembershipPermissions.CasesManage, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.ReferralsManage, MembershipPermissions.LeadsView, MembershipPermissions.LeadsManage, MembershipPermissions.FirmManage, MembershipPermissions.AnalyticsView, MembershipPermissions.BillingView }),
        new MembershipPlanSummary("firm_professional", "Law Firm Professional", "law_firm", 350m, 3500m, true, new[] { MembershipPermissions.DashboardAccess, MembershipPermissions.CasesManage, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.ReferralsManage, MembershipPermissions.LeadsView, MembershipPermissions.LeadsManage, MembershipPermissions.FirmManage, MembershipPermissions.FirmUsersManage, MembershipPermissions.AnalyticsView, MembershipPermissions.BillingView }),
        new MembershipPlanSummary("firm_premium", "Law Firm Premium", "law_firm", 600m, 6000m, true, new[] { MembershipPermissions.DashboardAccess, MembershipPermissions.CasesManage, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.ReferralsManage, MembershipPermissions.LeadsView, MembershipPermissions.LeadsManage, MembershipPermissions.FirmManage, MembershipPermissions.FirmUsersManage, MembershipPermissions.AnalyticsView, MembershipPermissions.BillingView, MembershipPermissions.DirectoryPriorityPlacement }),
        new MembershipPlanSummary("institutional", "Institutional", "institution", null, null, true, new[] { MembershipPermissions.DashboardAccess })
    };
    return Results.Ok(new { items = fallback, source = "configuration-fallback" });
});

app.MapMembershipEndpoints(databaseUrl);
app.MapCreditEndpoints(databaseUrl);
app.MapGet("/api/legal-services", (string? city, string? type, string? area, string? q, LegalServicesDirectory directory) => Results.Ok(new { items = directory.Search(city, type, area, q), cities = directory.Cities() }));

app.MapPost("/api/admin/legal-sources", async (HttpRequest httpRequest, LegalSourceInput input, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    if (string.IsNullOrWhiteSpace(adminApiKey)) return Results.Problem("ADMIN_API_KEY is not configured.", statusCode: 503);
    if (!httpRequest.Headers.TryGetValue("X-Admin-Key", out var suppliedKey) || suppliedKey != adminApiKey) return Results.Unauthorized();
    try
    {
        var store = httpRequest.HttpContext.RequestServices.GetRequiredService<PostgresLegalSourceStore>();
        var correlationId = Guid.NewGuid().ToString("N");
        var id = await store.UpsertAsync(input, correlationId, ct);
        return Results.Ok(new { id, correlationId, input.Verified });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.Run();

static bool IsAllowedWebOrigin(string origin, HashSet<string> configuredOrigins)
{
    if (configuredOrigins.Contains(origin.TrimEnd('/'))) return true;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    if (uri.Scheme == Uri.UriSchemeHttp && (uri.Host == "localhost" || uri.Host == "127.0.0.1")) return true;
    if (uri.Scheme != Uri.UriSchemeHttps) return false;
    var host = uri.Host.ToLowerInvariant();
    if (host == "fijilaw-ai-pasifika-solutions.vercel.app") return true;
    return host.StartsWith("fijilaw-", StringComparison.Ordinal) && host.EndsWith("-pasifika-solutions.vercel.app", StringComparison.Ordinal);
}

public partial class Program { }
