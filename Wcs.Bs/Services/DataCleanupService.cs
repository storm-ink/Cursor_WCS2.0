namespace Wcs.Bs.Services;

public class DataCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataCleanupService> _logger;
    private readonly int _retainDays;

    public DataCleanupService(IServiceProvider serviceProvider, ILogger<DataCleanupService> logger, IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _retainDays = config.GetValue("DataCleanup:RetainDays", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
                await taskService.CleanupOldTasksAsync(_retainDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cleanup] Error during data cleanup");
            }
        }
    }
}
