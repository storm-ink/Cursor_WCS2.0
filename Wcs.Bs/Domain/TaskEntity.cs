using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wcs.Bs.Domain;

[Table("Tasks")]
public class TaskEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [MaxLength(50)]
    public string TaskCode { get; set; } = string.Empty;

    public TaskSource Source { get; set; }
    public TaskType Type { get; set; }

    [MaxLength(50)]
    public string PalletCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string StartLocationCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? StartLocationDeviceName { get; set; }

    [MaxLength(50)]
    public string EndLocationCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EndLocationDeviceName { get; set; }

    [MaxLength(50)]
    public string? CurrentLocationCode { get; set; }

    [MaxLength(50)]
    public string? CurrentLocationDeviceName { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Created;

    [MaxLength(50)]
    public string? PathCode { get; set; }

    public int CurrentStepOrder { get; set; }
    public int TotalSteps { get; set; }
    public int Priority { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public List<DeviceTaskEntity> DeviceTasks { get; set; } = new();
}
