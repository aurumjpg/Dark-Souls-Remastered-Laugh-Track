using DSLaughTrack;
using DSLaughTrack.Logging;
using DSLaughTrack.Triggers;
using Xunit;

namespace DSLaughTrack.Tests;

/// Locks the shipped animation_ids.json (provenance-recorded discoveries) to the trigger
/// set the app actually builds from it. If this test breaks, either the discovery file
/// changed (update assertions to match) or TriggerFactory regressed.
public class TriggerFactoryIntegrationTests
{
    [Fact]
    public void RealAnimationIdsFile_BuildsExpectedTriggerSet()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var animPath = Path.Combine(repoRoot, "animation_ids.json");
        Assert.True(File.Exists(animPath), $"expected repo animation_ids.json at {animPath}");

        var ids = AnimationIds.Load(animPath, new Log());
        var triggers = TriggerFactory.Build(ids, new Log());
        var keys = triggers.Select(t => t.Key).ToHashSet();

        Assert.Equal(8, triggers.Count);
        Assert.Contains("outOfStamina", keys);
        Assert.Contains("tookDamage", keys);
        Assert.Contains("death", keys);
        Assert.Contains("dexIncrease", keys);
        Assert.Contains("runningJump", keys);
        Assert.Contains("emptyEstus", keys);
        Assert.Contains("hitWall", keys);
        Assert.Contains("failedParry", keys);
        Assert.DoesNotContain("gotParried", keys);
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DSLaughTrack.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"could not find repo root (DSLaughTrack.slnx) walking up from {startDir}");
    }
}
