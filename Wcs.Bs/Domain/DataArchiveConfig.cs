namespace Wcs.Bs.Domain;

/// <summary>
/// 数据归档配置：控制历史表/备份表的保留时长和重置开关
/// </summary>
public class DataArchiveConfig
{
    /// <summary>历史表保留月数（超过此时长的数据将被清理）</summary>
    public int HistoryRetentionMonths { get; set; } = 3;

    /// <summary>备份表保留月数（超过此时长的数据将被清理）</summary>
    public int BackupRetentionMonths { get; set; } = 12;

    /// <summary>是否允许重置当前表数据库</summary>
    public bool EnableCurrentReset { get; set; } = false;

    /// <summary>是否允许重置历史表数据库</summary>
    public bool EnableHistoryReset { get; set; } = false;

    /// <summary>归档清理间隔（小时）</summary>
    public int CleanupIntervalHours { get; set; } = 6;
}
