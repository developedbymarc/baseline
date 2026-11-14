using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class CrewMember : IdentityUser<Guid>
{
    public CrewMember()
    {
        Id = Guid.NewGuid();
    }

    [Required]
    public string FirstName { get; set; } = null!;

    [Required]
    public string LastName { get; set; } = null!;

    [Required]
    public string Role { get; set; } = null!;

    [Required]
    public DateTimeOffset DateOfBirth { get; set; }

    [Required]
    public int HeightInCm { get; set; }

    [Required]
    public string Notes { get; set; } = null!;
}
