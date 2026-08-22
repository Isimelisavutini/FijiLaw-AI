using FijiLaw.AI;
using FijiLaw.Domain;
using FijiLaw.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = builder.Configuration["DATABASE_URL"];
if (string.IsNullOrWhiteSpace(databaseUrl))
{
    builder.Services.AddSingleton<ILegalSourceRetriever, EmptyLegalSourceRetriever>();
}
else
{
    builder.Services.AddSingleton<ILegalSourceRetriever>(_ => new PostgresLegalSourceRetriever(databaseUrl));
    builder.Services.AddSingleton(_ => new DatabaseInitializer(databaseUrl));
}

builder.Services.AddSingleton<ILegalAgent, LegalAgent>();
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

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "FijiLaw.Api",
    legalSourceStorage = string.IsNullOrWhiteSpace(databaseUrl) ? "in-memory-fallback" : "postgresql"
}));

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

app.Run();

public partial class Program { }
