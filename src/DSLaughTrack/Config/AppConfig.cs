using System.Text.Json;

namespace DSLaughTrack.Config;

public sealed class TriggerConfig
{
    public bool Enabled { get; set; } = true;
    public double Volume { get; set; } = 1.0;
    public double CooldownSeconds { get; set; } = 5.0;
    /// When true, this trigger's sound stops whatever is currently playing and
    /// takes over, instead of being skipped by the no-overlap gate.
    public bool Interrupt { get; set; } = false;
    public List<string> Sounds { get; set; } = new();
}

public sealed class AppConfig
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public double MasterVolume { get; set; } = 0.8;
    public double GlobalCooldownSeconds { get; set; } = 2.0;
    public int PollHz { get; set; } = 30;
    public string LogLevel { get; set; } = "info";
    /// Optional override for where sound files are read from. Absolute, or relative
    /// to the exe folder. Null/empty = "sounds" next to the exe. Applied at startup only.
    public string? SoundsPath { get; set; }
    public Dictionary<string, TriggerConfig> Triggers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ResolveSoundsRoot(string baseDir) =>
        string.IsNullOrWhiteSpace(SoundsPath)
            ? Path.Combine(baseDir, "sounds")
            : Path.GetFullPath(SoundsPath, baseDir);

    public TriggerConfig For(string key) =>
        Triggers.TryGetValue(key, out var t) ? t : new TriggerConfig();

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();
        var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOpts);
        if (cfg != null)
        {
            cfg.Triggers = new Dictionary<string, TriggerConfig>(cfg.Triggers, StringComparer.OrdinalIgnoreCase);
        }
        return cfg ?? new AppConfig();
    }
}
