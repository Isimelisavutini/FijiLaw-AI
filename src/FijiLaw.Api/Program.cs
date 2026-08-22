using FijiLaw.AI;
using FijiLaw.Api;
using FijiLaw.Domain;
using FijiLaw.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = builder.Configuration["DATABASE_URL"];
var adminApiKey = builder.Configuration["ADMIN_API_KEY"];
var openAiApiKey = builder.Configuration["OPENAI_API_KEY"];
var openAiModel = builder.Configuration["OPENAI_MODEL"] ?? "gpt-5.6-luna";

if (string.IsNullOrWhiteSpace(databaseUrl))
{
    builder.Services.AddSingleton<ILegalSourceRetriever, CuratedFijiLegalSourceRetriever>();
}
else
{
    builder.Services.AddSingleton<ILegalSourceRetriever>(_ => new PostgresLegalSourceRetriever(databaseUrl));
    builder.Services.AddSingleton(_ => new DatabaseInitializer(databaseUrl));
    builder.Services.AddSingleton(_ => new PostgresLegalSourceStore(databaseUrl));
}

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
    policy.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:3000")
          .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.EnsureCreatedAsync();
}

app.MapGet("/health", (ILanguageModelProvider modelProvider) => Results.Ok(new
{
    status = "ok",
    service = "FijiLaw.Api",
    legalSourceStorage = string.IsNullOrWhiteSpace(databaseUrl) ? "curated-official-source-fallback" : "postgresql",
    legalSourceIngestion = string.IsNullOrWhiteSpace(databaseUrl) ? "unavailable" : "available",
    aiProvider = modelProvider.Name,
    aiEnabled = modelProvider.IsEnabled,
    documentAnalysis = "pdf-docx-txt",
    legalServicesDirectory = "available"
}));

app.MapGet("/api/legal-services", (string? city, string? type, string? area, string? q, LegalServicesDirectory directory) =>
    Results.Ok(new { items = directory.Search(city, type, area, q), cities = directory.Cities() }));

app.MapPost("/api/legal/triage", async (LegalTriageRequest request, ILegalAgent agent, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await agent.TriageAsync(request, ct));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/legal/documents/analyse", async (
    IFormFile file,
    DocumentTextExtractor extractor,
    ILegalAgent agent,
    CancellationToken ct) =>
{
    try
    {
        var text = await extractor.ExtractAsync(file, ct);
        var triageText = $"I uploaded a legal document named '{Path.GetFileName(file.FileName)}'. Analyse the document context and identify the likely Fiji legal area, relevant authorities, important missing information and next steps. Document text:\n{text}";
        var assessment = await agent.TriageAsync(new LegalTriageRequest(triageText, Language: "en"), ct);
        var preview = text.Length > 1200 ? text[..1200] + "…" : text;

        return Results.Ok(new
        {
            fileName = Path.GetFileName(file.FileName),
            contentType = file.ContentType,
            characterCount = text.Length,
            preview,
            assessment,
            note = "The uploaded file is processed in memory for this MVP and is not stored by this endpoint."
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch
    {
        return Results.Problem("The document could not be read safely. Check that the file is a valid PDF, DOCX or TXT document.", statusCode: 400);
    }
}).DisableAntiforgery();

app.MapPost("/api/admin/legal-sources", async (
    HttpRequest httpRequest,
    LegalSourceInput input,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(databaseUrl))
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    if (string.IsNullOrWhiteSpace(adminApiKey))
        return Results.Problem("ADMIN_API_KEY is not configured.", statusCode: 503);

    if (!httpRequest.Headers.TryGetValue("X-Admin-Key", out var suppliedKey) || suppliedKey != adminApiKey)
        return Results.Unauthorized();

    try
    {
        var store = httpRequest.HttpContext.RequestServices.GetRequiredService<PostgresLegalSourceStore>();
        var correlationId = Guid.NewGuid().ToString("N");
        var id = await store.UpsertAsync(input, correlationId, ct);
        return Results.Ok(new { id, correlationId, input.Verified });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public partial class Program { }
