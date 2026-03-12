using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wcs.Bs.Domain;

[Table("Users")]
public class UserEntity
{
    public const int MaxUsernameLength = 50;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(MaxUsernameLength)]
    [Required]
    public string Username { get; set; } = string.Empty;

    [MaxLength(256)]
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>角色: admin | user | guest</summary>
    [MaxLength(20)]
    public string Role { get; set; } = "user";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
