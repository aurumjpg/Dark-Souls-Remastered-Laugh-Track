namespace DSLaughTrack.Triggers;

public sealed class AnimationTrigger : ITrigger
{
    public string Key { get; }
    private readonly int _target;
    private readonly Func<GameState, bool>? _extra;

    public AnimationTrigger(string key, int targetAnimId, Func<GameState, bool>? extraCondition = null)
    {
        Key = key;
        _target = targetAnimId;
        _extra = extraCondition;
    }

    public bool ShouldFire(GameState prev, GameState curr) =>
        curr.AnimationId == _target && prev.AnimationId != _target && prev.AnimationId is not null
        && (_extra?.Invoke(curr) ?? true);
}
