using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Services.AgentRunner;
using WorkflowDashboard.Api.Services.Catalog;
using WorkflowDashboard.Api.Services.Git;
using WorkflowDashboard.Api.Services.Pipeline;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

Log.Information("Starting WorkflowDashboard API host.");

builder.Services.AddDbContext<WorkflowDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WorkflowDb")));

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.Configure<CatalogOptions>(builder.Configuration.GetSection(CatalogOptions.SectionName));
builder.Services.AddSingleton<ICatalogStore, CatalogStore>();
builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddSingleton<ICatalogSettingsProvider, CatalogSettingsProvider>();
builder.Services.AddScoped<CatalogScanner>();
builder.Services.AddSingleton<CatalogStartupScanner>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CatalogStartupScanner>());

builder.Services.Configure<AgentRunnerOptions>(builder.Configuration.GetSection(AgentRunnerOptions.SectionName));
builder.Services.AddSingleton<IAgentRunnerSettingsProvider, AgentRunnerSettingsProvider>();
builder.Services.AddSingleton<IProcessLauncher, ProcessLauncher>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddSingleton<InstructionsInjector>();
builder.Services.AddSingleton<WorkflowInputWriter>();
builder.Services.AddSingleton<PipelineOrchestrator>();
builder.Services.AddSingleton<IPipelineOrchestrator>(sp => sp.GetRequiredService<PipelineOrchestrator>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PipelineOrchestrator>());

builder.Services.AddScoped<PipelineRunProjector>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

try
{
    var app = builder.Build();

    app.UseSerilogRequestLogging();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        Log.Information("Applying database migrations.");
        db.Database.Migrate();
    }

    app.UseCors("AllowAngularDev");
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapControllers();
    app.MapHub<WorkflowHub>("/hubs/workflow");
    app.MapFallbackToFile("index.html");

    Log.Information("WorkflowDashboard API host started.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WorkflowDashboard API host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
