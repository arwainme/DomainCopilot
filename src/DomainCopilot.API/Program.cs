using DomainCopilot.Application.Abstractions;
using DomainCopilot.Infrastructure.Providers;
using DomainCopilot.Infrastructure.Providers.Local;
using DomainCopilot.Infrastructure.Providers.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services
    .AddOptions<LlmProviderOptions>()
    .Bind(builder.Configuration.GetSection("LlmProvider"));

// LLM providers
builder.Services.AddHttpClient<OpenAiLlmProvider>();
builder.Services.AddHttpClient<LocalLlmProvider>();

builder.Services.AddSingleton<LlmProviderSelector>();

builder.Services.AddSingleton<ILlmProvider>(sp =>
    sp.GetRequiredService<LlmProviderSelector>());

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}));

app.Run();
