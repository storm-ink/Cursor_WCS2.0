using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wcs.Bs.Domain;

[Table("CraneReachableConfigs")]
public class CraneReachableConfigEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ReachablePattern { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
