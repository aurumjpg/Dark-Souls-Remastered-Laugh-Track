namespace DSLaughTrack;

public sealed record GameState(
    DateTimeOffset Timestamp,
    bool ProcessAttached,
    bool InGame,
    int? Hp,
    int? MaxHp,
    int? Stamina,
    int? Dexterity,
    int? EstusCount,
    int? AnimationId)
{
    public static GameState Detached(DateTimeOffset ts) =>
        new(ts, false, false, null, null, null, null, null, null);
}
