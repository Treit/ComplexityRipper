using System.Diagnostics;
using System.Globalization;
using ComplexityRipper.Models;

namespace ComplexityRipper.Analysis;

/// <summary>
/// Invokes the Lizard CLI tool to analyze non-C# source files.
/// Runs as a single process to avoid the deadlocks encountered in the previous approach.
/// </summary>
public class LizardRunner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".py", ".js", ".ts", ".tsx", ".go", ".java", ".rb", ".php", ".swift", ".scala",
        ".kt", ".lua", ".zig", ".pl", ".r",
    };

    /// <summary>
    /// Runs Lizard against the root path for non-C# languages, returning function metrics.
    /// Excludes .cs files (handled by Roslyn) and .rs files (Lizard can't parse Rust functions).
    /// </summary>
    public List<FunctionMetrics> Analyze(string rootPath, List<RepoInfo> repos, Action<string>? onProgress = null)
    {
        var functions = new List<FunctionMetrics>();

        // Check if Lizard is available
        if (!IsLizardAvailable())
        {
            Console.Error.WriteLine("Warning: Lizard not found. Skipping non-C# analysis.");
            return functions;
        }

        onProgress?.Invoke("Running Lizard for non-C# files...");

        // Build exclusion patterns
        // Run Lizard as a single process against the root to avoid deadlocks
        var args = $"--csv \"{rootPath}\" -x \"*.cs\" -x \"*.rs\" -x \"*/obj/*\" -x \"*/bin/*\" -x \"*/node_modules/*\" -x \"*/.git/*\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "lizard",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.Error.WriteLine("Warning: Failed to start Lizard process.");
                return functions;
            }

            // Read output in a streaming fashion to avoid blocking
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit(600_000); // 10 minute timeout

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.Error.WriteLine($"Lizard warnings: {error[..Math.Min(error.Length, 500)]}");
            }

            functions = ParseLizardCsv(output, rootPath, repos);
            onProgress?.Invoke($"Lizard found {functions.Count} functions in non-C# files.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Lizard analysis failed: {ex.Message}");
        }

        return functions;
    }

    /// <summary>
    /// Parses Lizard CSV output into FunctionMetrics.
    /// CSV columns: NLOC, CCN, Token, PARAM, Length, Location, File, Function, Start, End
    /// </summary>
    private List<FunctionMetrics> ParseLizardCsv(string csv, string rootPath, List<RepoInfo> repos)
    {
        var functions = new List<FunctionMetrics>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Skip header line and summary lines
            if (line.StartsWith("NLOC") || line.StartsWith("Total") || line.StartsWith("-----")
                || line.StartsWith("!!") || !line.Contains(','))
            {
                continue;
            }

            try
            {
                var parts = SplitCsvLine(line);
                if (parts.Length < 10)
                {
                    continue;
                }

                // NLOC, CCN, Token, PARAM, Length, Location, File, Function, Start, End
                if (!int.TryParse(parts[0].Trim(), out int nloc))
                {
                    continue;
                }

                if (!int.TryParse(parts[1].Trim(), out int ccn))
                {
                    continue;
                }

                if (!int.TryParse(parts[3].Trim(), out int paramCount))
                {
                    continue;
                }

                var filePath = parts[6].Trim().Trim('"');
                var functionName = parts[7].Trim().Trim('"');
                var startLinePart = parts[8].Trim().Trim('"');
                var endLinePart = parts[9].Trim().Trim('"');

                if (!int.TryParse(startLinePart, out int startLine))
                {
                    continue;
                }

                if (!int.TryParse(endLinePart, out int endLine))
                {
                    continue;
                }

                // Determine repo and relative path
                var (repoName, relativePath) = ResolveRepoPath(filePath, rootPath);
                if (repoName == null)
                {
                    continue;
                }

                // Determine language from file extension
                var language = GetLanguageFromExtension(Path.GetExtension(filePath));

                functions.Add(new FunctionMetrics
                {
                    File = relativePath,
                    Repo = repoName,
                    Language = language,
                    Function = functionName,
                    StartLine = startLine,
                    EndLine = endLine,
                    LineCount = nloc,
                    CyclomaticComplexity = ccn,
                    ParameterCount = paramCount,
                    MaxNestingDepth = 0, // Lizard doesn't report nesting depth
                });
            }
            catch
            {
                // Skip malformed lines
            }
        }

        return functions;
    }

    private static string[] SplitCsvLine(string line)
    {
        var parts = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == ',' && !inQuotes)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        parts.Add(current.ToString());
        return parts.ToArray();
    }

    private static (string? repoName, string relativePath) ResolveRepoPath(string fullPath, string rootPath)
    {
        var normalizedFull = Path.GetFullPath(fullPath);
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);

        if (!normalizedFull.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return (null, fullPath);
        }

        var relative = normalizedFull[(normalizedRoot.Length + 1)..];
        var separatorIndex = relative.IndexOf(Path.DirectorySeparatorChar);
        if (separatorIndex < 0)
        {
            return (relative, relative);
        }

        var repoName = relative[..separatorIndex];
        var filePath = relative[(separatorIndex + 1)..];
        return (repoName, filePath);
    }

    private static string GetLanguageFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".py" => "Python",
        ".js" => "JavaScript",
        ".ts" or ".tsx" => "TypeScript",
        ".go" => "Go",
        ".java" => "Java",
        ".rb" => "Ruby",
        ".php" => "PHP",
        ".swift" => "Swift",
        ".scala" => "Scala",
        ".kt" => "Kotlin",
        ".lua" => "Lua",
        ".zig" => "Zig",
        ".pl" => "Perl",
        ".r" => "R",
        _ => "Unknown",
    };

    private static bool IsLizardAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "lizard",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
