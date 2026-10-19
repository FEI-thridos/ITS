using KukaFm.Middleware.Api.Data;
using KukaFm.Middleware.Api.Kuka;
using KukaFm.Middleware.Api.Robots;
using KukaFm.Middleware.Api.Sync;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddOptions<KukaFmOptions>().BindConfiguration(KukaFmOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SyncOptions>().BindConfiguration(SyncOptions.Section).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IKukaClient, KukaClient>();
builder.Services.AddScoped<IRobotRepository, RobotRepository>();
builder.Services.AddScoped<RobotService>();
builder.Services.AddScoped<RobotSyncService>();
builder.Services.AddHostedService<RobotSyncWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/api/health", () => Results.Ok());
app.MapRobotEndpoints();

app.Run();
