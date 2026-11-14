using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class HealthMetric
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public double PlausibleMin { get; set; }
    public double PlausibleMax { get; set; }
    public double NormalLow { get; set; }
    public double NormalHigh { get; set; }
    public double WarningLow { get; set; }
    public double WarningHigh { get; set; }
    public double CriticalLow { get; set; }
    public double CriticalHigh { get; set; }
    public double ZWarning { get; set; }
    public double ZCritical { get; set; }
    public string Polarity { get; set; } = null!;
    public string InstructionsMd { get; set; } = null!;
}
