namespace backend.Entities;

public class CrewMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int Age { get; set; }
    public string Sex { get; set; } = null!;
    public int HeightCm { get; set; }
    public string? Notes { get; set; }
}
