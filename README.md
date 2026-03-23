# ComplexityRipper

[![CI](https://github.com/Treit/ComplexityRipper/actions/workflows/ci.yml/badge.svg)](https://github.com/Treit/ComplexityRipper/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/ComplexityRipper.svg)](https://www.nuget.org/packages/ComplexityRipper/)

A Roslyn-based code complexity analyzer that scans C# repositories for long functions, high cyclomatic complexity, and deep nesting. Also supports Python, TypeScript, Go, and other languages via [Lizard](https://github.com/terryyin/lizard) integration. Generates self-contained HTML reports with hyperlinks to source files.

## Installation

```shell
dotnet tool install -g ComplexityRipper
```

## Usage

### Analyze and generate a report in one step

```shell
complexityripper run --root C:\src\my-repos --output report.html
```

### Analyze only (JSON output)

```shell
complexityripper analyze --root C:\src\my-repos --output stats.json
```

### Generate report from existing JSON

```shell
complexityripper report --input stats.json --output report.html
```

### Custom thresholds

```shell
complexityripper run --root C:\src\my-repos --threshold-lines 300 --threshold-complexity 20
```

### C# only (skip Lizard)

```shell
complexityripper run --root C:\src\my-repos --csharp-only
```

## Commands

| Command | Description |
|---------|-------------|
| `run` | Analyze repos and generate HTML report in one step |
| `analyze` | Scan repositories and output analysis JSON |
| `report` | Generate HTML report from analysis JSON |

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `--root` | *(required)* | Root directory containing repos to analyze |
| `--output` | `code-complexity-report.html` | Output file path |
| `--stats` | `stats.json` | Intermediate stats JSON file path (for `run`) |
| `--threshold-lines` | `200` | Line count threshold for flagging functions |
| `--threshold-complexity` | `15` | Cyclomatic complexity threshold |
| `--csharp-only` | `false` | Skip non-C# analysis (Lizard) |

## How It Works

### C# Analysis (Roslyn)

For C# files, ComplexityRipper uses the Roslyn compiler APIs (`Microsoft.CodeAnalysis.CSharp`) for accurate syntax tree parsing. It finds all methods, constructors, property accessors, and local functions, then computes:

- **Line count** — total lines of the function body
- **Cyclomatic complexity** — decision points: `if`, `else if`, `case`, `for`, `foreach`, `while`, `do`, `catch`, `&&`, `||`, `??`, `?.`, ternary `?:`
- **Parameter count** — number of parameters
- **Max nesting depth** — deepest nesting of control flow blocks

### Other Languages (Lizard)

For Python, TypeScript, JavaScript, Go, and other languages, ComplexityRipper invokes [Lizard](https://github.com/terryyin/lizard) (must be installed separately) as a single process. Rust files are excluded because Lizard cannot reliably detect Rust function boundaries.

### HTML Report

The generated report is a self-contained HTML file (inline CSS + JS, no external dependencies) that includes:

- **Summary cards** — total repos, files, functions, flagged counts
- **Distribution charts** — function length and complexity histograms
- **Combined risk table** — functions exceeding both thresholds (highest refactoring priority)
- **Long functions table** — sorted by line count
- **High complexity table** — sorted by cyclomatic complexity
- **Per-repo breakdown** — aggregated stats per repository

All tables are sortable and filterable. Function entries include hyperlinks to source files in Azure DevOps (parsed from git remotes).

## Metrics

### Cyclomatic Complexity

Each of the following adds 1 to the complexity score (starting from a base of 1):

| Construct | Example |
|-----------|---------|
| `if` | `if (x > 0)` |
| `else if` | `else if (x < 0)` |
| `case` | `case 1:` |
| Switch expression arm | `1 => "one"` |
| `for` | `for (int i = 0; ...)` |
| `foreach` | `foreach (var x in items)` |
| `while` | `while (condition)` |
| `do` | `do { ... } while (...)` |
| `catch` | `catch (Exception)` |
| `&&` | `a && b` |
| `\|\|` | `a \|\| b` |
| `??` | `x ?? defaultValue` |
| `?.` | `obj?.Property` |
| `?:` | `x ? a : b` |

## License

[MIT](LICENSE)
