using System.ComponentModel.DataAnnotations;

namespace StudentTestingSystem.Models.Entities;

public class Group
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Students { get; set; } = new List<User>();

    public ICollection<CourseGroup> CourseGroups { get; set; } = new List<CourseGroup>();
}
