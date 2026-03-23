using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.RegularExpressions;
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
var rootCommand = new RootCommand("Roslyn-based code complexity analyzer for C#");

// analyze command — scans repos and outputs JSON
var analyzeCommand = new Command("analyze", "Scan repositories and generate analysis JSON");
var rootPathOption = new Option<string>("--root", "Root directory containing repos to analyze") { IsRequired = true };
var outputOption = new Option<string>("--output", () => "stats.json", "Output JSON file path");
var includeOption = new Option<string?>("--include", "Regex to include repos (use | for OR). Only matching repo names are analyzed");
var excludeOption = new Option<string?>("--exclude", "Regex to exclude repos (use | for OR). Matching repo names are skipped");
analyzeCommand.AddOption(rootPathOption);
analyzeCommand.AddOption(outputOption);
analyzeCommand.AddOption(includeOption);
analyzeCommand.AddOption(excludeOption);

analyzeCommand.SetHandler(async (string root, string output, string? include, string? exclude) =>
{
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"Error: Directory not found: {root}");
        return;
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
    var result = analyzer.AnalyzeRepos(root, msg => Console.WriteLine($"  {msg}"), includeFilter, excludeFilter);

    Console.WriteLine($"Found {result.Functions.Count:N0} functions in {result.Summary.TotalFiles:N0} files across {result.Summary.TotalRepos} repos");

    var json = JsonSerializer.Serialize(result, jsonOptions);
    await File.WriteAllTextAsync(output, json);
    Console.WriteLine($"Stats written to: {output}");

}, rootPathOption, outputOption, includeOption, excludeOption);

// report command — reads JSON and generates HTML
var reportCommand = new Command("report", "Generate HTML report from analysis JSON");
var inputOption = new Option<string>("--input", () => "stats.json", "Input JSON file path");
var reportOutputOption = new Option<string>("--output", () => "code-complexity-report.html", "Output HTML file path");
var thresholdLinesOption = new Option<int>("--threshold-lines", () => 200, "Line count threshold for flagging functions");
var thresholdComplexityOption = new Option<int>("--threshold-complexity", () => 15, "Cyclomatic complexity threshold for flagging functions");
var themeOption = new Option<string>("--theme", () => "light", "Report theme: light, dark, high-contrast, ink");
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
var runThemeOption = new Option<string>("--theme", () => "light", "Report theme: light, dark, high-contrast, ink");
var runIncludeOption = new Option<string?>("--include", "Regex to include repos (use | for OR). Only matching repo names are analyzed");
var runExcludeOption = new Option<string?>("--exclude", "Regex to exclude repos (use | for OR). Matching repo names are skipped");
runCommand.AddOption(runRootOption);
runCommand.AddOption(runOutputOption);
runCommand.AddOption(runStatsOption);
runCommand.AddOption(runThresholdLinesOption);
runCommand.AddOption(runThresholdComplexityOption);
runCommand.AddOption(runThemeOption);
runCommand.AddOption(runIncludeOption);
runCommand.AddOption(runExcludeOption);

runCommand.SetHandler(async (InvocationContext ctx) =>
{
    var root = ctx.ParseResult.GetValueForOption(runRootOption)!;
    var output = ctx.ParseResult.GetValueForOption(runOutputOption)!;
    var statsPath = ctx.ParseResult.GetValueForOption(runStatsOption)!;
    var thresholdLines = ctx.ParseResult.GetValueForOption(runThresholdLinesOption);
    var thresholdComplexity = ctx.ParseResult.GetValueForOption(runThresholdComplexityOption);
    var theme = ctx.ParseResult.GetValueForOption(runThemeOption)!;
    var include = ctx.ParseResult.GetValueForOption(runIncludeOption);
    var exclude = ctx.ParseResult.GetValueForOption(runExcludeOption);
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"Error: Directory not found: {root}");
        return;
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
    var result = analyzer.AnalyzeRepos(root, msg => Console.WriteLine($"  {msg}"), includeFilter, excludeFilter);

    Console.WriteLine($"Found {result.Functions.Count:N0} functions in {result.Summary.TotalFiles:N0} files across {result.Summary.TotalRepos} repos");

    var json = JsonSerializer.Serialize(result, jsonOptions);
    await File.WriteAllTextAsync(statsPath, json);
    Console.WriteLine($"Stats written to: {statsPath}");

    var reportGenerator = new HtmlReportGenerator();
    reportGenerator.Generate(result, output, thresholdLines, thresholdComplexity, theme);
    Console.WriteLine($"Report written to: {output}");

    sw.Stop();
    Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:F1}s");

});

rootCommand.AddCommand(analyzeCommand);
rootCommand.AddCommand(reportCommand);
rootCommand.AddCommand(runCommand);

return await rootCommand.InvokeAsync(args);
