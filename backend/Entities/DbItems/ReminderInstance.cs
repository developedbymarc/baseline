using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class ReminderInstance
{
    public ReminderInstance()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public ReminderRule Rule { get; set; } = null!;

    public DateTimeOffset DueAt { get; set; }

    public ReminderStatus Status { get; set; }
}
