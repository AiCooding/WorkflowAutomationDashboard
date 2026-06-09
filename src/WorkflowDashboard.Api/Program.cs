using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<WorkflowDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WorkflowDb")));

// Controllers
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

// Background polling service
builder.Services.AddHostedService<PollingService>();

// CORS (allow Angular dev server)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
    db.Database.EnsureCreated();
}

// Middleware
app.UseCors("AllowAngularDev");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<WorkflowHub>("/hubs/workflow");
app.MapFallbackToFile("index.html");

app.Run();
