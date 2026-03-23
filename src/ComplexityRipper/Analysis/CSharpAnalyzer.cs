using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ComplexityRipper.Models;

namespace ComplexityRipper.Analysis;

/// <summary>
/// Analyzes C# source files using Roslyn to extract function-level metrics.
/// </summary>
public class CSharpAnalyzer
{
    /// <summary>
    /// Analyzes all .cs files under the given repo directories, running in parallel for performance.
    /// </summary>
    public AnalysisResult AnalyzeRepos(string rootPath, Action<string>? onProgress = null)
    {
        var result = new AnalysisResult
        {
            Metadata = { RootPath = rootPath, GeneratedAt = DateTimeOffset.UtcNow }
        };

        var repoDirs = Directory.GetDirectories(rootPath)
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
            .ToList();

        var allFunctions = new System.Collections.Concurrent.ConcurrentBag<FunctionMetrics>();
        var repoInfos = new System.Collections.Concurrent.ConcurrentBag<RepoInfo>();

        Parallel.ForEach(repoDirs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, repoDir =>
        {
            var repoName = Path.GetFileName(repoDir);
            onProgress?.Invoke($"Scanning {repoName}...");

            var adoBaseUrl = Utilities.AdoUrlHelper.GetAdoBaseUrl(repoDir);

            var csFiles = Directory.EnumerateFiles(repoDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToList();

            int functionCount = 0;
            foreach (var file in csFiles)
            {
                var functions = AnalyzeFile(file, repoDir, repoName);
                foreach (var func in functions)
                {
                    allFunctions.Add(func);
                    Interlocked.Increment(ref functionCount);
                }
            }

            repoInfos.Add(new RepoInfo
            {
                Name = repoName,
                AdoBaseUrl = adoBaseUrl,
                FileCount = csFiles.Count,
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
    /// Analyzes a single C# file, returning metrics for each function found.
    /// </summary>
    public List<FunctionMetrics> AnalyzeFile(string filePath, string repoRoot, string repoName)
    {
        var functions = new List<FunctionMetrics>();

        try
        {
            var code = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
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

            // Find containing class/struct/record
            string? className = null;
            var typeDecl = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (typeDecl != null)
            {
                // Include nested type names
                var typeNames = node.Ancestors().OfType<TypeDeclarationSyntax>()
                    .Reverse()
                    .Select(t => t.Identifier.Text);
                className = string.Join(".", typeNames);
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
