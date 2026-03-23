using System.CommandLine;
using System.Text.Json;
using ComplexityRipper.Analysis;
using ComplexityRipper.Models;
using ComplexityRipper.Report;

// Configure JSON serialization options
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

// Root command
var rootCommand = new RootCommand("Code Complexity Analyzer — Roslyn + Lizard hybrid analysis tool");

// analyze command — scans repos and outputs JSON
var analyzeCommand = new Command("analyze", "Scan repositories and generate analysis JSON");
var rootPathOption = new Option<string>("--root", "Root directory containing repos to analyze") { IsRequired = true };
var outputOption = new Option<string>("--output", () => "stats.json", "Output JSON file path");
var csharpOnlyOption = new Option<bool>("--csharp-only", () => false, "Only analyze C# files (skip Lizard)");
analyzeCommand.AddOption(rootPathOption);
analyzeCommand.AddOption(outputOption);
analyzeCommand.AddOption(csharpOnlyOption);

analyzeCommand.SetHandler(async (string root, string output, bool csharpOnly) =>
{
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"Error: Directory not found: {root}");
        return;
    }

    Console.WriteLine($"Analyzing repos in: {root}");
    Console.WriteLine();

    // Phase 1: Roslyn C# analysis
    var csharpAnalyzer = new CSharpAnalyzer();
    var result = csharpAnalyzer.AnalyzeRepos(root, msg => Console.WriteLine($"  [C#] {msg}"));

    Console.WriteLine($"  [C#] Found {result.Functions.Count:N0} functions in {result.Summary.TotalFiles:N0} files across {result.Summary.TotalRepos} repos");

    // Phase 2: Lizard for other languages
    if (!csharpOnly)
    {
        var lizardRunner = new LizardRunner();
        var otherFunctions = lizardRunner.Analyze(root, result.Repos, msg => Console.WriteLine($"  [Lizard] {msg}"));

        result.Functions.AddRange(otherFunctions);

        // Update summary with Lizard results
        var langGroups = otherFunctions.GroupBy(f => f.Language);
        foreach (var group in langGroups)
        {
            if (!result.Summary.LanguageBreakdown.ContainsKey(group.Key))
            {
                result.Summary.LanguageBreakdown[group.Key] = new LanguageStats();
            }

            result.Summary.LanguageBreakdown[group.Key].Functions += group.Count();
        }

        result.Summary.TotalFunctions = result.Functions.Count;
        Console.WriteLine($"  [Lizard] Added {otherFunctions.Count:N0} functions from other languages");
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {result.Functions.Count:N0} functions analyzed");

    // Write JSON output
    var json = JsonSerializer.Serialize(result, jsonOptions);
    await File.WriteAllTextAsync(output, json);
    Console.WriteLine($"Stats written to: {output}");

}, rootPathOption, outputOption, csharpOnlyOption);

// report command — reads JSON and generates HTML
var reportCommand = new Command("report", "Generate HTML report from analysis JSON");
var inputOption = new Option<string>("--input", () => "stats.json", "Input JSON file path");
var reportOutputOption = new Option<string>("--output", () => "code-complexity-report.html", "Output HTML file path");
var thresholdLinesOption = new Option<int>("--threshold-lines", () => 200, "Line count threshold for flagging functions");
var thresholdComplexityOption = new Option<int>("--threshold-complexity", () => 15, "Cyclomatic complexity threshold for flagging functions");
var themeOption = new Option<string>("--theme", () => "dark", "Report theme: dark, light, high-contrast, ink");
reportCommand.AddOption(inputOption);
reportCommand.AddOption(reportOutputOption);
reportCommand.AddOption(thresholdLinesOption);
reportCommand.AddOption(thresholdComplexityOption);
reportCommand.AddOption(themeOption);

