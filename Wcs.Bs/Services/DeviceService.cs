using System.Collections.Concurrent;
using Wcs.Bs.Domain;
using Wcs.Bs.Plc;

namespace Wcs.Bs.Services;

public class DeviceService
{
    private readonly ConcurrentDictionary<string, PlcClient> _clients = new();
    private readonly ConcurrentDictionary<string, DeviceConfig> _configs = new();
    private readonly ConcurrentDictionary<string, DeviceStatus> _statuses = new();
    private readonly ConcurrentDictionary<string, List<DeviceMessage>> _messages = new();
    private readonly ILogger<DeviceService> _logger;
    private const int MaxMessages = 500;

    public DeviceService(ILogger<DeviceService> logger)
    {
        _logger = logger;
    }

    public void RegisterDevice(DeviceConfig config)
    {
        _configs[config.Code] = config;
        _statuses[config.Code] = new DeviceStatus
        {
            Code = config.Code,
            Type = config.Type,
            IsConnected = false,
            IsEnabled = true,
            LastUpdate = DateTime.Now
        };
        _messages[config.Code] = new List<DeviceMessage>();
    }

    public PlcClient? GetClient(string deviceCode) =>
        _clients.TryGetValue(deviceCode, out var client) ? client : null;

    public void SetClient(string deviceCode, PlcClient client) =>
        _clients[deviceCode] = client;

    public List<DeviceStatus> GetAllStatuses() =>
        _statuses.Values.ToList();

    public DeviceStatus? GetStatus(string deviceCode) =>
        _statuses.TryGetValue(deviceCode, out var status) ? status : null;

    public void UpdateStatus(string deviceCode, Action<DeviceStatus> update)
    {
        if (_statuses.TryGetValue(deviceCode, out var status))
        {
            update(status);
            status.LastUpdate = DateTime.Now;
        }
    }

    public bool SetEnabled(string deviceCode, bool enabled)
    {
        if (!_statuses.TryGetValue(deviceCode, out var status)) return false;
        status.IsEnabled = enabled;
        status.LastUpdate = DateTime.Now;
        _logger.LogInformation("[Device] {Code} {Action}", deviceCode, enabled ? "启用" : "禁用");
        return true;
    }

    public bool IsEnabled(string deviceCode)
    {
        return _statuses.TryGetValue(deviceCode, out var s) && s.IsEnabled;
    }

    public void AddMessage(string deviceCode, string direction, string rawData, string? parsedSummary = null)
    {
        if (!_messages.TryGetValue(deviceCode, out var list)) return;

        lock (list)
        {
            list.Add(new DeviceMessage
            {
                DeviceCode = deviceCode,
                Direction = direction,
                RawData = rawData,
                ParsedSummary = parsedSummary,
                Timestamp = DateTime.Now
            });

            while (list.Count > MaxMessages)
                list.RemoveAt(0);
        }
    }

    public List<DeviceMessage> GetMessages(string deviceCode, int count = 500)
    {
        if (!_messages.TryGetValue(deviceCode, out var list)) return new();
        lock (list)
        {
            return list.TakeLast(count).ToList();
        }
    }

    public List<DeviceConfig> GetAllConfigs() => _configs.Values.ToList();
}

public class DeviceStatus
{
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsConnected { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string CurrentTaskNo { get; set; } = "";
    public string State { get; set; } = "Idle";
    public DateTime LastUpdate { get; set; }
}

public class DeviceMessage
{
    public string DeviceCode { get; set; } = "";
    public string Direction { get; set; } = "";
    public string RawData { get; set; } = "";
    public string? ParsedSummary { get; set; }
    public DateTime Timestamp { get; set; }
}
