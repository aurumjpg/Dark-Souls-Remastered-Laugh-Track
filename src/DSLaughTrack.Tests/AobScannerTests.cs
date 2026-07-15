using DSLaughTrack.Memory;
using Xunit;

namespace DSLaughTrack.Tests;

public class AobScannerTests
{
    private static readonly byte[] Hay = { 0x00, 0x48, 0x8B, 0x0D, 0xAA, 0xBB, 0xCC, 0xDD, 0x0F, 0x28 };

    [Fact]
    public void ExactMatch_ReturnsIndex() =>
        Assert.Equal(1, AobScanner.Find(Hay, "48 8b 0d aa bb cc dd"));

    [Fact]
    public void Wildcards_Match() =>
        Assert.Equal(1, AobScanner.Find(Hay, "48 8b 0d ? ? ? ? 0f"));

    [Fact]
    public void NoMatch_ReturnsMinusOne() =>
        Assert.Equal(-1, AobScanner.Find(Hay, "ff ff ff"));

    [Fact]
    public void MatchAtEnd_Found() =>
        Assert.Equal(8, AobScanner.Find(Hay, "0f 28"));
}
