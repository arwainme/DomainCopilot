using DomainCopilot.API.Authentication;
using DomainCopilot.API.Middleware;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.Agents;
using DomainCopilot.Application.Configuration;
using DomainCopilot.Application.Services;
using DomainCopilot.Application.Tools;
using DomainCopilot.Application.Workflows;
using DomainCopilot.Infrastructure.Audit;
using DomainCopilot.Infrastructure.Providers;
using DomainCopilot.Infrastructure.Providers.Local;
using DomainCopilot.Infrastructure.Providers.OpenAI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;
using DomainCopilot.Infrastructure.Usage;
using DomainCopilot.Infrastructure.Providers.Gemini;
using DomainCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DomainCopilotDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

const string jwtSecret =
    "DomainCopilot-Demo-Secret-Key-2026-Change-In-Production";
builder.Services.AddScoped<IDocumentRepository, EfDocumentRepository>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<JwtAuthenticationService>();
// Configuration
builder.Services
    .AddOptions<LlmProviderOptions>()
    .Bind(builder.Configuration.GetSection("LlmProvider"));
//Tools
builder.Services.AddScoped<SearchEvidenceTool>();
builder.Services.AddScoped<CheckEligibilityTool>();
builder.Services.AddScoped<ResolveProcedureTool>();
builder.Services.AddScoped<SubmitApprovalTool>();
builder.Services.AddScoped<DocumentIngestionService>();

builder.Services.AddScoped<ITool>(sp =>
    sp.GetRequiredService<SearchEvidenceTool>());

builder.Services.AddScoped<ITool>(sp =>
    sp.GetRequiredService<CheckEligibilityTool>());

builder.Services.AddScoped<ITool>(sp =>
    sp.GetRequiredService<ResolveProcedureTool>());

builder.Services.AddScoped<ITool>(sp =>
    sp.GetRequiredService<SubmitApprovalTool>());

builder.Services.AddScoped<ToolRegistry>();
// LLM providers
builder.Services.AddSingleton<IUsageTracker, UsageTracker>();
builder.Services.AddControllers();
builder.Services.AddHttpClient<OpenAiLlmProvider>();
builder.Services.AddHttpClient<LocalLlmProvider>();
builder.Services.AddHttpClient<GeminiLlmProvider>();
builder.Services.AddHttpClient<DomainCopilot.Infrastructure.Providers.OpenRouter.OpenRouterLlmProvider>();
builder.Services.AddHttpClient<OpenAiLlmProvider>();



builder.Services.AddScoped<IRetrievalService, RetrievalService>();

builder.Services.AddScoped<IEligibilityIdentifier, EligibilityIdentifier>();
builder.Services.AddScoped<IProcedureResolver, ProcedureResolver>();
builder.Services.AddScoped<IResponseDrafter, ResponseDrafter>();

builder.Services.AddSingleton<IApprovalService, ApprovalService>();
builder.Services.AddScoped<IGovernmentWorkflow, GovernmentWorkflow>();
builder.Services.AddSingleton<IAuditStore, FileAuditStore>();
builder.Services.AddSingleton<LlmProviderSelector>();
builder.Services.AddScoped<IReplayService, ReplayService>();
builder.Services.AddSingleton<ILlmProvider>(sp =>
    sp.GetRequiredService<LlmProviderSelector>());
builder.Services
    .AddOptions<WorkflowOptions>()
    .Bind(builder.Configuration.GetSection("Workflow"));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<WorkflowOptions>>().Value);
// OpenAPI
builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "DomainCopilot API",
            Version = "v1"
        });

    options.AddSecurityDefinition(
        "bearer",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "Enter your JWT token. Example: Bearer eyJ..."
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "bearer",
                document)] = []
        });
});

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "DomainCopilot API v1");

        options.DocumentTitle = "DomainCopilot API";
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}));
//app.MapGet(
//    "/api/runs/{runId:guid}",
//    async (
//        Guid runId,
//        IAuditStore auditStore,
//        CancellationToken cancellationToken) =>
//    {
//        var run = await auditStore.GetRunAsync(
//            runId,
//            cancellationToken);

//        return run is null
//            ? Results.NotFound(new
//            {
//                message = $"Run '{runId}' was not found."
//            })
//            : Results.Ok(run);
//    });

//app.MapPost(
//    "/api/runs/{runId:guid}/replay",
//    async (
//        Guid runId,
//        IReplayService replayService,
//        CancellationToken cancellationToken) =>
//    {
//        var run = await replayService.ReplayAsync(
//            runId,
//            cancellationToken);

//        return run is null
//            ? Results.NotFound(new
//            {
//                message = $"Run '{runId}' was not found."
//            })
//            : Results.Ok(new
//            {
//                message = "Run replayed successfully.",
//                run
//            });
//    });

app.Run();


public partial class Program { }

