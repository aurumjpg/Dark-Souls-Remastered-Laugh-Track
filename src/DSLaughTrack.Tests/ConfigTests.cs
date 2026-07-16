using System.Text.Json;
using DSLaughTrack.Config;
using Xunit;

namespace DSLaughTrack.Tests;

public class ConfigTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var cfg = AppConfig.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.Equal(0.8, cfg.MasterVolume);
        Assert.Equal(30, cfg.PollHz);
        Assert.True(cfg.For("death").Enabled);
        Assert.Equal(5.0, cfg.For("death").CooldownSeconds);
    }

    [Fact]
    public void Load_ParsesTriggersCaseInsensitively()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            { "masterVolume": 0.5,
              "triggers": { "death": { "enabled": false, "volume": 0.9, "cooldownSeconds": 10, "sounds": ["a.wav"] } } }
            """);
        var cfg = AppConfig.Load(path);
        Assert.Equal(0.5, cfg.MasterVolume);
        Assert.False(cfg.For("death").Enabled);
        Assert.False(cfg.For("DEATH").Enabled);
        Assert.False(cfg.For("Death").Enabled);
        Assert.Equal(10, cfg.For("death").CooldownSeconds);
        Assert.Equal(10, cfg.For("DEATH").CooldownSeconds);
        Assert.Single(cfg.For("death").Sounds);
        Assert.True(cfg.For("unknownKey").Enabled); // default for unconfigured trigger
    }

    [Fact]
    public void Load_ParsesInterruptFlag_DefaultsFalse()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            { "triggers": { "death": { "interrupt": true }, "tookDamage": { } } }
            """);
        var cfg = AppConfig.Load(path);
        Assert.True(cfg.For("death").Interrupt);
        Assert.False(cfg.For("tookDamage").Interrupt);
        Assert.False(cfg.For("unknownKey").Interrupt);
    }

    [Fact]
    public void Load_MalformedJson_Throws()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ not json");
        Assert.Throws<JsonException>(() => AppConfig.Load(path));
    }
}
