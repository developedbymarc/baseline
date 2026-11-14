namespace backend.Entities;

// Locked at mission start (Mission.Mode -> Inflight). ApplicationDbContext refuses to modify
// or delete a locked baseline; there is no audit table (spec section 16).
public class Baseline
{
    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public string MetricCode { get; set; } = null!;

    public double Mean { get; set; }
    // Personal pre-flight sample SD. The shrunk SD used for z-scores is derived from
    // StandardDeviation, SampleSize and the metric's sd_pop (see BaselineEngine).
    public double StandardDeviation { get; set; }
    public int SampleSize { get; set; }

    // Not mapped (no setter). BASELINE.method is still undecided, left as the team wrote it.
    public string Method => SampleSize switch
    {
        > 10 => "sample",
        _ => "shrunk"
    };

    public DateTimeOffset? LockedAt { get; set; }
    public bool IsLocked => LockedAt is not null;
}
