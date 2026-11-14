using backend.Entities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace backend.Configuration;

public sealed class ConfigValidationException(string message) : Exception(message);

// Loaded once at start from the config/ directory. Errors throw so a bad edit fails fast;
// warnings (unopened sources, steps needing medical review, missing sd_pop) are collected in
// Warnings and logged at startup without stopping the app.
public sealed class HealthConfig
{
    // Field names a metric source may list in `fields`.
    public static readonly IReadOnlySet<string> MetricFieldNames = new HashSet<string>
    {
        "plausible_min", "plausible_max", "normal_low", "normal_high", "warn_low", "warn_high",
        "crit_low", "crit_high", "z_warn", "z_crit", "sd_pop",
    };

    public string MetricsVersion { get; }
    public IReadOnlyDictionary<string, HealthMetric> Metrics { get; }
    public IReadOnlyDictionary<string, ProtocolDefinition> Protocols { get; }
    // config/context_rules.yaml; null when the file is absent (context factors are then off, with a warning).
    public ContextRulesFile? ContextRules { get; }
    public IReadOnlyList<string> Warnings => _warnings;

    private readonly List<string> _warnings = [];

    public HealthConfig(
        IEnumerable<HealthMetric> metrics, IEnumerable<ProtocolDefinition> protocols,
        string metricsVersion = "test", ContextRulesFile? contextRules = null)
    {
        MetricsVersion = metricsVersion;
        Metrics = metrics.ToDictionary(m => m.Code);
        Protocols = protocols.ToDictionary(p => p.Id);
        ContextRules = contextRules;
    }

    public const string ContextRulesFileName = "context_rules.yaml";

    public HealthMetric GetMetric(string code) =>
        Metrics.TryGetValue(code, out var m) ? m : throw new KeyNotFoundException($"Unknown metric '{code}'.");

    // The protocol whose trigger lists this symptom code (story C6), or null.
    public ProtocolDefinition? ProtocolForSymptom(string symptomCode) =>
        Protocols.Values.FirstOrDefault(p => p.Trigger?.Symptoms.Contains(symptomCode) == true);

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    // context_rules.yaml is loaded strictly: a misspelled key is an error, not silently ignored.
    private static readonly IDeserializer StrictYaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static T Parse<T>(IDeserializer d, string path)
    {
        try
        {
            return d.Deserialize<T>(File.ReadAllText(path))
                   ?? throw new ConfigValidationException($"{Path.GetFileName(path)} is empty.");
        }
        catch (YamlDotNet.Core.YamlException e)
        {
            var inner = e.InnerException?.Message is { } m ? $" ({m})" : "";
            throw new ConfigValidationException(
                $"{Path.GetFileName(path)} line {e.Start.Line}, column {e.Start.Column}: {e.Message}{inner}");
        }
    }

    public static HealthConfig LoadFromDirectory(string directory)
    {
        var metricsPath = Path.Combine(directory, "metrics.yaml");
        if (!File.Exists(metricsPath))
            throw new ConfigValidationException($"metrics.yaml not found in '{Path.GetFullPath(directory)}'.");

        var metricsFile = Parse<MetricsFile>(Yaml, metricsPath);

        // Every other *.yaml file with a tree_yaml block is a protocol.
        var protocols = new List<ProtocolDefinition>();
        foreach (var path in Directory.GetFiles(directory, "*.yaml").Order())
        {
            if (Path.GetFileName(path) is "metrics.yaml" or ContextRulesFileName) continue;
            if (!File.ReadAllText(path).Contains("tree_yaml:")) continue;
            protocols.Add(Parse<ProtocolDefinition>(Yaml, path));
        }

        var rulesPath = Path.Combine(directory, ContextRulesFileName);
        var contextRules = File.Exists(rulesPath) ? Parse<ContextRulesFile>(StrictYaml, rulesPath) : null;

        var config = new HealthConfig(metricsFile.Metrics, protocols, metricsFile.Version, contextRules);
        config.Validate();
        return config;
    }

