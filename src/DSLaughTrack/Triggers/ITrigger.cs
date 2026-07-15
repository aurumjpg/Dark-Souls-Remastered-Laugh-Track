namespace DSLaughTrack.Triggers;

public interface ITrigger
{
    string Key { get; }
    /// Called only when both prev and curr are InGame. Must be side-effect free
    /// except for the trigger's own internal arming state.
    bool ShouldFire(GameState prev, GameState curr);
}
