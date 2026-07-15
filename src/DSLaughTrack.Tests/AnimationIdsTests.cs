using DSLaughTrack;
using DSLaughTrack.Logging;
using Xunit;

namespace DSLaughTrack.Tests;

public class AnimationIdsTests
{
    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var ids = AnimationIds.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), new Log());
        Assert.Null(ids.Get("runningJump"));
    }

    [Fact]
    public void Load_ReadsValuesAndIgnoresEntriesWithoutValue()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            { "ids": {
                "runningJump": { "value": 1234, "capturedOn": "2026-07-15", "gameVersion": "1.03.1", "method": "monitor mode", "notes": "sprint+jump" },
                "hitWall": { "notes": "not yet discovered" }
            } }
            """);
        var ids = AnimationIds.Load(path, new Log());
        Assert.Equal(1234, ids.Get("runningJump"));
        Assert.Null(ids.Get("hitWall"));
    }
}