    public void Validate()
    {
        var errors = new List<string>();
        _warnings.Clear();

        if (string.IsNullOrWhiteSpace(MetricsVersion)) errors.Add("metrics.yaml: version is required");

        foreach (var m in Metrics.Values)
        {
            string P(string msg) => $"metric '{m.Code}': {msg}";

            if (string.IsNullOrWhiteSpace(m.Name) || string.IsNullOrWhiteSpace(m.Unit))
                errors.Add(P("name and unit are required"));
            if (m.PlausibleMin >= m.PlausibleMax)
                errors.Add(P("plausible_min must be below plausible_max"));
            if (m.Polarity is not ("both" or "low" or "high" or "none"))
                errors.Add(P($"polarity '{m.Polarity}' must be both, low, high or none"));

            if (m.HasStatus)
            {
                if (m.ZWarn is null || m.ZCrit is null) errors.Add(P("z_warn and z_crit are required"));
                else if (m.ZWarn >= m.ZCrit) errors.Add(P("z_warn must be below z_crit"));
                if (m.WarnLow is null && m.WarnHigh is null) errors.Add(P("needs warn_low or warn_high"));
                if (m.CritLow is { } cl && m.WarnLow is { } wl && cl > wl) errors.Add(P("crit_low must be <= warn_low"));
                if (m.CritHigh is { } ch && m.WarnHigh is { } wh && ch < wh) errors.Add(P("crit_high must be >= warn_high"));
                if (m.SdPop is <= 0) errors.Add(P("sd_pop must be positive"));
                if (m.SdPop is null)
                    _warnings.Add(P("sd_pop is null; baselines with n < 10 use the person's own SD (no shrinkage)"));
            }

            foreach (var s in m.Sources)
            {
                var label = string.Join(": ", new[] { s.Publisher, s.Title }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } l ? l : "(unnamed)";
                if (s.Fields.Count == 0) errors.Add(P($"source '{label}' must list the fields it covers"));
                foreach (var f in s.Fields)
                    if (!MetricFieldNames.Contains(f)) errors.Add(P($"source '{label}' lists unknown field '{f}'"));
                if (s.Opened == false)
                    _warnings.Add(P($"source '{label}' has opened: false (seen in a search result only)"));
            }
        }

        foreach (var p in Protocols.Values)
        {
            string P(string msg) => $"protocol '{p.Id}': {msg}";
            if (string.IsNullOrWhiteSpace(p.Version)) errors.Add(P("version is required"));

            var ids = new HashSet<string>();
            foreach (var n in p.TreeYaml)
                if (!ids.Add(n.Id)) errors.Add(P($"duplicate node id '{n.Id}'"));
            var sourceIds = p.Sources.Select(s => s.Id).Append("team").ToHashSet();

            if (!ids.Contains(p.Start)) errors.Add(P($"start node '{p.Start}' does not exist"));

            if (p.Trigger is { } t)
            {
                if (t.Symptoms.Count == 0 && t.RedMetrics.Count == 0)
                    errors.Add(P("trigger needs symptoms or red_metrics"));
                if (t.RequiresSymptom && t.Symptoms.Count == 0)
                    errors.Add(P("trigger has requires_symptom: true but no symptoms"));
                foreach (var code in t.RedMetrics)
                    if (!Metrics.TryGetValue(code, out var tm)) errors.Add(P($"trigger red_metrics: unknown metric '{code}'"));
                    else if (!tm.HasStatus) errors.Add(P($"trigger red_metrics: '{code}' has no status, so it can never be red"));
                if (t.WindowMin is <= 0) errors.Add(P("trigger window_min must be a whole number of minutes above 0"));
                if (t.RequiresSymptom && t.RedMetrics.Count > 0 && t.WindowMin is null)
                    errors.Add(P("trigger window_min is required when requires_symptom is true"));
            }

            foreach (var n in p.TreeYaml)
            {
                string N(string msg) => P($"node '{n.Id}': {msg}");
                switch (n.Type)
                {
                    case "question":
                        if (n.Options.Count == 0) errors.Add(N("question needs options"));
                        foreach (var o in n.Options)
                            if (!ids.Contains(o.Next)) errors.Add(N($"option '{o.Label}' points to missing node '{o.Next}'"));
                        break;
                    case "action":
                    case "measure":
                        if (n.Next is null || !ids.Contains(n.Next)) errors.Add(N($"next '{n.Next}' does not exist"));
                        if (n.Type == "measure")
                            foreach (var code in n.Metrics)
                                if (!Metrics.ContainsKey(code)) errors.Add(N($"unknown metric '{code}'"));
                        break;
                    case "end":
                        if (n.Next is not null) errors.Add(N("end node must not have next"));
                        if (string.IsNullOrWhiteSpace(n.Outcome)) errors.Add(N("end node needs an outcome"));
                        break;
                    default:
                        errors.Add(N($"unknown type '{n.Type}'"));
                        break;
                }
                if (n.Type != "end" && n.Outcome is not null) errors.Add(N("only end nodes may have an outcome"));
                foreach (var s in n.Src)
                    if (!sourceIds.Contains(s)) errors.Add(N($"unknown source '{s}'"));
                if (n.Review)
                    _warnings.Add(N("review: true (needs flight-surgeon review before real use)"));
            }

            var outcomes = p.TreeYaml.Where(n => n.Outcome is not null).GroupBy(n => n.Outcome);
            foreach (var dup in outcomes.Where(g => g.Count() > 1))
                errors.Add(P($"outcome '{dup.Key}' is used by more than one end node"));

            foreach (var s in p.Sources.Where(s => !s.Opened))
                _warnings.Add(P($"source {s.Id} has opened: false (seen in a search result only)"));
        }

        ValidateContextRules(errors);

        if (errors.Count > 0)
            throw new ConfigValidationException("Invalid health config:\n  " + string.Join("\n  ", errors));
    }

