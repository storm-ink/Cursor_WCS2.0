using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;
using Wcs.Bs.Services;

namespace Wcs.Bs.Infrastructure;

/// <summary>
/// 负责应用启动时的数据库初始化、种子数据写入、路径配置加载和设备注册。
/// 将原先分散在 Program.cs 中的初始化逻辑集中到一处，并提供日志记录与重试策略。
/// </summary>
public static class DatabaseInitializer
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(2);
    private const int DbCommandTimeoutSeconds = 60;

    /// <summary>
    /// 执行完整的数据库初始化流程：建表 → 补齐缺失表 → 种子数据 → 加载配置 → 注册设备。
    /// 每个步骤均有独立的错误捕获与日志记录；数据库操作带有超时与重试保护。
    /// </summary>
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        logger.LogInformation("[DB Init] 开始数据库初始化…");

        var mainDb    = services.GetRequiredService<WcsDbContext>();
        var historyDb = services.GetRequiredService<WcsHistoryDbContext>();
        var backupDb  = services.GetRequiredService<WcsBackupDbContext>();

        // 为每个上下文设置命令超时
        mainDb.Database.SetCommandTimeout(DbCommandTimeoutSeconds);
        historyDb.Database.SetCommandTimeout(DbCommandTimeoutSeconds);
        backupDb.Database.SetCommandTimeout(DbCommandTimeoutSeconds);

        // 1. EnsureCreated（新库直接建全表；已存在则跳过）
        await EnsureDbCreatedAsync(mainDb,    "主库 (cursor_wcs)",          logger);
        await EnsureDbCreatedAsync(historyDb, "历史库 (cursor_wcs_history)", logger);
        await EnsureDbCreatedAsync(backupDb,  "备份库 (cursor_wcs_backup)",  logger);

        // 2. 补齐 Users 表 —— 处理在 UserEntity 加入前已存在的主库
        await EnsureUsersTableAsync(mainDb, logger);

        // 3. 写入默认用户种子数据
        await SeedUsersAsync(mainDb, logger);

        // 4. 加载路径配置（失败不阻断启动）
        await LoadPathConfigAsync(services, configuration, logger);

        // 5. 注册设备
        RegisterDevices(services, configuration, logger);

        logger.LogInformation("[DB Init] 数据库初始化完成");
    }

    // ── 私有辅助方法 ──────────────────────────────────────────────────────────

    /// <summary>带重试的 EnsureCreated。</summary>
    private static async Task EnsureDbCreatedAsync(DbContext db, string name, ILogger logger)
    {
        await RetryAsync(
            async () =>
            {
                var created = await db.Database.EnsureCreatedAsync();
                if (created)
                    logger.LogInformation("[DB Init] {Name} 已创建（全新库）", name);
                else
                    logger.LogInformation("[DB Init] {Name} 已存在，跳过建库", name);
            },
            name,
            logger);
    }

    /// <summary>
    /// 确保主库中存在 Users 表。
    /// 使用 IF NOT EXISTS DDL 处理"主库已存在但 Users 表尚未创建"的升级场景，
    /// 彻底解决 "对象名 'Users' 无效" 的启动报错。
    /// </summary>
    private static async Task EnsureUsersTableAsync(WcsDbContext db, ILogger logger)
    {
        const string sql = @"
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Users' AND type = 'U'
)
BEGIN
    CREATE TABLE [Users] (
        [Id]           INT            IDENTITY(1,1) NOT NULL,
        [Username]     NVARCHAR(50)   NOT NULL,
        [PasswordHash] NVARCHAR(256)  NOT NULL,
        [Role]         NVARCHAR(20)   NOT NULL,
        [CreatedAt]    DATETIME2(7)   NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END";

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
            logger.LogInformation("[DB Init] Users 表已就绪");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DB Init] 确保 Users 表存在时出错");
            throw;
        }
    }

    /// <summary>写入默认管理员和操作员账户（幂等：已存在则跳过）。</summary>
    private static async Task SeedUsersAsync(WcsDbContext db, ILogger logger)
    {
        try
        {
            var hasher = new PasswordHasher<string>();
            var seeds = new[]
            {
                new UserEntity
                {
                    Username     = "admin",
                    Role         = "admin",
                    PasswordHash = hasher.HashPassword("admin", "Sineva@123"),
                    CreatedAt    = DateTime.UtcNow
                },
                new UserEntity
                {
                    Username     = "user",
                    Role         = "user",
                    PasswordHash = hasher.HashPassword("user", "user@123"),
                    CreatedAt    = DateTime.UtcNow
                }
            };

            bool changed = false;
            foreach (var seed in seeds)
            {
                if (!await db.Users.AnyAsync(u => u.Username == seed.Username))
                {
                    db.Users.Add(seed);
                    changed = true;
                    logger.LogInformation("[DB Init] 添加默认用户：{Username} ({Role})", seed.Username, seed.Role);
                }
            }

            if (changed)
                await db.SaveChangesAsync();

            logger.LogInformation("[DB Init] 默认用户种子数据就绪");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DB Init] 默认用户种子数据初始化失败");
            throw;
        }
    }

    /// <summary>从配置文件加载路径配置。失败时记录警告但不阻断启动。</summary>
    private static async Task LoadPathConfigAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        try
        {
            var svc        = services.GetRequiredService<PathConfigService>();
            var configPath = configuration["PathConfig:JsonPath"] ?? "Config/paths.json";
            await svc.ImportFromFileAsync(configPath);
            logger.LogInformation("[DB Init] 路径配置加载完成");
        }
        catch (Exception ex)
        {
            // 路径配置加载失败不应阻断服务器启动
            logger.LogWarning(ex, "[DB Init] 路径配置加载失败，应用将继续启动");
        }
    }

    /// <summary>根据配置注册 PLC 设备。</summary>
    private static void RegisterDevices(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        try
        {
            var deviceService = services.GetRequiredService<DeviceService>();
            var devices       = configuration.GetSection("Devices").Get<List<DeviceConfig>>() ?? new();
            foreach (var device in devices)
                deviceService.RegisterDevice(device);

            logger.LogInformation("[DB Init] 已注册 {Count} 台设备", devices.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DB Init] 设备注册失败");
            throw;
        }
    }

    /// <summary>
    /// 通用重试辅助：在 <see cref="MaxRetries"/> 次尝试内以指数退避重试 <paramref name="action"/>。
    /// 若所有尝试均失败则重新抛出最后一次异常。
    /// </summary>
    private static async Task RetryAsync(Func<Task> action, string context, ILogger logger)
    {
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (attempt < MaxRetries)
                {
                    var delay = RetryBaseDelay * attempt; // attempt=1 → 2s，attempt=2 → 4s
                    logger.LogWarning(
                        ex,
                        "[DB Init] {Context} 初始化第 {Attempt}/{Max} 次失败，{Delay}s 后重试…",
                        context, attempt, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay);
                }
            }
        }

        logger.LogError(lastEx, "[DB Init] {Context} 在 {Max} 次尝试后仍然失败", context, MaxRetries);
        throw lastEx!;
    }
}
