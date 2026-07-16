namespace DSLaughTrack.Triggers;

public sealed class AnimationTrigger : ITrigger
{
    public string Key { get; }
    private readonly IReadOnlyList<int> _targets;
    private readonly Func<GameState, bool>? _extra;

    public AnimationTrigger(string key, int targetAnimId, Func<GameState, bool>? extraCondition = null)
        : this(key, new[] { targetAnimId }, extraCondition)
    {
    }

    public AnimationTrigger(string key, IReadOnlyList<int> targetAnimIds, Func<GameState, bool>? extraCondition = null)
    {
        Key = key;
        _targets = targetAnimIds;
        _extra = extraCondition;
    }

    public bool ShouldFire(GameState prev, GameState curr) =>
        curr.AnimationId is int c && _targets.Contains(c)
        && prev.AnimationId is int p && !_targets.Contains(p)
        && (_extra?.Invoke(curr) ?? true);
}
