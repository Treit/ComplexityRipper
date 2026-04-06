using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ComplexityRipper.Models;

namespace ComplexityRipper.Analysis;

/// <summary>
/// Analyzes C# source files using Roslyn to extract function-level metrics.
/// </summary>
public sealed class CSharpAnalyzer
{
    /// <summary>
    /// Analyzes all .cs files under the given repo directories, running in parallel for performance.
    /// </summary>
    public AnalysisResult AnalyzeRepos(string rootPath, Action<string>? onProgress = null, Regex? includeFilter = null, Regex? excludeFilter = null, bool includeTestCode = false)
    {
        var result = new AnalysisResult
        {
            Metadata = { RootPath = rootPath, GeneratedAt = DateTimeOffset.UtcNow }
        };

        var repoDirs = ResolveRepoDirs(rootPath);

        var allFunctions = new System.Collections.Concurrent.ConcurrentBag<FunctionMetrics>();
        var repoInfos = new System.Collections.Concurrent.ConcurrentBag<RepoInfo>();

        Parallel.ForEach(repoDirs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, repoDir =>
        {
            var repoName = Path.GetRelativePath(rootPath, repoDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');

            if (!PassesFilter(repoName, includeFilter, excludeFilter))
            {
                onProgress?.Invoke($"Skipping {repoName} (filtered)");
                return;
            }

            onProgress?.Invoke($"Scanning {repoName}...");

            var adoBaseUrl = Utilities.AdoUrlHelper.GetAdoBaseUrl(repoDir);
            var defaultBranch = adoBaseUrl != null
                ? Utilities.AdoUrlHelper.GetDefaultBranch(repoDir)
                : "main";

            var objSegment = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
            var binSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
            var csFiles = Directory.EnumerateFiles(repoDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(objSegment) && !f.Contains(binSegment));

            int functionCount = 0;
            int fileCount = 0;
            var projectResolver = new ProjectResolver(repoDir);
            foreach (var file in csFiles)
            {
                var projectName = projectResolver.GetProjectName(file);

                if (!includeTestCode && IsTestProject(projectName))
                {
                    continue;
                }

                fileCount++;
                var functions = AnalyzeFile(file, repoDir, repoName);
                foreach (var func in functions)
                {
                    func.Project = projectName;
                    allFunctions.Add(func);
                    Interlocked.Increment(ref functionCount);
                }
            }

            repoInfos.Add(new RepoInfo
            {
                Name = repoName,
                AdoBaseUrl = adoBaseUrl,
                DefaultBranch = defaultBranch,
                FileCount = fileCount,
                FunctionCount = functionCount,
            });
        });

        result.Repos = repoInfos.OrderBy(r => r.Name).ToList();
        result.Functions = allFunctions.ToList();

        // Build summary
        result.Summary = new AnalysisSummary
        {
            TotalRepos = result.Repos.Count,
            TotalFiles = result.Repos.Sum(r => r.FileCount),
            TotalFunctions = result.Functions.Count,
            LanguageBreakdown = result.Functions
                .GroupBy(f => f.Language)
                .ToDictionary(
                    g => g.Key,
                    g => new LanguageStats { Files = 0, Functions = g.Count() })
        };

        // Set file counts per language
        if (result.Summary.LanguageBreakdown.TryGetValue("C#", out var csStats))
        {
            csStats.Files = result.Repos.Sum(r => r.FileCount);
        }

        return result;
    }

    /// <summary>
    /// Determines whether rootPath is itself a git repo or a directory containing multiple repos.
    /// If rootPath contains a .git directory, it is treated as a single repo.
    /// Otherwise, each subdirectory that is a git repo is included. Subdirectories that are not
    /// git repos are treated as organizational folders and their children are checked for repos.
    /// </summary>
    private static List<string> ResolveRepoDirs(string rootPath)
    {
        if (Directory.Exists(Path.Combine(rootPath, ".git")))
        {
            return [rootPath];
        }

        var result = new List<string>();

        foreach (var dir in Directory.GetDirectories(rootPath))
        {
            if (Path.GetFileName(dir).StartsWith('.'))
            {
                continue;
            }

            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                result.Add(dir);
            }
            else
            {
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    if (!Path.GetFileName(subDir).StartsWith('.') &&
                        Directory.Exists(Path.Combine(subDir, ".git")))
                    {
                        result.Add(subDir);
                    }
                }
            }
        }

