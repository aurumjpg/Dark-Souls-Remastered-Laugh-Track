using DSLaughTrack;
using DSLaughTrack.Triggers;
using Xunit;

namespace DSLaughTrack.Tests;

public static class States
{
    public static GameState S(int? hp = 100, int? maxHp = 100, int? stam = 50, int? dex = 10,
        int? estus = 5, int? anim = 0, bool inGame = true, double t = 0) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(t), true, inGame, hp, maxHp, stam, dex, estus, anim);
}

public class BasicTriggerTests
{
    [Fact]
    public void OutOfStamina_FiresOnCrossingZero_Once()
    {
        var t = new OutOfStaminaTrigger();
        Assert.True(t.ShouldFire(States.S(stam: 12), States.S(stam: 0)));
        Assert.False(t.ShouldFire(States.S(stam: 0), States.S(stam: 0)));   // stays empty: no refire
        Assert.False(t.ShouldFire(States.S(stam: 0), States.S(stam: 30)));  // recovering
        Assert.False(t.ShouldFire(States.S(stam: null), States.S(stam: 0))); // unavailable
    }

    [Fact]
    public void TookDamage_FiresOnHpDrop_NotOnDeath()
    {
        var t = new TookDamageTrigger();
        Assert.True(t.ShouldFire(States.S(hp: 100), States.S(hp: 60)));
        Assert.False(t.ShouldFire(States.S(hp: 60), States.S(hp: 60)));
        Assert.False(t.ShouldFire(States.S(hp: 60), States.S(hp: 100)));  // healing
        Assert.False(t.ShouldFire(States.S(hp: 60), States.S(hp: 0)));    // death, not damage
    }

    [Fact]
    public void Death_FiresWhenHpReachesZero()
    {
        var t = new DeathTrigger();
        Assert.True(t.ShouldFire(States.S(hp: 60), States.S(hp: 0)));
        Assert.False(t.ShouldFire(States.S(hp: 0), States.S(hp: 0)));
        Assert.False(t.ShouldFire(States.S(hp: 0), States.S(hp: 100))); // respawn
    }

    [Fact]
    public void DexIncrease_FiresOnlyOnIncrease()
    {
        var t = new DexIncreaseTrigger();
        Assert.True(t.ShouldFire(States.S(dex: 10), States.S(dex: 11)));
        Assert.True(t.ShouldFire(States.S(dex: 10), States.S(dex: 15))); // multi-point level-up
        Assert.False(t.ShouldFire(States.S(dex: 11), States.S(dex: 11)));
        Assert.False(t.ShouldFire(States.S(dex: null), States.S(dex: 11)));
    }
}
