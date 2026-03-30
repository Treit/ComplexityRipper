using System.Text.Json.Serialization;

namespace ComplexityRipper.Models;

public sealed class AnalysisResult
{
    [JsonPropertyName("metadata")]
    public AnalysisMetadata Metadata { get; set; } = new();

    [JsonPropertyName("repos")]
    public List<RepoInfo> Repos { get; set; } = new();

    [JsonPropertyName("summary")]
    public AnalysisSummary Summary { get; set; } = new();

    [JsonPropertyName("functions")]
    public List<FunctionMetrics> Functions { get; set; } = new();
}

public sealed class AnalysisMetadata
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("rootPath")]
    public string RootPath { get; set; } = string.Empty;

    [JsonPropertyName("defaultThresholds")]
    public Thresholds DefaultThresholds { get; set; } = new();
}

public sealed class Thresholds
{
    [JsonPropertyName("maxLines")]
    public int MaxLines { get; set; } = 200;

    [JsonPropertyName("maxComplexity")]
    public int MaxComplexity { get; set; } = 25;
}

public sealed class RepoInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("adoBaseUrl")]
    public string? AdoBaseUrl { get; set; }

    [JsonPropertyName("defaultBranch")]
    public string DefaultBranch { get; set; } = "main";

    [JsonPropertyName("lifecycle")]
    public string? Lifecycle { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    [JsonPropertyName("functionCount")]
    public int FunctionCount { get; set; }
}

public sealed class AnalysisSummary
{
    [JsonPropertyName("totalRepos")]
    public int TotalRepos { get; set; }

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("totalFunctions")]
    public int TotalFunctions { get; set; }

    [JsonPropertyName("languageBreakdown")]
    public Dictionary<string, LanguageStats> LanguageBreakdown { get; set; } = new();
}

public sealed class LanguageStats
{
    [JsonPropertyName("files")]
    public int Files { get; set; }

    [JsonPropertyName("functions")]
    public int Functions { get; set; }
}
