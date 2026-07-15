using DSLaughTrack.Config;
using DSLaughTrack.Triggers;

namespace DSLaughTrack;

public sealed class TriggerEngine
{
    private readonly IReadOnlyList<ITrigger> _triggers;
    private readonly Func<AppConfig> _config;
    private readonly Dictionary<string, DateTimeOffset> _lastFire = new();
    private DateTimeOffset _lastGlobalFire = DateTimeOffset.MinValue;

    public TriggerEngine(IReadOnlyList<ITrigger> triggers, Func<AppConfig> config)
    {
        _triggers = triggers;
        _config = config;
    }

    public IReadOnlyList<string> Tick(GameState prev, GameState curr)
    {
        var fired = new List<string>();
        if (!prev.InGame || !curr.InGame) return fired;
        var cfg = _config();
        foreach (var trigger in _triggers)
        {
            var tc = cfg.For(trigger.Key);
            if (!tc.Enabled) continue;
            if (!trigger.ShouldFire(prev, curr)) continue;
            var now = curr.Timestamp;
            if (now - _lastGlobalFire < TimeSpan.FromSeconds(cfg.GlobalCooldownSeconds)) continue;
            if (_lastFire.TryGetValue(trigger.Key, out var last) &&
                now - last < TimeSpan.FromSeconds(tc.CooldownSeconds)) continue;
            _lastFire[trigger.Key] = now;
            _lastGlobalFire = now;
            fired.Add(trigger.Key);
        }
        return fired;
    }
}
