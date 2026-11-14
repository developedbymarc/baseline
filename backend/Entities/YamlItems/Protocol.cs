namespace backend.Entities;

// A protocol decision tree such as config/chest_pain.yaml. Rendered by one generic runner component.
public class ProtocolDefinition
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Version { get; set; } = null!;
    public string? Language { get; set; }
    public string Start { get; set; } = null!;
    public string? Disclaimer { get; set; }
    public ProtocolTrigger? Trigger { get; set; }
    // Display text only. The real threshold is Link:OnboardFirstThresholdSeconds in appsettings.
    public string? LatencyRule { get; set; }
    public List<ProtocolNode> TreeYaml { get; set; } = [];
    public List<ProtocolSource> Sources { get; set; } = [];
}

// When the app offers to open a protocol (story E1).
public class ProtocolTrigger
{
    // Symptom codes from the symptom checklist that open this protocol.
    public List<string> Symptoms { get; set; } = [];
    // A red result on any of these metrics ...
    public List<string> RedMetrics { get; set; } = [];
    // ... only opens the protocol if one of the symptoms is also logged.
    public bool RequiresSymptom { get; set; }
    // "Also logged" = within this many minutes of MISSION time, before or after the red reading.
    public int? WindowMin { get; set; }
}

public class ProtocolNode
{
    public string Id { get; set; } = null!;
    // question | action | measure | end
    public string Type { get; set; } = null!;
    public string Text { get; set; } = null!;
    public List<ProtocolOption> Options { get; set; } = [];
    public string? Next { get; set; }
    public List<string> Metrics { get; set; } = [];
    public List<string> Src { get; set; } = [];
    public string? Outcome { get; set; }
    public bool LogAnswer { get; set; }
    public bool Log { get; set; }
    public int? TimerS { get; set; }
    public bool BuildPacket { get; set; }
    public bool NotifyGround { get; set; }
    public bool Review { get; set; }
    public string? Note { get; set; }
}

public class ProtocolOption
{
    public string Label { get; set; } = null!;
    public string Next { get; set; } = null!;
}

public class ProtocolSource
{
    public string Id { get; set; } = null!;
    public string? Publisher { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public string? Reviewed { get; set; }
    public string? Updated { get; set; }
    public string? Checked { get; set; }
    public bool Opened { get; set; }
}
