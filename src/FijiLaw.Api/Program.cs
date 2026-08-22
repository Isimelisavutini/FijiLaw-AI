using System.Threading.RateLimiting;
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
}

builder.Services.AddSingleton(_ => new ResendEmailSender(
    new HttpClient { Timeout = TimeSpan.FromSeconds(20) },
    resendApiKey,
    emailFrom,
    publicWebUrl));

if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    builder.Services.AddSingleton<ILanguageModelProvider, DisabledLanguageModelProvider>();
}
else
{
    builder.Services.AddSingleton<ILanguageModelProvider>(_ =>
        new OpenAIResponsesProvider(new HttpClient { Timeout = TimeSpan.FromSeconds(45) }, openAiApiKey, openAiModel));
}

builder.Services.AddSingleton<ILegalAgent, LegalAgent>();
builder.Services.AddSingleton<DocumentTextExtractor>();
builder.Services.AddSingleton<LegalServicesDirectory>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.SetIsOriginAllowed(origin => IsAllowedWebOrigin(origin, configuredOrigins))
          .AllowAnyHeader()
          .AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("verification", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
app.UseCors();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.ReferrerPolicy = "no-referrer";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresMembershipInitializer>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresMembershipAuthStore>().EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PostgresMembershipSecurityStore>().EnsureCreatedAsync();
}

app.MapGet("/health", (ILanguageModelProvider modelProvider, ResendEmailSender emailSender) => Results.Ok(new
{
    status = "ok",
    service = "FijiLaw.Api",
    legalSourceStorage = string.IsNullOrWhiteSpace(databaseUrl) ? "curated-official-source-fallback" : "postgresql",
    legalSourceIngestion = string.IsNullOrWhiteSpace(databaseUrl) ? "unavailable" : "available",
    membershipStorage = string.IsNullOrWhiteSpace(databaseUrl) ? "configuration-fallback" : "postgresql",
    membershipAuth = string.IsNullOrWhiteSpace(databaseUrl) ? "awaiting-postgresql" : "available",
    membershipSecurity = string.IsNullOrWhiteSpace(databaseUrl) ? "awaiting-postgresql" : "available",
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

app.MapGet("/api/legal-services", (string? city, string? type, string? area, string? q, LegalServicesDirectory directory) =>
    Results.Ok(new { items = directory.Search(city, type, area, q), cities = directory.Cities() }));

app.MapPost("/api/legal/triage", async (LegalTriageRequest request, ILegalAgent agent, CancellationToken ct) =>
{
    try { return Results.Ok(await agent.TriageAsync(request, ct)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/legal/documents/analyse", async (IFormFile file, DocumentTextExtractor extractor, ILegalAgent agent, CancellationToken ct) =>
{
    try
    {
        var text = await extractor.ExtractAsync(file, ct);
        var triageText = $"I uploaded a legal document named '{Path.GetFileName(file.FileName)}'. Analyse the document context and identify the likely Fiji legal area, relevant authorities, important missing information and next steps. Document text:\n{text}";
        var assessment = await agent.TriageAsync(new LegalTriageRequest(triageText, Language: "en"), ct);
        var preview = text.Length > 1200 ? text[..1200] + "…" : text;
        return Results.Ok(new { fileName = Path.GetFileName(file.FileName), contentType = file.ContentType, characterCount = text.Length, preview, assessment, note = "The uploaded file is processed in memory for this MVP and is not stored by this endpoint." });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch { return Results.Problem("The document could not be read safely. Check that the file is a valid PDF, DOCX or TXT document.", statusCode: 400); }
}).DisableAntiforgery();

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
