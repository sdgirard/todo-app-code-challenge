namespace TodoApi.Gateway.Tests;

public class VersionEndpointTests
{
    [Fact]
    public void ParseInformationalVersion_WithVersionCommitAndBuildDate_ParsesAllThree()
    {
        var (version, commit, buildDate) = VersionEndpoint.ParseInformationalVersion("0.1.4+abc1234.2026-09-19T18:00:00Z");

        Assert.Equal("0.1.4", version);
        Assert.Equal("abc1234", commit);
        Assert.Equal("2026-09-19T18:00:00Z", buildDate);
    }

    [Fact]
    public void ParseInformationalVersion_WithNoMetadata_ReturnsUnknownCommitAndBuildDate()
    {
        var (version, commit, buildDate) = VersionEndpoint.ParseInformationalVersion("1.0.0");

        Assert.Equal("1.0.0", version);
        Assert.Equal("unknown", commit);
        Assert.Equal("unknown", buildDate);
    }

    [Fact]
    public void ParseInformationalVersion_WithCommitButNoBuildDate_ReturnsUnknownBuildDate()
    {
        var (version, commit, buildDate) = VersionEndpoint.ParseInformationalVersion("1.0.0+abc1234");

        Assert.Equal("1.0.0", version);
        Assert.Equal("abc1234", commit);
        Assert.Equal("unknown", buildDate);
    }
}
