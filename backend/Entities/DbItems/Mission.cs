using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class Mission
{
    [Required]
    public Guid Id;

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public Environment Environment { get; set; }

    [Required]
    public DateTimeOffset LaunchDate { get; set; }

    [Required]
    public int OneWayLatencyInSeconds { get; set; }
}
