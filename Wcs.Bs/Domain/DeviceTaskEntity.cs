using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wcs.Bs.Domain;

[Table("DeviceTasks")]
public class DeviceTaskEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long TaskId { get; set; }

    [MaxLength(50)]
    public string TaskCode { get; set; } = string.Empty;

    public int StepOrder { get; set; }
    public DeviceType DeviceType { get; set; }

    [MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SegmentSource { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SegmentDest { get; set; } = string.Empty;

    public DeviceTaskStatus Status { get; set; } = DeviceTaskStatus.Waiting;

    public int SendCount { get; set; }
    public DateTime? LastSendTime { get; set; }
    public int TimeoutSeconds { get; set; } = 60;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    [MaxLength(10)]
    public string? RoutingNo { get; set; }

    [ForeignKey(nameof(TaskId))]
    public TaskEntity? Task { get; set; }
}
