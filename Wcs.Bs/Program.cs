using Microsoft.EntityFrameworkCore;
using Serilog;
using Wcs.Bs.Domain;
using Wcs.Bs.Hubs;
using Wcs.Bs.Infrastructure;
using Wcs.Bs.Services;
using Wcs.Bs.Services.Pipeline;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<WcsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<PathConfigService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddSingleton<DeviceService>();
builder.Services.AddHostedService<PlcDispatchService>();
builder.Services.AddHostedService<DataCleanupService>();

// Pipeline: 注册过滤器和钩子的实现（可替换为自定义实现）
builder.Services.AddScoped<IDeviceTaskDispatchFilter, DefaultDispatchFilter>();
builder.Services.AddScoped<IDeviceTaskCompletedHandler, DefaultDeviceTaskCompletedHandler>();
builder.Services.AddScoped<ITaskCompletedHandler, DefaultTaskCompletedHandler>();

builder.Services.AddSignalR();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WcsDbContext>();
    db.Database.EnsureCreated();

    var pathConfigService = scope.ServiceProvider.GetRequiredService<PathConfigService>();
    var configPath = builder.Configuration["PathConfig:JsonPath"] ?? "Config/paths.json";
    await pathConfigService.ImportFromFileAsync(configPath);

    var deviceService = scope.ServiceProvider.GetRequiredService<DeviceService>();
    var devices = builder.Configuration.GetSection("Devices").Get<List<DeviceConfig>>() ?? new();
    foreach (var device in devices)
    {
        deviceService.RegisterDevice(device);
    }
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Sink(new SerilogSignalRSink(app.Services))
    .CreateLogger();

app.UseCors();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHub<WcsHub>("/hubs/wcs");

app.Run();
