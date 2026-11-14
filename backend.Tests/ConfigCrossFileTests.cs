using System.Net.Http.Json;
using System.Text.Json;
using backend.Configuration;

namespace backend.Tests;

// Startup cross-file checks on real copies of config/: broken references stop startup, review flags and
// missing how-to texts only warn. Also how-to serving and the version line.
public class ConfigCrossFileTests
{
    private static void WithConfigCopy(Action<string> edit, Action<string> act)
    {
        var dir = Directory.CreateTempSubdirectory("baseline-xfile-");
        try
        {
            foreach (var f in Directory.GetFiles(TestPaths.ConfigDirectory, "*.yaml"))
                File.Copy(f, Path.Combine(dir.FullName, Path.GetFileName(f)));
            edit(dir.FullName);
            act(dir.FullName);
        }
        finally { dir.Delete(true); }
    }

    private static void Replace(string dir, string file, string from, string to)
    {
        var path = Path.Combine(dir, file);
        var text = File.ReadAllText(path);
        Assert.Contains(from, text); // guard: the edit must hit the real file
        File.WriteAllText(path, text.Replace(from, to));
    }

    public static TheoryData<string, string, string, string> BrokenReferences => new()
    {
        { "chest_pain.yaml", "red_metrics: [hr_rest, bp_sys, bp_dia, spo2]", "red_metrics: [hr_rest, glucose]", "trigger red_metrics: unknown metric 'glucose'" },
        { "chest_pain.yaml", "metrics: [hr_rest, bp_sys, bp_dia, spo2]   # codes", "metrics: [hr_rest, lactate]   # codes", "unknown metric 'lactate'" },
        { "chest_pain.yaml", "src: [S2, S6]", "src: [S2, S9]", "unknown source 'S9'" },
        { "context_rules.yaml", "applies_to: [hr_rest, bp_sys, bp_dia]\n    direction: high\n    condition:\n      type: metric_count\n      metric: sleep_h",
          "applies_to: [hr_rest, glucose]\n    direction: high\n    condition:\n      type: metric_count\n      metric: sleep_h", "applies_to: unknown metric 'glucose'" },
        { "context_rules.yaml", "metric: sleep_h", "metric: sleep_hours", "condition.metric: unknown metric 'sleep_hours'" },
        { "context_rules.yaml", "source: [C1, C2]", "source: [C1, C99]", "unknown source 'C99'" },
        { "context_rules.yaml", "never_downgrade_metrics: [temp, spo2]", "never_downgrade_metrics: [temp, o2]", "never_downgrade_metrics: unknown metric 'o2'" },
        { "metrics.yaml", "instructions_md: howto/hr_rest.md", "instructions_md: ../secrets.md", "instructions_md '../secrets.md' must be a path inside the config folder" },
    };

    [Theory]
    [MemberData(nameof(BrokenReferences))]
    public void Broken_cross_file_reference_stops_startup(string file, string from, string to, string expected)
    {
        WithConfigCopy(dir => Replace(dir, file, from, to), dir =>
        {
            var ex = Assert.Throws<ConfigValidationException>(() => HealthConfig.LoadFromDirectory(dir));
            Assert.Contains(expected, ex.Message);
        });
    }

    [Fact]
    public void Removing_a_metric_breaks_every_file_that_uses_it()
    {
        // Rename spo2 in metrics.yaml only: chest_pain (trigger + measure nodes) and context_rules (never_downgrade) break.
        WithConfigCopy(dir => Replace(dir, "metrics.yaml", "code: spo2", "code: spo2_renamed"), dir =>
        {
            var ex = Assert.Throws<ConfigValidationException>(() => HealthConfig.LoadFromDirectory(dir));
            Assert.Contains("protocol 'chest_pain': trigger red_metrics: unknown metric 'spo2'", ex.Message);
            Assert.Contains("node 'n_vitals': unknown metric 'spo2'", ex.Message);
            Assert.Contains("never_downgrade_metrics: unknown metric 'spo2'", ex.Message);
        });
    }

    [Fact]
    public void Real_config_loads_with_warnings_only()
    {
        var c = HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory);
        Assert.Contains(c.Warnings, w => w.Contains("node 'n_aspirin'") && w.Contains("review: true"));
        Assert.Contains(c.Warnings, w => w.Contains("rule 'adaptation_window'") && w.Contains("review: true"));
        // config/howto/ does not exist yet (Member Y writes it): one warning per metric, never an error.
        Assert.Contains(c.Warnings, w => w.Contains("how-to file config/howto/hr_rest.md not found"));
        Assert.Contains(c.Warnings, w => w.Contains("how-to file config/howto/bp.md not found"));
    }

    [Fact]
    public void Version_line_names_all_three_files()
    {
        var c = HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory);
        var line = c.VersionLine();
        Assert.StartsWith("Config versions: metrics.yaml v", line);
        Assert.Contains($"chest_pain.yaml v{c.Protocols["chest_pain"].Version}", line);
        Assert.Contains($"context_rules.yaml v{c.ContextRules!.Version}", line);
    }

    [Fact]
    public void Missing_how_to_returns_placeholder()
    {
        var c = HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory);
        var h = c.GetHowTo("hr_rest");
        Assert.False(h.Available);
        Assert.Equal("howto/hr_rest.md", h.File);
        Assert.Contains("not been written yet", h.Markdown);
        Assert.Contains("config/howto/hr_rest.md", h.Markdown);
    }

    [Fact]
    public void Existing_how_to_is_served_and_shared_files_work()
    {
        WithConfigCopy(dir =>
        {
            Directory.CreateDirectory(Path.Combine(dir, "howto"));
            File.WriteAllText(Path.Combine(dir, "howto", "bp.md"), "# Blood pressure\n\nSit for 5 minutes first.");
        }, dir =>
        {
            var c = HealthConfig.LoadFromDirectory(dir);
            Assert.True(c.GetHowTo("bp_sys").Available);
            Assert.Equal("# Blood pressure\n\nSit for 5 minutes first.", c.GetHowTo("bp_dia").Markdown); // bp_sys and bp_dia share bp.md
            Assert.DoesNotContain(c.Warnings, w => w.Contains("config/howto/bp.md"));
            Assert.Contains(c.Warnings, w => w.Contains("config/howto/temp.md not found"));
        });
    }
}

public sealed class HowToApiTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task How_to_endpoint_serves_placeholder_without_failing()
    {
        var http = _factory.CreateClient();
        var res = await http.GetFromJsonAsync<JsonElement>("/api/config/howto/spo2");
        var d = res.GetProperty("data");
        Assert.Equal("spo2", d.GetProperty("metricCode").GetString());
        Assert.False(d.GetProperty("available").GetBoolean());
        Assert.Contains("Blood oxygen saturation", d.GetProperty("markdown").GetString());

        var missing = await http.GetAsync("/api/config/howto/glucose");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }
}