        return result;
    }

    internal static bool PassesFilter(string filePath, Regex? includeFilter, Regex? excludeFilter)
    {
        var normalized = filePath.Replace('\\', '/');
        if (includeFilter != null && !includeFilter.IsMatch(normalized))
        {
            return false;
        }

        if (excludeFilter != null && excludeFilter.IsMatch(normalized))
        {
            return false;
        }

        return true;
    }

    private static bool IsTestProject(string? projectName)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return false;
        }

        return projectName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)
            || projectName.EndsWith("Test", StringComparison.OrdinalIgnoreCase)
            || projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
            || projectName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Analyzes a single C# file, returning metrics for each function found.
    /// </summary>
    public List<FunctionMetrics> AnalyzeFile(string filePath, string repoRoot, string repoName)
    {
        var functions = new List<FunctionMetrics>();

        try
        {
            using var stream = File.OpenRead(filePath);
            var sourceText = SourceText.From(stream, Encoding.UTF8);
            var tree = CSharpSyntaxTree.ParseText(sourceText, path: filePath);
            var root = tree.GetRoot();

            var relativePath = Path.GetRelativePath(repoRoot, filePath);

            // Find the namespace and class context
            var walker = new FunctionWalker(relativePath, repoName, tree);
            walker.Visit(root);
            functions.AddRange(walker.Functions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse {filePath}: {ex.Message}");
        }

        return functions;
    }

    /// <summary>
    /// Walks a C# syntax tree to find all function declarations and compute their metrics.
    /// </summary>
    private class FunctionWalker : CSharpSyntaxWalker
    {
        private readonly string _relativePath;
        private readonly string _repoName;
        private readonly SyntaxTree _tree;
        public List<FunctionMetrics> Functions { get; } = new();

        public FunctionWalker(string relativePath, string repoName, SyntaxTree tree)
        {
            _relativePath = relativePath;
            _repoName = repoName;
            _tree = tree;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AddFunction(node, node.Identifier.Text, node.ParameterList.Parameters.Count);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            AddFunction(node, node.Identifier.Text + " (ctor)", node.ParameterList.Parameters.Count);
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            AddFunction(node, node.Identifier.Text + " (local)", node.ParameterList.Parameters.Count);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            // Only include property accessors with explicit bodies (not auto-properties)
            if (node.Body != null || node.ExpressionBody != null)
            {
                var property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
                if (property != null)
                {
                    var lineSpan = _tree.GetLineSpan(node.Span);
                    int lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;

                    // Only include accessors with meaningful bodies (more than a few lines)
                    if (lineCount > 5)
                    {
                        string accessorKind = node.Keyword.Text; // "get" or "set"
                        AddFunction(node, $"{property.Identifier.Text}.{accessorKind}", 0);
                    }
                }
            }
            base.VisitAccessorDeclaration(node);
        }

        private void AddFunction(SyntaxNode node, string functionName, int parameterCount)
        {
            var lineSpan = _tree.GetLineSpan(node.Span);
            int startLine = lineSpan.StartLinePosition.Line + 1; // 1-based
            int endLine = lineSpan.EndLinePosition.Line + 1;
            int lineCount = endLine - startLine + 1;

            string? className = null;
            // Materialize once to avoid re-enumerating the ancestor tree for Reverse/Select.
            var typeAncestors = node.Ancestors().OfType<TypeDeclarationSyntax>().ToList();
            if (typeAncestors.Count > 0)
            {
                typeAncestors.Reverse();
                className = string.Join(".", typeAncestors.Select(t => t.Identifier.Text));
            }

            // Find containing namespace
            string? ns = null;
            var nsDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsDecl != null)
            {
                ns = nsDecl.Name.ToString();
            }

            var complexity = ComplexityCalculator.Calculate(node);
            var nestingDepth = ComplexityCalculator.CalculateMaxNestingDepth(node);

            Functions.Add(new FunctionMetrics
            {
                File = _relativePath,
                Repo = _repoName,
                Language = "C#",
                Namespace = ns,
                ClassName = className,
                Function = functionName,
                StartLine = startLine,
                EndLine = endLine,
                LineCount = lineCount,
                CyclomaticComplexity = complexity,
                ParameterCount = parameterCount,
                MaxNestingDepth = nestingDepth,
            });
        }
    }
}

