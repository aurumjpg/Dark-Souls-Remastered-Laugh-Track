using DSLaughTrack;
using DSLaughTrack.Logging;
using DSLaughTrack.Triggers;
using Xunit;

namespace DSLaughTrack.Tests;

public class AnimationTriggerTests
{
    [Fact]
    public void FiresOnEnteringTargetAnimation_Once()
    {
        var t = new AnimationTrigger("runningJump", 777);
        Assert.True(t.ShouldFire(States.S(anim: 0), States.S(anim: 777)));
        Assert.False(t.ShouldFire(States.S(anim: 777), States.S(anim: 777)));
        Assert.False(t.ShouldFire(States.S(anim: 777), States.S(anim: 0)));
        Assert.False(t.ShouldFire(States.S(anim: null), States.S(anim: 777)));
    }

    [Fact]
    public void MultiTarget_FiresOnEnteringEitherIdFromOutsideSet()
    {
        var t = new AnimationTrigger("hitWall", new[] { 253150, 254150 });
        Assert.True(t.ShouldFire(States.S(anim: 0), States.S(anim: 253150)));
        Assert.True(t.ShouldFire(States.S(anim: 0), States.S(anim: 254150)));
    }

    [Fact]
    public void MultiTarget_DoesNotFireTransitioningBetweenInSetIds()
    {
        var t = new AnimationTrigger("hitWall", new[] { 253150, 254150 });
        Assert.False(t.ShouldFire(States.S(anim: 253150), States.S(anim: 254150)));
        Assert.False(t.ShouldFire(States.S(anim: 254150), States.S(anim: 253150)));
    }

    [Fact]
    public void MultiTarget_NullPrevDoesNotFire()
    {
        var t = new AnimationTrigger("hitWall", new[] { 253150, 254150 });
        Assert.False(t.ShouldFire(States.S(anim: null), States.S(anim: 253150)));
    }

    [Fact]
    public void ExtraCondition_Gates()
    {
        var t = new AnimationTrigger("emptyEstus", 555, s => s.EstusCount == 0);
        Assert.True(t.ShouldFire(States.S(anim: 0, estus: 0), States.S(anim: 555, estus: 0)));
        Assert.False(t.ShouldFire(States.S(anim: 0, estus: 3), States.S(anim: 555, estus: 3)));
    }

    [Fact]
    public void FailedParry_FiresAfterWindowWithoutSuccess()
    {
        var t = new FailedParryTrigger(900, 901, TimeSpan.FromMilliseconds(800));
        Assert.False(t.ShouldFire(States.S(anim: 0, t: 0.00), States.S(anim: 900, t: 0.03)));
        Assert.False(t.ShouldFire(States.S(anim: 900, t: 0.03), States.S(anim: 900, t: 0.50)));
        Assert.True(t.ShouldFire(States.S(anim: 900, t: 0.50), States.S(anim: 0, t: 0.90)));
        // no refire after it has fired
        Assert.False(t.ShouldFire(States.S(anim: 0, t: 0.90), States.S(anim: 0, t: 1.50)));
    }

    [Fact]
    public void FailedParry_SuppressedBySuccessAnimation()
    {
        var t = new FailedParryTrigger(900, 901, TimeSpan.FromMilliseconds(800));
        Assert.False(t.ShouldFire(States.S(anim: 0, t: 0.00), States.S(anim: 900, t: 0.03)));
        Assert.False(t.ShouldFire(States.S(anim: 900, t: 0.03), States.S(anim: 901, t: 0.40)));
        Assert.False(t.ShouldFire(States.S(anim: 901, t: 0.40), States.S(anim: 0, t: 1.50)));
    }

    [Fact]
    public void Factory_DisablesTriggersWithoutIds()
    {
        var ids = new AnimationIds(new() { ["runningJump"] = 777 });
        var triggers = TriggerFactory.Build(ids, new Log());
        var keys = triggers.Select(t => t.Key).ToHashSet();
        Assert.Contains("outOfStamina", keys);
        Assert.Contains("tookDamage", keys);
        Assert.Contains("death", keys);
        Assert.Contains("dexIncrease", keys);
        Assert.Contains("runningJump", keys);
        Assert.DoesNotContain("gotParried", keys);
        Assert.DoesNotContain("emptyEstus", keys);
        Assert.DoesNotContain("failedParry", keys);
        Assert.DoesNotContain("hitWall", keys);
    }
}
