using InventoryStore.Application.Services;

namespace InventoryStore.Tests;

public class ReleaseVersionTests
{
    [Fact]
    public void ALaterStampInTheNewShapeIsNewer()
    {
        Assert.True(ReleaseVersion.IsNewer("26.1026.0900", "26.1025.1536"));
    }

    [Fact]
    public void AnEarlierStampInTheNewShapeIsNotNewer()
    {
        Assert.False(ReleaseVersion.IsNewer("26.1025.1536", "26.1026.0900"));
    }

    [Fact]
    public void TheSameStampIsNotNewer()
    {
        Assert.False(ReleaseVersion.IsNewer("26.1026.0900", "26.1026.0900"));
    }

    [Fact]
    public void ALaterStampInTheOldShapeIsNewer()
    {
        Assert.True(ReleaseVersion.IsNewer("20261025.1536", "20260921.1836"));
    }

    [Fact]
    public void AnyReleaseInTheNewShapeIsNewerThanAnyInTheOldShape()
    {
        Assert.True(ReleaseVersion.IsNewer("26.0101.0000", "20261231.2359"));
    }

    [Fact]
    public void AnyReleaseInTheOldShapeIsOlderThanAnyInTheNewShape()
    {
        Assert.False(ReleaseVersion.IsNewer("20261231.2359", "26.0101.0000"));
    }

    [Theory]
    [InlineData("v26.1026.0900", "26.1025.1536")]
    [InlineData("26.1026.0900", "v26.1025.1536")]
    [InlineData("26.1026.0900+abc1234", "26.1025.1536")]
    public void ALeadingVAndABuildSuffixAreIgnored(string latest, string current)
    {
        Assert.True(ReleaseVersion.IsNewer(latest, current));
    }

    [Fact]
    public void APrereleaseSuffixDoesNotChangeWhichStampIsNewer()
    {
        Assert.True(ReleaseVersion.IsNewer("26.1026.0900-rc", "26.1025.1536"));
        Assert.False(ReleaseVersion.IsNewer("26.1025.1536-rc", "26.1026.0900"));
    }

    [Fact]
    public void APrereleaseAndItsPromotedReleaseAreTheSameVersion()
    {
        Assert.False(ReleaseVersion.IsNewer("26.1026.0900", "26.1026.0900-rc"));
        Assert.False(ReleaseVersion.IsNewer("26.1026.0900-rc", "26.1026.0900"));
    }

    [Fact]
    public void StampsAreComparedAsNumbersNotAsText()
    {
        Assert.True(ReleaseVersion.IsNewer("26.1101.0800", "26.1031.2359"));
        Assert.True(ReleaseVersion.IsNewer("27.0101.0000", "26.1231.2359"));
    }

    [Theory]
    [InlineData("26.1026.0900", "dev")]
    [InlineData("26.1026.0900", "")]
    [InlineData("26.1026.0900", "1.0.0")]
    [InlineData("not a version", "26.1025.1536")]
    [InlineData("", "26.1025.1536")]
    public void ADevelopmentBuildOrAnUnreadableVersionNeverCountsAsAnUpdate(string latest, string current)
    {
        Assert.False(ReleaseVersion.IsNewer(latest, current));
    }
}
