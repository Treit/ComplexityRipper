using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.RegularExpressions;
using ComplexityRipper.Analysis;
using ComplexityRipper.Models;
using ComplexityRipper.Report;

namespace ComplexityRipper;

public static class CliConfiguration
{
    public static RootCommand CreateRootCommand()
    {
        // Configure JSON serialization options
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        // Root command
        var rootCommand = new RootCommand("Roslyn-based code complexity analyzer for C#");

        // analyze command — scans repos and outputs JSON
        var analyzeCommand = new Command("analyze", "Scan repositories and generate analysis JSON");
        var rootPathOption = new Option<string>("--root")
        {
            Description = "Root directory containing repos to analyze",
            Required = true,
        };
        var outputOption = new Option<string>("--output")
        {
            Description = "Output JSON file path",
            DefaultValueFactory = _ => "stats.json",
        };
        var includeOption = new Option<string?>("--include")
        {
            Description = "Regex to include repos (use | for OR). Only matching repo names are analyzed",
        };
        var excludeOption = new Option<string?>("--exclude")
        {
            Description = "Regex to exclude repos (use | for OR). Matching repo names are skipped",
        };
        var includeTestsOption = new Option<bool>("--include-tests")
        {
            Description = "Include test projects (excluded by default)",
            DefaultValueFactory = _ => false,
        };
        analyzeCommand.Options.Add(rootPathOption);
        analyzeCommand.Options.Add(outputOption);
        analyzeCommand.Options.Add(includeOption);
        analyzeCommand.Options.Add(excludeOption);
        analyzeCommand.Options.Add(includeTestsOption);

        analyzeCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var root = parseResult.GetValue(rootPathOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var include = parseResult.GetValue(includeOption);
            var exclude = parseResult.GetValue(excludeOption);
            var includeTests = parseResult.GetValue(includeTestsOption);

            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine($"Error: Directory not found: {root}");
                return 1;
            }

            var includeFilter = include != null ? new Regex(include, RegexOptions.IgnoreCase | RegexOptions.Compiled) : null;
            var excludeFilter = exclude != null ? new Regex(exclude, RegexOptions.IgnoreCase | RegexOptions.Compiled) : null;

            Console.WriteLine($"Analyzing repos in: {root}");
            if (includeFilter != null)
            {
                Console.WriteLine($"  Include filter: {include}");
            }

            if (excludeFilter != null)
            {
                Console.WriteLine($"  Exclude filter: {exclude}");
            }

            Console.WriteLine();

            var analyzer = new CSharpAnalyzer();
            var result = analyzer.AnalyzeRepos(root, msg => Console.WriteLine($"  {msg}"), includeFilter, excludeFilter, includeTests);

            Console.WriteLine($"Found {result.Functions.Count:N0} functions in {result.Summary.TotalFiles:N0} files across {result.Summary.TotalRepos} repos");

            var json = JsonSerializer.Serialize(result, jsonOptions);
            await File.WriteAllTextAsync(output, json, cancellationToken);
            Console.WriteLine($"Stats written to: {output}");

            return 0;
        });

        // report command — reads JSON and generates HTML
        var reportCommand = new Command("report", "Generate HTML report from analysis JSON");
        var inputOption = new Option<string>("--input")
        {
            Description = "Input JSON file path",
            DefaultValueFactory = _ => "stats.json",
        };
        var reportOutputOption = new Option<string>("--output")
        {
            Description = "Output HTML file path",
            DefaultValueFactory = _ => "code-complexity-report.html",
        };
        var thresholdLinesOption = new Option<int>("--threshold-lines")
        {
            Description = "Line count threshold for flagging functions",
            DefaultValueFactory = _ => 200,
        };
        var thresholdComplexityOption = new Option<int>("--threshold-complexity")
        {
            Description = "Cyclomatic complexity threshold for flagging functions",
            DefaultValueFactory = _ => 25,
        };
        var thresholdNestingOption = new Option<int>("--threshold-nesting")
        {
            Description = "Max nesting depth threshold for flagging functions",
            DefaultValueFactory = _ => 4,
        };
        var themeOption = new Option<string>("--theme")
        {
            Description = "Report theme: light, dark, high-contrast, ink",
            DefaultValueFactory = _ => "light",
        };
        var reportMetadataOption = new Option<string?>("--repo-metadata")
        {
            Description = "Path to repo-metadata.json for lifecycle tags",
        };
        reportCommand.Options.Add(inputOption);
        reportCommand.Options.Add(reportOutputOption);
        reportCommand.Options.Add(thresholdLinesOption);
        reportCommand.Options.Add(thresholdComplexityOption);
        reportCommand.Options.Add(thresholdNestingOption);
        reportCommand.Options.Add(themeOption);
        reportCommand.Options.Add(reportMetadataOption);

        reportCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption)!;
            var output = parseResult.GetValue(reportOutputOption)!;
            var thresholdLines = parseResult.GetValue(thresholdLinesOption);
            var thresholdComplexity = parseResult.GetValue(thresholdComplexityOption);
            var thresholdNesting = parseResult.GetValue(thresholdNestingOption);
            var theme = parseResult.GetValue(themeOption)!;
            var repoMetadataPath = parseResult.GetValue(reportMetadataOption);

            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Error: File not found: {input}");
                return 1;
            }

            Console.WriteLine($"Generating report from: {input}");
            Console.WriteLine($"Thresholds: {thresholdLines} lines, {thresholdComplexity} complexity, {thresholdNesting} nesting");

            var json = await File.ReadAllTextAsync(input, cancellationToken);
            var data = JsonSerializer.Deserialize<AnalysisResult>(json, jsonOptions);
            if (data == null)
            {
                Console.Error.WriteLine("Error: Failed to deserialize analysis data.");
                return 1;
            }

            ApplyRepoMetadata(data, repoMetadataPath);

            var reportGenerator = new HtmlReportGenerator();
            reportGenerator.Generate(data, output, thresholdLines, thresholdComplexity, theme, thresholdNesting);
            Console.WriteLine($"Report written to: {output}");

            return 0;
        });

        // run command — analyze + report in one step
        var runCommand = new Command("run", "Analyze repos and generate report in one step");
        var runRootOption = new Option<string>("--root")
        {
            Description = "Root directory containing repos to analyze",
            Required = true,
        };
        var runOutputOption = new Option<string>("--output")
        {
            Description = "Output HTML report file path",
            DefaultValueFactory = _ => "code-complexity-report.html",
        };
        var runStatsOption = new Option<string>("--stats")
        {
            Description = "Intermediate stats JSON file path",
            DefaultValueFactory = _ => "stats.json",
        };
        var runThresholdLinesOption = new Option<int>("--threshold-lines")
        {
            Description = "Line count threshold for flagging functions",
            DefaultValueFactory = _ => 200,
        };
        var runThresholdComplexityOption = new Option<int>("--threshold-complexity")
        {
            Description = "Cyclomatic complexity threshold for flagging functions",
            DefaultValueFactory = _ => 25,
        };
        var runThresholdNestingOption = new Option<int>("--threshold-nesting")
        {
            Description = "Max nesting depth threshold for flagging functions",
            DefaultValueFactory = _ => 4,
        };
        var runThemeOption = new Option<string>("--theme")
        {
            Description = "Report theme: light, dark, high-contrast, ink",
            DefaultValueFactory = _ => "light",
        };
        var runIncludeOption = new Option<string?>("--include")
        {
            Description = "Regex to include repos (use | for OR). Only matching repo names are analyzed",
        };
        var runExcludeOption = new Option<string?>("--exclude")
        {
            Description = "Regex to exclude repos (use | for OR). Matching repo names are skipped",
        };
        var runIncludeTestsOption = new Option<bool>("--include-tests")
        {
            Description = "Include test projects (excluded by default)",
            DefaultValueFactory = _ => false,
        };
        var runMetadataOption = new Option<string?>("--repo-metadata")
        {
            Description = "Path to repo-metadata.json for lifecycle tags",
        };
        runCommand.Options.Add(runRootOption);
        runCommand.Options.Add(runOutputOption);
        runCommand.Options.Add(runStatsOption);
        runCommand.Options.Add(runThresholdLinesOption);
        runCommand.Options.Add(runThresholdComplexityOption);
        runCommand.Options.Add(runThresholdNestingOption);
        runCommand.Options.Add(runThemeOption);
        runCommand.Options.Add(runIncludeOption);
        runCommand.Options.Add(runExcludeOption);
        runCommand.Options.Add(runIncludeTestsOption);
        runCommand.Options.Add(runMetadataOption);

        runCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var root = parseResult.GetValue(runRootOption)!;
            var output = parseResult.GetValue(runOutputOption)!;
            var statsPath = parseResult.GetValue(runStatsOption)!;
            var thresholdLines = parseResult.GetValue(runThresholdLinesOption);
            var thresholdComplexity = parseResult.GetValue(runThresholdComplexityOption);
            var thresholdNesting = parseResult.GetValue(runThresholdNestingOption);
            var theme = parseResult.GetValue(runThemeOption)!;
            var include = parseResult.GetValue(runIncludeOption);
            var exclude = parseResult.GetValue(runExcludeOption);
            var includeTests = parseResult.GetValue(runIncludeTestsOption);
            var repoMetadataPath = parseResult.GetValue(runMetadataOption);

            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine($"Error: Directory not found: {root}");
                return 1;
            }

            var includeFilter = include != null ? new Regex(include, RegexOptions.IgnoreCase | RegexOptions.Compiled) : null;
            var excludeFilter = exclude != null ? new Regex(exclude, RegexOptions.IgnoreCase | RegexOptions.Compiled) : null;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine($"Analyzing repos in: {root}");
            if (includeFilter != null)
            {
                Console.WriteLine($"  Include filter: {include}");
            }

            if (excludeFilter != null)
            {
                Console.WriteLine($"  Exclude filter: {exclude}");
            }

            Console.WriteLine();

            var analyzer = new CSharpAnalyzer();
            var result = analyzer.AnalyzeRepos(root, msg => Console.WriteLine($"  {msg}"), includeFilter, excludeFilter, includeTests);

            Console.WriteLine($"Found {result.Functions.Count:N0} functions in {result.Summary.TotalFiles:N0} files across {result.Summary.TotalRepos} repos");

            var json = JsonSerializer.Serialize(result, jsonOptions);
            await File.WriteAllTextAsync(statsPath, json, cancellationToken);
            Console.WriteLine($"Stats written to: {statsPath}");

            ApplyRepoMetadata(result, repoMetadataPath);

            var reportGenerator = new HtmlReportGenerator();
            reportGenerator.Generate(result, output, thresholdLines, thresholdComplexity, theme, thresholdNesting);
            Console.WriteLine($"Report written to: {output}");

            sw.Stop();
            Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:F1}s");

            return 0;
        });

        rootCommand.Subcommands.Add(analyzeCommand);
        rootCommand.Subcommands.Add(reportCommand);
        rootCommand.Subcommands.Add(runCommand);

        return rootCommand;
    }

    private static void ApplyRepoMetadata(AnalysisResult data, string? metadataPath)
    {
        if (metadataPath == null || !File.Exists(metadataPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (metadata == null)
            {
                return;
            }

            int applied = 0;
            foreach (var repo in data.Repos)
            {
                JsonElement entry = default;
                bool found = metadata.TryGetValue(repo.Name, out entry);
                if (!found)
                {
                    var leafName = repo.Name.Contains('/') ? repo.Name[(repo.Name.LastIndexOf('/') + 1)..] : repo.Name;
                    found = metadata.TryGetValue(leafName, out entry);
                }

                if (found && entry.ValueKind == JsonValueKind.Object)
                {
                    if (entry.TryGetProperty("lifecycle", out var lc) && lc.ValueKind == JsonValueKind.String)
                    {
                        repo.Lifecycle = lc.GetString();
                        applied++;
                    }
                }
            }

            Console.WriteLine($"Applied lifecycle metadata to {applied} repos from: {metadataPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to load repo metadata: {ex.Message}");
        }
    }
}
