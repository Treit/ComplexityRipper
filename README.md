# ComplexityRipper

[![CI](https://github.com/Treit/ComplexityRipper/actions/workflows/ci.yml/badge.svg)](https://github.com/Treit/ComplexityRipper/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/ComplexityRipper.svg)](https://www.nuget.org/packages/ComplexityRipper/)

A Roslyn-based code complexity analyzer that scans C# repositories for long functions, high cyclomatic complexity, and deep nesting. Also supports Python, TypeScript, Go, and other languages via [Lizard](https://github.com/terryyin/lizard) integration. Generates self-contained HTML reports with hyperlinks to source files.

## Installation

```shell
dotnet tool install -g ComplexityRipper
```

## Quick Start

Scan a directory of repos and generate an HTML report with default thresholds (200 lines, 15 complexity):

```powershell
complexityripper run --root C:\src\my-repos
```

This produces two files: `stats.json` (raw metrics) and `code-complexity-report.html` (the report).

## Usage Examples

### Scan a single repo

`--root` can point to a single git repo or a directory containing multiple repos. ComplexityRipper auto-detects which case it is by checking for a `.git` directory.

```powershell
complexityripper run --root C:\src\MyProject
```

### Scan multiple repos

```powershell
complexityripper run --root C:\src\all-repos
```

### Custom output paths

```powershell
complexityripper run --root C:\src\my-repos --output my-report.html --stats my-stats.json
```

### Custom thresholds

Flag functions with 300+ lines or cyclomatic complexity of 20+:

```powershell
complexityripper run --root C:\src\my-repos --threshold-lines 300 --threshold-complexity 20
```

### C# only (skip Lizard)

If you only care about C# or don't have Lizard installed:

```powershell
complexityripper run --root C:\src\my-repos --csharp-only
```

### Two-step workflow: analyze once, adjust thresholds later

Generate the JSON once (the slow part), then re-generate the HTML report with different thresholds without re-scanning:

```powershell
complexityripper analyze --root C:\src\my-repos --output stats.json

complexityripper report --input stats.json --threshold-lines 100 --output strict-report.html
complexityripper report --input stats.json --threshold-lines 500 --output lenient-report.html
```

### Analyze only (JSON output)

```powershell
complexityripper analyze --root C:\src\my-repos --output stats.json --csharp-only
```

### Generate report from existing JSON

```powershell
complexityripper report --input stats.json --output report.html --threshold-lines 200 --threshold-complexity 15
```

## Commands

| Command | Description |
|---------|-------------|
| `run` | Analyze repos and generate HTML report in one step |
| `analyze` | Scan repositories and output analysis JSON |
| `report` | Generate HTML report from analysis JSON |

## Options

### `run`

| Option | Default | Description |
|--------|---------|-------------|
| `--root` | *(required)* | Root directory: a single git repo or a directory containing multiple repos |
| `--output` | `code-complexity-report.html` | Output HTML report file path |
| `--stats` | `stats.json` | Intermediate stats JSON file path |
| `--threshold-lines` | `200` | Line count threshold for flagging functions |
| `--threshold-complexity` | `15` | Cyclomatic complexity threshold |
| `--csharp-only` | `false` | Only analyze C# files (skip Lizard) |

### `analyze`

| Option | Default | Description |
|--------|---------|-------------|
| `--root` | *(required)* | Root directory to scan |
| `--output` | `stats.json` | Output JSON file path |
| `--csharp-only` | `false` | Only analyze C# files (skip Lizard) |

### `report`

| Option | Default | Description |
|--------|---------|-------------|
| `--input` | `stats.json` | Input JSON file path (from a previous `analyze` or `run`) |
| `--output` | `code-complexity-report.html` | Output HTML file path |
| `--threshold-lines` | `200` | Line count threshold for flagging functions |
| `--threshold-complexity` | `15` | Cyclomatic complexity threshold |

## How It Works

### C# Analysis (Roslyn)

For C# files, ComplexityRipper uses the Roslyn compiler APIs (`Microsoft.CodeAnalysis.CSharp`) for accurate syntax tree parsing. It finds all methods, constructors, property accessors, and local functions, then computes:

**Line count** -- total lines of the function body.
**Cyclomatic complexity** -- decision points (see table below).
**Parameter count** -- number of parameters.
**Max nesting depth** -- deepest nesting of control flow blocks.

### Other Languages (Lizard)

For Python, TypeScript, JavaScript, Go, and other languages, ComplexityRipper invokes [Lizard](https://github.com/terryyin/lizard) (must be installed separately) as a single process. Rust files are excluded because Lizard cannot reliably detect Rust function boundaries.

### Hyperlinks

The HTML report generates clickable hyperlinks for every repo, file, class, and function. Remote URLs are parsed from `git remote -v` and support Azure DevOps (`dev.azure.com` and legacy `visualstudio.com`) and GitHub remotes.

### HTML Report

The generated report is a self-contained HTML file (inline CSS + JS, no external dependencies) that includes:

**Summary cards** -- total repos, files, functions, flagged counts.
**Distribution charts** -- function length and complexity histograms.
**Combined risk table** -- functions exceeding both thresholds (highest refactoring priority).
**Long functions table** -- sorted by line count.
**High complexity table** -- sorted by cyclomatic complexity.
**Per-repo breakdown** -- aggregated stats per repository.

All tables are sortable and filterable.

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
