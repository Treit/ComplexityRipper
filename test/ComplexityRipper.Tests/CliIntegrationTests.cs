using System.CommandLine;
using System.CommandLine.Parsing;
using ComplexityRipper;

namespace ComplexityRipper.Tests;

public class CliIntegrationTests : IDisposable
{
    private readonly StringWriter _outputWriter = new();
    private readonly StringWriter _errorWriter = new();
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;

    public CliIntegrationTests()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        Console.SetOut(_outputWriter);
        Console.SetError(_errorWriter);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _outputWriter.Dispose();
        _errorWriter.Dispose();
    }

    private static string CreateTempRepo(string repoName, string code)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cli_test_{Guid.NewGuid():N}");
        var repoPath = Path.Combine(root, repoName);
        Directory.CreateDirectory(Path.Combine(repoPath, ".git"));
        File.WriteAllText(Path.Combine(repoPath, "Test.cs"), code);
        return root;
    }

    private static string CreateTempRepoWithNested(string orgName, params (string Name, string Code)[] repos)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cli_test_{Guid.NewGuid():N}");
        var orgPath = Path.Combine(root, orgName);
        foreach (var (name, code) in repos)
        {
            var repoPath = Path.Combine(orgPath, name);
            Directory.CreateDirectory(Path.Combine(repoPath, ".git"));
            File.WriteAllText(Path.Combine(repoPath, "Test.cs"), code);
        }
        return root;
    }

    [Fact]
    public async Task RootCommand_NoArgs_ShowsHelp()
    {
        var rootCommand = CliConfiguration.CreateRootCommand();
        var exitCode = await rootCommand.Parse(Array.Empty<string>()).InvokeAsync();

        // System.CommandLine returns non-zero when no valid subcommand is provided
        Assert.NotEqual(0, exitCode);
        var output = _outputWriter.ToString();
        // Use culture-independent assertion (description text is always present)
        Assert.Contains("Roslyn-based", output);
    }

    [Fact]
    public async Task AnalyzeCommand_ValidRepo_GeneratesJson()
    {
        var root = CreateTempRepo("MyRepo", "class C { void M() { } }");
        var outputPath = Path.Combine(root, "stats.json");

        try
        {
            var rootCommand = CliConfiguration.CreateRootCommand();
            var exitCode = await rootCommand.Parse(new[] { "analyze", "--root", root, "--output", outputPath }).InvokeAsync();

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            var json = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("functions", json);
            Assert.Contains("MyRepo", json);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AnalyzeCommand_DotRelativePath_Works()
    {
        var repoPath = CreateTempRepo("DotRepo", "class C { void M() { } }");
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(repoPath);
            var rootCommand = CliConfiguration.CreateRootCommand();
            var exitCode = await rootCommand.Parse(new[] { "analyze", "--root", ".", "--output", "stats.json" }).InvokeAsync();

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(repoPath, "stats.json")));
            var json = await File.ReadAllTextAsync(Path.Combine(repoPath, "stats.json"));
            Assert.Contains("DotRepo", json);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(repoPath, true);
        }
    }

    [Fact]
    public async Task AnalyzeCommand_MissingRoot_ReturnsError()
    {
        var rootCommand = CliConfiguration.CreateRootCommand();
        var exitCode = await rootCommand.Parse(new[] { "analyze" }).InvokeAsync();

        Assert.NotEqual(0, exitCode);
        var error = _errorWriter.ToString();
        Assert.Contains("--root", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeCommand_NonExistentRoot_ReturnsError()
    {
        var rootCommand = CliConfiguration.CreateRootCommand();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}");
        var exitCode = await rootCommand.Parse(new[] { "analyze", "--root", nonExistentPath }).InvokeAsync();

        Assert.Equal(1, exitCode);
        var error = _errorWriter.ToString();
        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportCommand_ValidJson_GeneratesHtml()
    {
        var root = CreateTempRepo("ReportRepo", "class C { void M() { } }");
        var statsPath = Path.Combine(root, "stats.json");
        var reportPath = Path.Combine(root, "report.html");

        try
        {
            // First, generate the stats
            var rootCommand = CliConfiguration.CreateRootCommand();
            var analyzeExit = await rootCommand.Parse(new[] { "analyze", "--root", root, "--output", statsPath }).InvokeAsync();
            Assert.Equal(0, analyzeExit);

            // Then, generate the report
            var reportExit = await rootCommand.Parse(new[] { "report", "--input", statsPath, "--output", reportPath }).InvokeAsync();
            Assert.Equal(0, reportExit);
            Assert.True(File.Exists(reportPath));
            var html = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("ReportRepo", html);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ReportCommand_MissingInput_ReturnsError()
    {
        var rootCommand = CliConfiguration.CreateRootCommand();
        var exitCode = await rootCommand.Parse(new[] { "report", "--input", "C:\\NonExistent_12345.json" }).InvokeAsync();

        Assert.Equal(1, exitCode);
        var error = _errorWriter.ToString();
        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunCommand_ValidRepo_GeneratesBothJsonAndHtml()
    {
        var root = CreateTempRepo("RunRepo", "class C { void M() { } }");
        var statsPath = Path.Combine(root, "stats.json");
        var reportPath = Path.Combine(root, "report.html");

        try
        {
            var rootCommand = CliConfiguration.CreateRootCommand();
            var exitCode = await rootCommand.Parse(new[]
            {
                "run",
                "--root", root,
                "--stats", statsPath,
                "--output", reportPath
            }).InvokeAsync();

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(statsPath));
            Assert.True(File.Exists(reportPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RunCommand_MissingRoot_ReturnsError()
    {
        var rootCommand = CliConfiguration.CreateRootCommand();
        var exitCode = await rootCommand.Parse(new[] { "run" }).InvokeAsync();

        Assert.NotEqual(0, exitCode);
        var error = _errorWriter.ToString();
        Assert.Contains("--root", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeCommand_WithIncludeFilter_IncludesOnlyMatching()
    {
        var root = CreateTempRepoWithNested(
            "org",
            ("RepoA", "class A { void M() { } }"),
            ("RepoB", "class B { void N() { } }")
        );
        var outputPath = Path.Combine(root, "stats.json");

        try
        {
            var rootCommand = CliConfiguration.CreateRootCommand();
            var exitCode = await rootCommand.Parse(new[] { "analyze", "--root", root, "--output", outputPath, "--include", "RepoA" }).InvokeAsync();

            Assert.Equal(0, exitCode);
            var json = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("RepoA", json);
            Assert.DoesNotContain("RepoB", json);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AnalyzeCommand_WithExcludeFilter_ExcludesMatching()
    {
        var root = CreateTempRepoWithNested(
            "org",
            ("RepoA", "class A { void M() { } }"),
            ("RepoB", "class B { void N() { } }")
        );
        var outputPath = Path.Combine(root, "stats.json");

        try
        {
            var rootCommand = CliConfiguration.CreateRootCommand();
            var exitCode = await rootCommand.Parse(new[] { "analyze", "--root", root, "--output", outputPath, "--exclude", "RepoA" }).InvokeAsync();

            Assert.Equal(0, exitCode);
            var json = await File.ReadAllTextAsync(outputPath);
            Assert.DoesNotContain("RepoA", json);
            Assert.Contains("RepoB", json);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ReportCommand_WithRepoMetadata_AppliesLifecycleTags()
    {
        var root = CreateTempRepo("MetaRepo", "class C { void M() { } }");
        var statsPath = Path.Combine(root, "stats.json");
        var reportPath = Path.Combine(root, "report.html");
        var metadataPath = Path.Combine(root, "metadata.json");

        try
        {
            // Generate stats
            var rootCommand = CliConfiguration.CreateRootCommand();
            var analyzeExit = await rootCommand.Parse(new[] { "analyze", "--root", root, "--output", statsPath }).InvokeAsync();
            Assert.Equal(0, analyzeExit);

            // Create metadata
            await File.WriteAllTextAsync(metadataPath, "{\"MetaRepo\":{\"lifecycle\":\"active\"}}");

            // Generate report with metadata
            var reportExit = await rootCommand.Parse(new[] { "report", "--input", statsPath, "--output", reportPath, "--repo-metadata", metadataPath }).InvokeAsync();
            Assert.Equal(0, reportExit);
            var html = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("active", html);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AnalyzeCommand_WithCustomThresholds_UsesDefaults()
    {
        var root = CreateTempRepo("ThresholdRepo", @"
class C {
    void VeryLongMethod() {
        var a = 1; var b = 2; var c = 3; var d = 4; var e = 5;
        var f = 6; var g = 7; var h = 8; var i = 9; var j = 10;
        if (a > 0) { if (b > 0) { if (c > 0) { if (d > 0) { } } } }
    }
}");
        var statsPath = Path.Combine(root, "stats.json");
        var reportPath = Path.Combine(root, "report.html");

        try
        {
            var rootCommand = CliConfiguration.CreateRootCommand();
            var exitCode = await rootCommand.Parse(new[]
            {
                "run",
                "--root", root,
                "--stats", statsPath,
                "--output", reportPath,
                "--threshold-lines", "5",
                "--threshold-complexity", "2",
                "--threshold-nesting", "1"
            }).InvokeAsync();

            Assert.Equal(0, exitCode);
            var html = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("VeryLongMethod", html);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
