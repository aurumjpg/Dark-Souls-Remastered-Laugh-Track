using DSLaughTrack.Memory;
using Xunit;

namespace DSLaughTrack.Tests;

public class DsrPointersTests
{
    [Fact]
    public void AobPatterns_AreParseable()
    {
        // Every registered pattern must be scannable (valid hex / wildcards).
        foreach (var pattern in DsrPointers.AllPatterns)
            Assert.Equal(-1, AobScanner.Find(new byte[8], pattern)); // no throw, no match on empty
    }
}
