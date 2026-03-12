using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
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

// 历史表数据库
builder.Services.AddDbContext<WcsHistoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HistoryConnection")));

// 备份表数据库
builder.Services.AddDbContext<WcsBackupDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BackupConnection")));

builder.Services.AddScoped<PathConfigService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<DataArchiveService>();
builder.Services.AddSingleton<DeviceService>();
builder.Services.AddHostedService<PlcDispatchService>();
builder.Services.AddHostedService<DataCleanupService>();

// Pipeline: 注册过滤器和钩子的实现（可替换为自定义实现）
builder.Services.AddScoped<IDeviceTaskDispatchFilter, DefaultDispatchFilter>();
builder.Services.AddScoped<IDeviceTaskCompletedHandler, DefaultDeviceTaskCompletedHandler>();
builder.Services.AddScoped<ITaskCompletedHandler, DefaultTaskCompletedHandler>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

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
    var initLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, builder.Configuration, initLogger);
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Sink(new SerilogSignalRSink(app.Services))
    .CreateLogger();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHub<WcsHub>("/hubs/wcs");

app.Run();
