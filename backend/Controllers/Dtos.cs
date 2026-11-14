using System.Text.Json;
using backend.Entities;
using backend.Services.Clock;
using backend.Services.Link;

namespace backend.Controllers;

public sealed record MissionStateDto(
    Guid Id, string Name, MissionEnvironment Environment, DateTimeOffset LaunchDate, MissionMode Mode,
    DateTimeOffset MissionNow, double MetSeconds, string Met, double ClockRate, bool ClockPaused,
    double OneWayLatencySeconds, bool LinkUp, bool OnboardFirst, double OnboardFirstThresholdSeconds)
{
    public static MissionStateDto From(Mission m, DateTimeOffset now, double onboardFirstThreshold)
    {
        var met = MissionClock.Met(m, now);
        var sign = met < TimeSpan.Zero ? "-" : "";
        var abs = met.Duration();
        return new MissionStateDto(m.Id, m.Name, m.Environment, m.LaunchDate, m.Mode, now, met.TotalSeconds,
            $"{sign}{(int)abs.TotalDays}d {abs.Hours:00}:{abs.Minutes:00}:{abs.Seconds:00}",
            m.ClockRate, m.ClockPaused, m.OneWayLatencySeconds, m.LinkUp,
            // E3: on-board protocol first when the link is down or slower than the threshold.
            !m.LinkUp || m.OneWayLatencySeconds > onboardFirstThreshold, onboardFirstThreshold);
    }
}

public sealed record LinkMessageDto(
    Guid Id, LinkDirection Direction, LinkMessageType Type, Guid? AlertId, string State,
    DateTimeOffset SentAt, DateTimeOffset? DeliverAt, DateTimeOffset? DeliveredAt,
    double AgeSeconds, double? SecondsUntilDelivery, JsonElement Payload)
{
    public static LinkMessageDto From(LinkMessage m, DateTimeOffset now) => new(
        m.Id, m.Direction, m.Type, m.AlertId, LinkQueue.State(m), m.SentAt, m.DeliverAt, m.DeliveredAt,
        (now - m.SentAt).TotalSeconds,
        m.DeliveredAt is null && m.DeliverAt is { } at ? Math.Max(0, (at - now).TotalSeconds) : null,
        JsonDocument.Parse(m.Payload).RootElement.Clone());
}
