using DSLaughTrack.Audio;
using DSLaughTrack.Config;
using Xunit;

namespace DSLaughTrack.Tests;

public class LaughPlayerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dslt-" + Guid.NewGuid());
    public LaughPlayerTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(string relPath)
    {
        var full = Path.Combine(_root, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[] { 0 });
        return full;
    }

    [Fact]
    public void ExplicitList_UsedWhenPresent_MissingFilesSkipped()
    {
        var existing = Touch("big_laugh.wav");
        var cfg = new TriggerConfig { Sounds = { "big_laugh.wav", "missing.wav" } };
        var resolved = LaughPlayer.ResolveSound(_root, "death", cfg, new Random(1));
        Assert.Equal(existing, resolved);
    }

    [Fact]
    public void FolderFallback_PicksOnlyAudioFiles()
    {
        Touch(Path.Combine("death", "a.wav"));
        Touch(Path.Combine("death", "b.MP3"));
        Touch(Path.Combine("death", "notes.txt"));
        var picks = Enumerable.Range(0, 20)
            .Select(i => LaughPlayer.ResolveSound(_root, "death", new TriggerConfig(), new Random(i)))
            .Where(p => p is not null)
            .Select(p => Path.GetFileName(p)!)
            .ToHashSet();
        Assert.Subset(new HashSet<string> { "a.wav", "b.MP3" }, picks);
        Assert.NotEmpty(picks);
    }

    [Fact]
    public void NoSounds_ReturnsNull()
    {
        Assert.Null(LaughPlayer.ResolveSound(_root, "death", new TriggerConfig(), new Random(1)));
    }

    [Fact]
    public void EmptyTriggerFolder_FallsBackToDefaultFolder()
    {
        var fallback = Touch(Path.Combine("default", "laugh.wav"));
        var resolved = LaughPlayer.ResolveSound(_root, "runningJump", new TriggerConfig(), new Random(1));
        Assert.Equal(fallback, resolved);
    }

    [Fact]
    public void TriggerOwnSounds_BeatDefaultFolder()
    {
        Touch(Path.Combine("default", "laugh.wav"));
        var own = Touch(Path.Combine("death", "dirge.wav"));
        var resolved = LaughPlayer.ResolveSound(_root, "death", new TriggerConfig(), new Random(1));
        Assert.Equal(own, resolved);
    }

    [Fact]
    public void ExplicitListWithOnlyMissingFiles_FallsBackToDefaultFolder()
    {
        var fallback = Touch(Path.Combine("default", "laugh.wav"));
        var cfg = new TriggerConfig { Sounds = { "missing.wav" } };
        var resolved = LaughPlayer.ResolveSound(_root, "death", cfg, new Random(1));
        Assert.Equal(fallback, resolved);
    }
}
