using backend.Entities;

namespace backend.Services.Link;

// WP 3.4. The latency simulator as pure rules over LinkMessage rows; callers load and save them.
//  - Sent while the link is up:   DeliverAt = SentAt + one-way latency.
//  - Sent while the link is down: DeliverAt = null (held). On reconnect, DeliverAt = reconnect time + latency.
//  - Messages already in flight when the link drops still arrive (the signal has already left).
//  - Delivered once mission time reaches DeliverAt; DeliveredAt = DeliverAt, so fast-forward stays exact.
//  - SentAt is the original creation time and never changes (E2, H2).
public static class LinkQueue
{
    public static LinkMessage Create(
        Mission mission, DateTimeOffset missionNow, LinkDirection direction, LinkMessageType type,
        string payload, Guid? alertId = null) => new()
        {
            MissionId = mission.Id,
            AlertId = alertId,
            Direction = direction,
            Type = type,
            Payload = payload,
            SentAt = missionNow,
            DeliverAt = mission.LinkUp ? missionNow + Latency(mission) : null,
        };

    public static TimeSpan Latency(Mission mission) => TimeSpan.FromSeconds(mission.OneWayLatencySeconds);

    // Call with the held messages of this mission (DeliverAt == null) when the link state changes.
    public static void SetLink(Mission mission, bool up, DateTimeOffset missionNow, IEnumerable<LinkMessage> held)
    {
        var wasUp = mission.LinkUp;
        mission.LinkUp = up;
        if (up && !wasUp)
            foreach (var msg in held.Where(m => m.DeliverAt is null))
                msg.DeliverAt = missionNow + Latency(mission);
    }

    // Marks due messages delivered. Returns the ones delivered by this call.
    public static List<LinkMessage> DeliverDue(IEnumerable<LinkMessage> undelivered, DateTimeOffset missionNow)
    {
        var delivered = new List<LinkMessage>();
        foreach (var msg in undelivered)
        {
            if (msg.DeliveredAt is not null || msg.DeliverAt is not { } at || at > missionNow) continue;
            msg.DeliveredAt = at;
            delivered.Add(msg);
        }
        return delivered;
    }

    public static string State(LinkMessage m) =>
        m.DeliveredAt is not null ? "delivered" : m.DeliverAt is null ? "held" : "in_flight";
}
