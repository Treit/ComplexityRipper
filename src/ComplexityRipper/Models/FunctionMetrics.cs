using System.Text.Json.Serialization;

namespace ComplexityRipper.Models;

public class FunctionMetrics
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("class")]
    public string? ClassName { get; set; }

    [JsonPropertyName("function")]
    public string Function { get; set; } = string.Empty;

    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    [JsonPropertyName("lineCount")]
    public int LineCount { get; set; }

    [JsonPropertyName("cyclomaticComplexity")]
    public int CyclomaticComplexity { get; set; }

    [JsonPropertyName("parameterCount")]
    public int ParameterCount { get; set; }

    [JsonPropertyName("maxNestingDepth")]
    public int MaxNestingDepth { get; set; }
}
