namespace Wcs.Bs.Domain;

public class DeviceConfig
{
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string PlcIp { get; set; } = string.Empty;
    public int PlcPort { get; set; }
}

public class PlcConfig
{
    public int TaskTimeoutSeconds { get; set; } = 60;
    public int MaxRetryCount { get; set; } = 3;
}

public class CvPlcConfig
{
    public int TaskReportCount { get; set; } = 5;
    public int LocationCount { get; set; } = 10;
}
