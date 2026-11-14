using backend.Entities;
using backend.Services.Link;

namespace backend.Tests;

public class LinkQueueTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private static Mission Mars(bool linkUp = true) => new() { Name = "t", OneWayLatencySeconds = 720, LinkUp = linkUp };

    private static LinkMessage Send(Mission m, DateTimeOffset at) =>
        LinkQueue.Create(m, at, LinkDirection.CrewToGround, LinkMessageType.Packet, "{}", Guid.NewGuid());

    [Fact]
    public void Deliver_at_is_sent_at_plus_latency()
    {
        var msg = Send(Mars(), T0);
        Assert.Equal(T0, msg.SentAt);
        Assert.Equal(T0.AddMinutes(12), msg.DeliverAt);
        Assert.Equal("in_flight", LinkQueue.State(msg));
    }

    [Fact]
    public void Not_delivered_before_latency_elapses()
    {
        var msg = Send(Mars(), T0);
        Assert.Empty(LinkQueue.DeliverDue([msg], T0.AddMinutes(12).AddTicks(-1)));
        Assert.Null(msg.DeliveredAt);
    }

    [Fact]
    public void Delivered_at_exact_deliver_time_even_when_processed_late()
    {
        var msg = Send(Mars(), T0);
        var delivered = LinkQueue.DeliverDue([msg], T0.AddDays(2)); // e.g. after a fast-forward
        Assert.Single(delivered);
        Assert.Equal(T0.AddMinutes(12), msg.DeliveredAt);
        Assert.Equal(T0, msg.SentAt); // original timestamp kept
        Assert.Equal("delivered", LinkQueue.State(msg));
    }

    [Fact]
    public void Delivery_is_idempotent()
    {
        var msg = Send(Mars(), T0);
        LinkQueue.DeliverDue([msg], T0.AddHours(1));
        Assert.Empty(LinkQueue.DeliverDue([msg], T0.AddHours(2)));
    }

    [Fact]
    public void Messages_sent_while_link_is_down_are_held_until_reconnect_plus_latency()
    {
        var m = Mars(linkUp: false);
        var msg = Send(m, T0);
        Assert.Null(msg.DeliverAt);
        Assert.Equal("held", LinkQueue.State(msg));
        Assert.Empty(LinkQueue.DeliverDue([msg], T0.AddDays(1)));

        var reconnect = T0.AddHours(3);
        LinkQueue.SetLink(m, true, reconnect, [msg]);
        Assert.True(m.LinkUp);
        Assert.Equal(reconnect.AddMinutes(12), msg.DeliverAt);
        Assert.Equal(T0, msg.SentAt);
    }

    [Fact]
    public void In_flight_messages_still_arrive_when_link_drops()
    {
        var m = Mars();
        var msg = Send(m, T0);
        LinkQueue.SetLink(m, false, T0.AddMinutes(1), []);
        Assert.Single(LinkQueue.DeliverDue([msg], T0.AddMinutes(12)));
    }

    [Fact]
    public void Toggling_up_when_already_up_does_not_reschedule()
    {
        var m = Mars();
        var held = new LinkMessage { SentAt = T0 };
        LinkQueue.SetLink(m, true, T0.AddHours(1), [held]);
        Assert.Null(held.DeliverAt);
    }

    [Fact]
    public void Zero_latency_delivers_immediately()
    {
        var m = new Mission { Name = "t", OneWayLatencySeconds = 0, LinkUp = true };
        var msg = Send(m, T0);
        Assert.Single(LinkQueue.DeliverDue([msg], T0));
    }
}
