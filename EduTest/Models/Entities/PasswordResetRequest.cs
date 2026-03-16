using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentTestingSystem.Models.Entities;

public class PasswordResetRequest
{
    [Key]
    public int Id { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.Now;

    public bool IsProcessed { get; set; } = false;

    public DateTime? ProcessedAt { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}
