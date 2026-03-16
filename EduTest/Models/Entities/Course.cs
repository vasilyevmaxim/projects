using System.ComponentModel.DataAnnotations;

namespace StudentTestingSystem.Models.Entities;

public class Course
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual ICollection<Test> Tests { get; set; } = new List<Test>();

    public ICollection<CourseGroup> CourseGroups { get; set; } = new List<CourseGroup>();
    public bool IsHidden { get; set; }          // полностью скрыт от студентов
    public bool IsLocked { get; set; }          // виден, но недоступен
}