/// <summary>
/// Resolves the nearest .NET project file for a given source file by walking up
/// the directory tree. Caches results per directory to avoid redundant I/O.
/// When no ancestor project is found, falls back to the project whose directory
/// shares the deepest common ancestor with the source file.
/// </summary>
internal class ProjectResolver
{
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];
    private readonly string _repoRoot;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private List<(string Dir, string Name)>? _allProjects;

    public ProjectResolver(string repoRoot)
    {
        _repoRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public string? GetProjectName(string sourceFilePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));
        if (dir == null)
        {
            return null;
        }

        var result = FindByAncestorWalk(dir, sourceFilePath);
        if (result != null)
        {
            return result;
        }

        return FindByNearestProject(dir);
    }

    private string? FindByAncestorWalk(string dir, string sourceFilePath)
    {
        var current = dir;
        while (current != null && current.StartsWith(_repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (_cache.TryGetValue(current, out var cached))
            {
                return cached;
            }

            foreach (var pattern in ProjectExtensions)
            {
                foreach (var file in Directory.EnumerateFiles(current, "*" + pattern))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    CacheUpTo(dir, current, name);
                    return name;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private string? FindByNearestProject(string sourceDir)
    {
        _allProjects ??= ScanAllProjects();

        if (_allProjects.Count == 0)
        {
            return null;
        }

        string? bestName = null;
        int bestCommonLength = -1;

        foreach (var (projDir, projName) in _allProjects)
        {
            int commonLen = GetCommonPrefixLength(sourceDir, projDir);
            if (commonLen > bestCommonLength)
            {
                bestCommonLength = commonLen;
                bestName = projName;
            }
        }

        if (bestName != null)
        {
            _cache[sourceDir] = bestName;
        }

        return bestName;
    }

    private List<(string Dir, string Name)> ScanAllProjects()
    {
        var projects = new List<(string Dir, string Name)>();
        foreach (var ext in ProjectExtensions)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(_repoRoot, "*" + ext, SearchOption.AllDirectories))
                {
                    var dir = Path.GetDirectoryName(file);
                    if (dir != null)
                    {
                        projects.Add((dir, Path.GetFileNameWithoutExtension(file)));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        return projects;
    }

    internal static int GetCommonPrefixLength(string pathA, string pathB)
    {
        int limit = Math.Min(pathA.Length, pathB.Length);
        int lastSep = -1;
        int matched = 0;

        for (int i = 0; i < limit; i++)
        {
            char a = pathA[i];
            char b = pathB[i];
            if (char.ToUpperInvariant(a) != char.ToUpperInvariant(b))
            {
                break;
            }

            matched = i + 1;

            if (a == Path.DirectorySeparatorChar || a == Path.AltDirectorySeparatorChar)
            {
                lastSep = i;
            }
        }

        if (matched == limit)
        {
            if (pathA.Length == pathB.Length)
            {
                return limit;
            }

            var longer = pathA.Length > pathB.Length ? pathA : pathB;
            if (longer[limit] == Path.DirectorySeparatorChar || longer[limit] == Path.AltDirectorySeparatorChar)
            {
                return limit;
            }
        }

        return lastSep;
    }

    private void CacheUpTo(string fromDir, string toDir, string? projectName)
    {
        var dir = fromDir;
        while (dir != null && dir.Length >= toDir.Length)
        {
            _cache[dir] = projectName;
            if (string.Equals(dir, toDir, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            dir = Path.GetDirectoryName(dir);
        }
    }
}
