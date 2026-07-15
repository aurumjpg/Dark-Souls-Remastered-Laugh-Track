namespace DSLaughTrack.Triggers;

/// EXPERIMENTAL: fires when the parry animation played and no success indicator
/// was observed within the window. If parrySuccessAnimId is unknown (null), every
/// parry attempt fires — documented limitation, ships disabled by default.
public sealed class FailedParryTrigger : ITrigger
{
    public string Key => "failedParry";
    private readonly int _parryAnim;
    private readonly int? _successAnim;
    private readonly TimeSpan _window;
    private DateTimeOffset? _pendingSince;

    public FailedParryTrigger(int parryAnimId, int? parrySuccessAnimId, TimeSpan window)
    {
        _parryAnim = parryAnimId;
        _successAnim = parrySuccessAnimId;
        _window = window;
    }

    public bool ShouldFire(GameState prev, GameState curr)
    {
        if (curr.AnimationId is null) { _pendingSince = null; return false; }
        if (curr.AnimationId == _parryAnim && prev.AnimationId != _parryAnim)
            _pendingSince = curr.Timestamp;
        if (_pendingSince is null) return false;
        if (_successAnim is not null && curr.AnimationId == _successAnim)
        {
            _pendingSince = null;
            return false;
        }
        if (curr.Timestamp - _pendingSince >= _window)
        {
            _pendingSince = null;
            return true;
        }
        return false;
    }
}
