using Wcs.Bs.Domain;

namespace Wcs.Bs.Services;

/// <summary>
/// 数据清理后台服务：定期执行归档（当前库→历史库+备份库）和过期数据清理
/// </summary>
public class DataCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataCleanupService> _logger;
    private readonly int _retainDays;
    private readonly DataArchiveConfig _archiveConfig;

    public DataCleanupService(IServiceProvider serviceProvider, ILogger<DataCleanupService> logger, IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _retainDays = config.GetValue("DataCleanup:RetainDays", 30);
        _archiveConfig = config.GetSection("DataArchive").Get<DataArchiveConfig>() ?? new DataArchiveConfig();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(_archiveConfig.CleanupIntervalHours), stoppingToken);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var archiveService = scope.ServiceProvider.GetRequiredService<DataArchiveService>();

                // 1. 归档当前库中的旧数据到历史库和备份库
                await archiveService.ArchiveOldTasksAsync(_retainDays);

                // 2. 清理历史库中超过保留月数的数据
                await archiveService.CleanupHistoryAsync(_archiveConfig.HistoryRetentionMonths);

                // 3. 清理备份库中超过保留月数的数据
                await archiveService.CleanupBackupAsync(_archiveConfig.BackupRetentionMonths);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cleanup] Error during data archive and cleanup");
            }
        }
    }
}