    private static readonly HashSet<string> Ops = ["lt", "gt", "lte", "gte"];
    private static readonly HashSet<string> EnvironmentKinds = ["co2_mmhg", "kp", "solar_particle_event"];

    private void ValidateContextRules(List<string> errors)
    {
        if (ContextRules is not { } cr)
        {
            _warnings.Add($"{ContextRulesFileName} not found: context factors (stories I1-I5) are off");
            return;
        }

        string F(string msg) => $"context_rules: {msg}";
        if (string.IsNullOrWhiteSpace(cr.Version)) errors.Add(F("version is required"));

        if (cr.Policy is not { } pol) errors.Add(F("policy is required"));
        else
        {
            if (pol.MaxTotalDowngrade is not (0 or 1)) errors.Add(F("policy.max_total_downgrade must be 0 or 1"));
            if (pol.DowngradeOnlyFrom is not ("yellow" or "red")) errors.Add(F("policy.downgrade_only_from must be yellow or red"));
            foreach (var code in pol.NeverDowngradeMetrics)
                if (!Metrics.ContainsKey(code)) errors.Add(F($"policy.never_downgrade_metrics: unknown metric '{code}'"));
        }

        var sourceIds = new HashSet<string>();
        foreach (var s in cr.Sources)
        {
            if (string.IsNullOrWhiteSpace(s.Id)) { errors.Add(F("every source needs an id")); continue; }
            if (!sourceIds.Add(s.Id)) errors.Add(F($"duplicate source id '{s.Id}'"));
            if (s.Opened == false) _warnings.Add(F($"source {s.Id} has opened: false (seen in a search result only)"));
        }
        sourceIds.Add("team");

        if (cr.Rules.Count == 0) errors.Add(F("rules must not be empty"));
        var ruleIds = new HashSet<string>();
        for (var i = 0; i < cr.Rules.Count; i++)
        {
            var r = cr.Rules[i];
            var name = string.IsNullOrWhiteSpace(r.Id) ? $"#{i + 1}" : $"'{r.Id}'";
            string R(string msg) => F($"rule {name}: {msg}");

            if (string.IsNullOrWhiteSpace(r.Id)) errors.Add(R("id is required"));
            else if (!ruleIds.Add(r.Id)) errors.Add(R("duplicate id"));
            if (r.Enabled is null) errors.Add(R("enabled is required"));
            if (r.Review is null) errors.Add(R("review is required"));
            if (string.IsNullOrWhiteSpace(r.Message)) errors.Add(R("message is required"));
            if (r.Direction is not ("high" or "low" or "any")) errors.Add(R("direction must be high, low or any"));
            if (r.SeverityModifier is not (0 or -1)) errors.Add(R("severity_modifier must be 0 or -1"));

            if (r.AppliesTo.Count == 0) errors.Add(R("applies_to must list at least one metric"));
            foreach (var code in r.AppliesTo)
                if (!Metrics.TryGetValue(code, out var m)) errors.Add(R($"applies_to: unknown metric '{code}'"));
                else if (!m.HasStatus) errors.Add(R($"applies_to: '{code}' has no status, so it is never flagged"));

            if (r.Source.Count == 0) errors.Add(R("source must list at least one id"));
            foreach (var s in r.Source)
                if (!sourceIds.Contains(s)) errors.Add(R($"unknown source '{s}'"));

            if (r.Condition is null) errors.Add(R("condition is required"));
            else ValidateCondition(r.Condition, R, errors);

            if (r.Review == true) _warnings.Add(R("review: true (needs medical review before real use)"));
        }
    }

