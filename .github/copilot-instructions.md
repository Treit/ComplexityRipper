# Copilot Instructions

## Build, Test, and Lint

```powershell
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~AdoUrlHelperTests.ParsesGitHubSshRemote"
```

Warnings are treated as errors (`TreatWarningsAsErrors` in Directory.Build.props). Code style is enforced at build time via `EnforceCodeStyleInBuild`.

## Architecture

The tool is a .NET 9 CLI application packaged as a dotnet global tool (`dotnet tool install -g ComplexityRipper`). It uses Roslyn (`Microsoft.CodeAnalysis.CSharp`) to parse C# syntax trees and compute per-function metrics: line count, cyclomatic complexity, parameter count, and max nesting depth.

The data flow is: `Program.cs` (CLI via `System.CommandLine`) calls `CSharpAnalyzer`, which walks syntax trees using a nested `FunctionWalker` and delegates complexity/nesting math to `ComplexityCalculator`. Results are collected into `AnalysisResult`/`FunctionMetrics` model objects, serialized to JSON, and optionally fed to `HtmlReportGenerator` which produces a self-contained HTML file (inline CSS/JS, no templates or external assets). `AdoUrlHelper` builds clickable source links for Azure DevOps and GitHub remotes.

The CLI has three commands: `run` (analyze + report in one step), `analyze` (JSON only), and `report` (HTML from existing JSON).

## Conventions

- .NET 9, nullable enabled, implicit usings enabled, file-scoped namespaces.
- NuGet Central Package Management via `Directory.Packages.props`. Never put version numbers in `.csproj` files.
- Private fields use `_camelCase` prefix.
- Prefer `var` when the type is apparent from the right-hand side.
- DTOs use `[JsonPropertyName(...)]` for explicit JSON property names.
- Core analysis and report generation are synchronous. Async is used only at I/O boundaries (file reads/writes in `Program.cs`).
- Parallel analysis uses `Parallel.ForEach` with `ConcurrentBag` and `Interlocked` for thread-safe accumulation.
- Error handling favors catch-and-return-null/empty over throwing exceptions. CLI validation uses early-return with `Console.Error` output.
- Helper types (`FunctionWalker`, `ProjectResolver`, `RepoStatsRow`) are nested inside their parent class rather than exposed as top-level types.
- Tests use xUnit with `[Fact]` attributes. No mocking frameworks. Tests parse inline C# snippets with Roslyn or use temporary files/directories with cleanup in `finally` blocks.
- HTML output is always encoded via `HttpUtility.HtmlEncode`.
