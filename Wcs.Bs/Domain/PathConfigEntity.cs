using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wcs.Bs.Domain;

[Table("PathConfigs")]
public class PathConfigEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [MaxLength(50)]
    public string PathCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DestinationPattern { get; set; } = string.Empty;

    public string ConfigJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