    private void ValidateCondition(ContextCondition c, Func<string, string> R, List<string> errors)
    {
        // Each type allows exactly these fields (besides "type").
        var allowed = c.Type switch
        {
            "metric_count" => new HashSet<string> { "metric", "op", "value", "window_days", "min_count" },
            "mission_day" => ["op", "value"],
            "environment" => c.Kind == "solar_particle_event"
                ? ["kind", "window_h"]
                : ["kind", "op", "value", "window_h"],
            "metric_recent" => ["metric", "op", "value", "window_h"],
            _ => null,
        };
        if (allowed is null)
        {
            errors.Add(R($"condition.type '{c.Type}' must be metric_count, mission_day, environment or metric_recent"));
            return;
        }

        var present = new Dictionary<string, bool>
        {
            ["metric"] = c.Metric is not null, ["op"] = c.Op is not null, ["value"] = c.Value is not null,
            ["window_days"] = c.WindowDays is not null, ["min_count"] = c.MinCount is not null,
            ["kind"] = c.Kind is not null, ["window_h"] = c.WindowH is not null,
        };
        foreach (var (field, isSet) in present)
        {
            if (allowed.Contains(field) && !isSet) errors.Add(R($"condition ({c.Type}) needs {field}"));
            if (!allowed.Contains(field) && isSet) errors.Add(R($"condition ({c.Type}) does not use {field}"));
        }

        if (c.Op is not null && !Ops.Contains(c.Op)) errors.Add(R($"condition.op '{c.Op}' must be lt, gt, lte or gte"));
        if (c.Metric is not null && !Metrics.ContainsKey(c.Metric)) errors.Add(R($"condition.metric: unknown metric '{c.Metric}'"));
        if (c.Kind is not null && !EnvironmentKinds.Contains(c.Kind))
            errors.Add(R($"condition.kind '{c.Kind}' must be co2_mmhg, kp or solar_particle_event"));
        if (c.WindowDays is { } wd && (wd < 1 || wd != Math.Floor(wd)))
            errors.Add(R("condition.window_days must be a whole number of mission days (1 or more, counting today)"));
        if (c.WindowH is <= 0) errors.Add(R("condition.window_h must be above 0"));
        if (c.MinCount is < 1) errors.Add(R("condition.min_count must be at least 1"));
    }

    // Valid end outcomes of a protocol (for validating protocol-run ends).
    public static IReadOnlySet<string> Outcomes(ProtocolDefinition p) =>
        p.TreeYaml.Where(n => n.Type == "end" && n.Outcome is not null).Select(n => n.Outcome!).ToHashSet();
}
