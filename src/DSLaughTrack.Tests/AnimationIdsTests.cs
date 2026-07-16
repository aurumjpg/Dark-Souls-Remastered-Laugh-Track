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

    [Fact]
    public void Load_ReadsValuesArray_GetAllReturnsAllAndGetReturnsFirst()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            { "ids": {
                "hitWall": { "values": [253150, 254150], "notes": "moveset-relative wall bounce" }
            } }
            """);
        var ids = AnimationIds.Load(path, new Log());
        Assert.Equal(new[] { 253150, 254150 }, ids.GetAll("hitWall"));
        Assert.Equal(253150, ids.Get("hitWall"));
    }

    [Fact]
    public void GetAll_SingleValueEntry_ReturnsOneElementList()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            { "ids": {
                "runningJump": { "value": 1234 }
            } }
            """);
        var ids = AnimationIds.Load(path, new Log());
        Assert.Equal(new[] { 1234 }, ids.GetAll("runningJump"));
    }

    [Fact]
    public void GetAll_MissingKey_ReturnsEmptyList()
    {
        var ids = new AnimationIds(new Dictionary<string, int>());
        Assert.Empty(ids.GetAll("nope"));
    }
}
