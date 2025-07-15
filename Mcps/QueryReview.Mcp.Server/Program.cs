using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QueryReview.Mcp.Server.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddEnvironmentVariables()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json");
var mcpSettings = builder.Configuration.GetSection(McpServerSettings.SettingsKey).Get<McpServerSettings>()!;

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
var app = builder.Build();

app.MapMcp();

app.Run(mcpSettings.Endpoint);
