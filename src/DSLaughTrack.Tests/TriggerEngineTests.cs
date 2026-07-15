using DSLaughTrack;
using DSLaughTrack.Config;
using DSLaughTrack.Triggers;
using Xunit;

namespace DSLaughTrack.Tests;

file sealed class AlwaysFire : ITrigger
{
    public AlwaysFire(string key) => Key = key;
    public string Key { get; }
    public bool ShouldFire(GameState prev, GameState curr) => true;
}

public class TriggerEngineTests
{
    private static AppConfig Cfg(double global = 0, double perTrigger = 0, bool enabled = true) => new()
    {
        GlobalCooldownSeconds = global,
        Triggers = { ["a"] = new TriggerConfig { Enabled = enabled, CooldownSeconds = perTrigger },
                     ["b"] = new TriggerConfig { CooldownSeconds = perTrigger } }
    };

    [Fact]
    public void FiresAndRespectsPerTriggerCooldown()
    {
        var cfg = Cfg(global: 0, perTrigger: 5);
        var engine = new TriggerEngine(new[] { new AlwaysFire("a") }, () => cfg);
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 0), States.S(t: 0.1)));
        Assert.Empty(engine.Tick(States.S(t: 0.1), States.S(t: 3)));   // inside cooldown
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 3), States.S(t: 5.2)));
    }

    [Fact]
    public void GlobalCooldown_SuppressesOtherTriggers()
    {
        var cfg = Cfg(global: 2, perTrigger: 0);
        var engine = new TriggerEngine(new ITrigger[] { new AlwaysFire("a"), new AlwaysFire("b") }, () => cfg);
        // first tick: "a" fires, then global cooldown blocks "b" in the same tick
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 0), States.S(t: 0.1)));
        Assert.Empty(engine.Tick(States.S(t: 0.1), States.S(t: 1)));
        Assert.Equal(new[] { "a" }, engine.Tick(States.S(t: 1), States.S(t: 2.5)));
    }

    [Fact]
    public void DisabledTrigger_NeverFires()
    {
        var cfg = Cfg(enabled: false);
        var engine = new TriggerEngine(new[] { new AlwaysFire("a") }, () => cfg);
        Assert.Empty(engine.Tick(States.S(t: 0), States.S(t: 0.1)));
    }

    [Fact]
    public void NotInGame_SuppressesEverything()
    {
        var engine = new TriggerEngine(new[] { new AlwaysFire("a") }, () => Cfg());
        Assert.Empty(engine.Tick(States.S(inGame: false), States.S()));
        Assert.Empty(engine.Tick(States.S(), States.S(inGame: false)));
    }
}
