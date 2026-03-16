using System.ComponentModel.DataAnnotations;

namespace StudentTestingSystem.Models.Entities;

public enum UserRole
{
    Student = 0,
    Teacher = 1
}

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? GroupNumber { get; set; }
    public int? GroupId { get; set; }          // FK
    public Group? Group { get; set; }          // навигация

    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }

    public bool IsBlocked { get; set; } = false;

    public virtual ICollection<StudentResult> StudentResults { get; set; } = new List<StudentResult>();
    public virtual ICollection<PasswordResetRequest> PasswordResetRequests { get; set; } = new List<PasswordResetRequest>();
}
