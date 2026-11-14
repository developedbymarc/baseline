namespace backend.Entities;

public enum Environment
{
    Orbit,
    SpaceStation,
    LunarSettlement,
    MartianSettlement,
}

public enum EnvironmentMetric
{
    Temperature,
    Pressure,
    Humidity,
    Radiation,
}

public enum HealthMeasurementSource
{
    PreFlight,
    InFlight,
}

public enum ReportWindow
{
    Biweekly,
    Monthly,
}

public enum ReminderStatus
{
    Pending,
    Done,
    Late,
    Missed,
}