reportCommand.SetHandler(async (string input, string output, int thresholdLines, int thresholdComplexity, string theme) =>
{
    if (!File.Exists(input))
    {
        Console.Error.WriteLine($"Error: File not found: {input}");
        return;
    }

    Console.WriteLine($"Generating report from: {input}");
    Console.WriteLine($"Thresholds: {thresholdLines} lines, {thresholdComplexity} complexity");

    var json = await File.ReadAllTextAsync(input);
    var data = JsonSerializer.Deserialize<AnalysisResult>(json, jsonOptions);
    if (data == null)
    {
        Console.Error.WriteLine("Error: Failed to deserialize analysis data.");
        return;
    }

    var reportGenerator = new HtmlReportGenerator();
    reportGenerator.Generate(data, output, thresholdLines, thresholdComplexity, theme);
    Console.WriteLine($"Report written to: {output}");

}, inputOption, reportOutputOption, thresholdLinesOption, thresholdComplexityOption, themeOption);

// run command — analyze + report in one step
var runCommand = new Command("run", "Analyze repos and generate report in one step");
var runRootOption = new Option<string>("--root", "Root directory containing repos to analyze") { IsRequired = true };
var runOutputOption = new Option<string>("--output", () => "code-complexity-report.html", "Output HTML report file path");
var runStatsOption = new Option<string>("--stats", () => "stats.json", "Intermediate stats JSON file path");
var runThresholdLinesOption = new Option<int>("--threshold-lines", () => 200, "Line count threshold for flagging functions");
var runThresholdComplexityOption = new Option<int>("--threshold-complexity", () => 15, "Cyclomatic complexity threshold for flagging functions");
var runCsharpOnlyOption = new Option<bool>("--csharp-only", () => false, "Only analyze C# files (skip Lizard)");
var runThemeOption = new Option<string>("--theme", () => "dark", "Report theme: dark, light, high-contrast, ink");
runCommand.AddOption(runRootOption);
runCommand.AddOption(runOutputOption);
runCommand.AddOption(runStatsOption);
runCommand.AddOption(runThresholdLinesOption);
runCommand.AddOption(runThresholdComplexityOption);
runCommand.AddOption(runCsharpOnlyOption);
runCommand.AddOption(runThemeOption);

runCommand.SetHandler(async (string root, string output, string statsPath, int thresholdLines, int thresholdComplexity, bool csharpOnly, string theme) =>
{
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"Error: Directory not found: {root}");
        return;
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();
    Console.WriteLine($"Analyzing repos in: {root}");
    Console.WriteLine();

    // Phase 1: Roslyn C# analysis
    var csharpAnalyzer = new CSharpAnalyzer();
    var result = csharpAnalyzer.AnalyzeRepos(root, msg => Console.WriteLine($"  [C#] {msg}"));

    Console.WriteLine($"  [C#] Found {result.Functions.Count:N0} functions in {result.Summary.TotalFiles:N0} files across {result.Summary.TotalRepos} repos");

    // Phase 2: Lizard for other languages
    if (!csharpOnly)
    {
        var lizardRunner = new LizardRunner();
        var otherFunctions = lizardRunner.Analyze(root, result.Repos, msg => Console.WriteLine($"  [Lizard] {msg}"));

        result.Functions.AddRange(otherFunctions);

        var langGroups = otherFunctions.GroupBy(f => f.Language);
        foreach (var group in langGroups)
        {
            if (!result.Summary.LanguageBreakdown.ContainsKey(group.Key))
            {
                result.Summary.LanguageBreakdown[group.Key] = new LanguageStats();
            }

            result.Summary.LanguageBreakdown[group.Key].Functions += group.Count();
        }

        result.Summary.TotalFunctions = result.Functions.Count;
        Console.WriteLine($"  [Lizard] Added {otherFunctions.Count:N0} functions from other languages");
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {result.Functions.Count:N0} functions analyzed");

    // Save stats JSON
    var json = JsonSerializer.Serialize(result, jsonOptions);
    await File.WriteAllTextAsync(statsPath, json);
    Console.WriteLine($"Stats written to: {statsPath}");

    // Generate HTML report
    var reportGenerator = new HtmlReportGenerator();
    reportGenerator.Generate(result, output, thresholdLines, thresholdComplexity, theme);
    Console.WriteLine($"Report written to: {output}");

    sw.Stop();
    Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:F1}s");

}, runRootOption, runOutputOption, runStatsOption, runThresholdLinesOption, runThresholdComplexityOption, runCsharpOnlyOption, runThemeOption);

rootCommand.AddCommand(analyzeCommand);
rootCommand.AddCommand(reportCommand);
rootCommand.AddCommand(runCommand);

return await rootCommand.InvokeAsync(args);
