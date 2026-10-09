using Jellyfin.Plugin.CjkNameFixer.Core;

namespace Jellyfin.Plugin.CjkNameFixer.Tests;

public sealed class CjkDetectorTests
{
    [Theory]
    [InlineData("木村拓哉")]
    [InlineData("김수현")]
    [InlineData("周星驰")]
    [InlineData("カタカナ")]
    [InlineData("𠀀")]
    public void ContainsCjk_ReturnsTrueForCjkScripts(string value)
    {
        Assert.True(CjkDetector.ContainsCjk(value));
    }

    [Theory]
    [InlineData("Kimura Takuya")]
    [InlineData("Seong-hyeon Kim")]
    [InlineData("Stephen Chow")]
    [InlineData("")]
    public void ContainsCjk_ReturnsFalseForLatinOrEmpty(string value)
    {
        Assert.False(CjkDetector.ContainsCjk(value));
    }

    [Fact]
    public void ContainsCjk_ReturnsFalseForNull()
    {
        Assert.False(CjkDetector.ContainsCjk(null));
    }
}
