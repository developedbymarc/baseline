namespace backend.Entities;

// The latency simulator. Nothing crosses between crew and ground except through this table.
public class LinkMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    public Guid? AlertId { get; set; }
    public Alert? Alert { get; set; }

    public LinkDirection Direction { get; set; }
    public LinkMessageType Type { get; set; }
    public string Payload { get; set; } = "{}";

    // Original creation time on the sender's side, in mission time. Never changes.
    public DateTimeOffset SentAt { get; set; }
    // SentAt + latency. Null while the message is held because the link was down when it was sent;
    // set to (reconnect time + latency) when the link comes back.
    public DateTimeOffset? DeliverAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}
