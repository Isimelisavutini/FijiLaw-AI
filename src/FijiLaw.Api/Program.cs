using FijiLaw.AI;
using FijiLaw.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILegalSourceRetriever, EmptyLegalSourceRetriever>();
builder.Services.AddSingleton<ILegalAgent, LegalAgent>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:3000")
          .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "FijiLaw.Api" }));

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
