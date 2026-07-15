namespace DSLaughTrack.Triggers;

public sealed class OutOfStaminaTrigger : ITrigger
{
    public string Key => "outOfStamina";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Stamina is > 0 && curr.Stamina is <= 0;
}

public sealed class TookDamageTrigger : ITrigger
{
    public string Key => "tookDamage";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Hp is int before && curr.Hp is int after && after < before && after > 0;
}

public sealed class DeathTrigger : ITrigger
{
    public string Key => "death";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Hp is > 0 && curr.Hp is <= 0;
}

public sealed class DexIncreaseTrigger : ITrigger
{
    public string Key => "dexIncrease";
    public bool ShouldFire(GameState prev, GameState curr) =>
        prev.Dexterity is int before && curr.Dexterity is int after && after > before;
}
