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
        var remoteOutput = "origin\tgit@ssh.dev.azure.com:v3/msdata/Sentinel Graph/NEXT (fetch)\norigin\tgit@ssh.dev.azure.com:v3/msdata/Sentinel Graph/NEXT (push)\n";
        var result = AdoUrlHelper.ParseAdoBaseUrl(remoteOutput, "NEXT");

        Assert.NotNull(result);
        Assert.Contains("dev.azure.com", result);
        Assert.Contains("NEXT", result);
    }

    [Fact]
    public void ParseAdoBaseUrl_NonAdoUrl_ReturnsNull()
    {
        var remoteOutput = "origin\thttps://github.com/Treit/SomeRepo.git (fetch)\n";
        var result = AdoUrlHelper.ParseAdoBaseUrl(remoteOutput, "SomeRepo");

        Assert.Null(result);
    }

    [Fact]
    public void ParseAdoBaseUrl_EmptyOutput_ReturnsNull()
    {
        var result = AdoUrlHelper.ParseAdoBaseUrl("", "SomeRepo");
        Assert.Null(result);
    }

    [Fact]
    public void BuildFileUrl_ConstructsCorrectUrl()
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
    public void BuildFileUrl_EncodesPath()
    {
        var baseUrl = "https://dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src/My Folder/File.cs", 1, 10);

        // Path should be URL-encoded
        Assert.DoesNotContain("My Folder", result);
        Assert.Contains("My%20Folder", result);
    }

    [Fact]
    public void BuildFileUrl_NormalizesBackslashes()
    {
        var baseUrl = "https://dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT";
        var result = AdoUrlHelper.BuildFileUrl(baseUrl, "src\\Foo\\Bar.cs", 1, 10);

        // Backslashes should be converted to forward slashes in the path
        Assert.DoesNotContain("\\", result.Split("path=")[1]);
    }
}
