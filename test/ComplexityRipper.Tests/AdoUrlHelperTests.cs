using ComplexityRipper.Utilities;

namespace ComplexityRipper.Tests;

public class AdoUrlHelperTests
{
    [Fact]
    public void ParseAdoBaseUrl_HttpsFormat_ExtractsCorrectUrl()
    {
        var remoteOutput = "origin\thttps://msdata@dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT (fetch)\norigin\thttps://msdata@dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT (push)\n";
        var result = AdoUrlHelper.ParseAdoBaseUrl(remoteOutput, "NEXT");

        Assert.NotNull(result);
        Assert.Equal("https://dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT", result);
    }

    [Fact]
    public void ParseAdoBaseUrl_SshFormat_ExtractsCorrectUrl()
    {
        var remoteOutput = "origin\tgit@ssh.dev.azure.com:v3/msdata/Sentinel Graph/NEXT (fetch)\norigin\tgit@ssh.dev.azure.com:v3/msdata/Sentinel Graph/NEXT (push)\n";        var result = AdoUrlHelper.ParseAdoBaseUrl(remoteOutput, "NEXT");

        Assert.NotNull(result);
        Assert.Contains("dev.azure.com", result);
        Assert.Contains("NEXT", result);
    }

    [Fact]
    public void ParseRemoteUrl_GitHubHttps_ExtractsCorrectUrl()
    {
        var remoteOutput = "origin\thttps://github.com/Treit/ComplexityRipper.git (fetch)\norigin\thttps://github.com/Treit/ComplexityRipper.git (push)\n";
        var result = AdoUrlHelper.ParseRemoteUrl(remoteOutput);

        Assert.NotNull(result);
        Assert.Equal("https://github.com/Treit/ComplexityRipper", result);
    }

    [Fact]
    public void ParseRemoteUrl_GitHubSsh_ExtractsCorrectUrl()
    {
        var remoteOutput = "origin\tgit@github.com:Treit/ComplexityRipper.git (fetch)\norigin\tgit@github.com:Treit/ComplexityRipper.git (push)\n";
        var result = AdoUrlHelper.ParseRemoteUrl(remoteOutput);

        Assert.NotNull(result);
        Assert.Equal("https://github.com/Treit/ComplexityRipper", result);
    }

    [Fact]
    public void ParseAdoBaseUrl_EmptyOutput_ReturnsNull()
    {
        var result = AdoUrlHelper.ParseAdoBaseUrl("", "SomeRepo");
        Assert.Null(result);
    }

    [Fact]
    public void BuildFileUrl_AdoFormat_ConstructsCorrectUrl()
    {
        var baseUrl = "https://dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src\\Foo\\Bar.cs", 42, 310);

        Assert.Contains("dev.azure.com", result);
        Assert.Contains("path=", result);
        Assert.Contains("line=42", result);
        Assert.Contains("lineEnd=310", result);
        Assert.Contains("_a=contents", result);
    }

    [Fact]
    public void BuildFileUrl_GitHubFormat_ConstructsCorrectUrl()
    {
        var baseUrl = "https://github.com/Treit/ComplexityRipper";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src/Analysis/Foo.cs", 10, 50, "main");

        Assert.Equal("https://github.com/Treit/ComplexityRipper/blob/main/src/Analysis/Foo.cs#L10-L50", result);
    }

    [Fact]
    public void BuildFileUrl_GitHubFormat_NormalizesBackslashes()
    {
        var baseUrl = "https://github.com/Treit/ComplexityRipper";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src\\Foo\\Bar.cs", 1, 10, "main");

        Assert.Equal("https://github.com/Treit/ComplexityRipper/blob/main/src/Foo/Bar.cs#L1-L10", result);
    }

    [Fact]
    public void BuildFileUrl_AdoFormat_EncodesPath()
    {
        var baseUrl = "https://dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src/My Folder/File.cs", 1, 10);

        Assert.DoesNotContain("My Folder", result);
        Assert.Contains("My%20Folder", result);
    }

    [Fact]
    public void BuildFileUrl_NoLines_AdoFormat()
    {
        var baseUrl = "https://dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src/Foo.cs");

        Assert.Contains("path=", result);
        Assert.Contains("_a=contents", result);
        Assert.DoesNotContain("line=", result);
    }

    [Fact]
    public void BuildFileUrl_NoLines_GitHubFormat()
    {
        var baseUrl = "https://github.com/Treit/ComplexityRipper";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src/Foo.cs", "main");

        Assert.Equal("https://github.com/Treit/ComplexityRipper/blob/main/src/Foo.cs", result);
    }

    [Fact]
    public void IsGitHub_ReturnsTrueForGitHub()
    {
        Assert.True(AdoUrlHelper.IsGitHub("https://github.com/Treit/ComplexityRipper"));
    }

    [Fact]
    public void IsGitHub_ReturnsFalseForAdo()
    {
        Assert.False(AdoUrlHelper.IsGitHub("https://dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT"));
    }
}
