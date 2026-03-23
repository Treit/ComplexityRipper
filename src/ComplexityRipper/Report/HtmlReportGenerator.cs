using System.Text;
using System.Web;
using ComplexityRipper.Models;
using ComplexityRipper.Utilities;

namespace ComplexityRipper.Report;

/// <summary>
/// Generates a self-contained HTML report from analysis results.
/// Matches the dark GitHub-like theme of the existing build health report.
/// </summary>
public class HtmlReportGenerator
{
    public void Generate(AnalysisResult data, string outputPath, int thresholdLines = 200, int thresholdComplexity = 15)
    {
        var sb = new StringBuilder();

        // Build ADO URL lookup from repo info
        var adoUrls = data.Repos.ToDictionary(r => r.Name, r => r.AdoBaseUrl, StringComparer.OrdinalIgnoreCase);

        // Classify functions
        var longFunctions = data.Functions
            .Where(f => f.LineCount >= thresholdLines)
            .OrderByDescending(f => f.LineCount)
            .ToList();

        var complexFunctions = data.Functions
            .Where(f => f.CyclomaticComplexity >= thresholdComplexity)
            .OrderByDescending(f => f.CyclomaticComplexity)
            .ToList();

        var combinedRisk = data.Functions
            .Where(f => f.LineCount >= thresholdLines && f.CyclomaticComplexity >= thresholdComplexity)
            .OrderByDescending(f => f.CyclomaticComplexity * f.LineCount)
            .ToList();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        AppendHead(sb);
        sb.AppendLine("<body>");
        AppendHeader(sb, data, thresholdLines, thresholdComplexity);
        AppendSummaryCards(sb, data, longFunctions.Count, complexFunctions.Count, combinedRisk.Count);
        AppendLanguageBreakdown(sb, data);
        AppendDistributionCharts(sb, data, thresholdLines, thresholdComplexity);
        AppendCombinedRiskTable(sb, combinedRisk, adoUrls);
        AppendFunctionTable(sb, "Long Functions", $"Functions with {thresholdLines}+ lines", longFunctions, adoUrls, "long-functions");
        AppendFunctionTable(sb, "High Complexity Functions", $"Functions with cyclomatic complexity ≥ {thresholdComplexity}", complexFunctions, adoUrls, "complex-functions");
        AppendRepoBreakdown(sb, data, thresholdLines, thresholdComplexity);
        AppendFooter(sb);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(outputPath, sb.ToString());
    }

    private void AppendHead(StringBuilder sb)
    {
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Code Complexity Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(GetCss());
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
    }

    private string GetCss() => @"
:root {
    --bg: #0d1117;
    --bg-secondary: #161b22;
    --bg-tertiary: #1c2128;
    --border: #30363d;
    --text: #e6edf3;
    --text-muted: #8b949e;
    --green: #3fb950;
    --yellow: #d29922;
    --orange: #db6d28;
    --red: #f85149;
    --blue: #58a6ff;
    --purple: #bc8cff;
}

* { box-sizing: border-box; margin: 0; padding: 0; }
body { background: var(--bg); color: var(--text); font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; line-height: 1.6; padding: 20px; }
.container { max-width: 1400px; margin: 0 auto; }
h1 { font-size: 28px; margin-bottom: 8px; }
h2 { font-size: 22px; margin: 32px 0 16px 0; padding-bottom: 8px; border-bottom: 1px solid var(--border); }
h3 { font-size: 18px; margin: 16px 0 8px 0; }
.subtitle { color: var(--text-muted); font-size: 14px; margin-bottom: 24px; }
a { color: var(--blue); text-decoration: none; }
a:hover { text-decoration: underline; }

/* Summary cards */
.cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 32px; }
.card { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 8px; padding: 16px; text-align: center; }
.card .value { font-size: 32px; font-weight: 700; }
.card .label { font-size: 13px; color: var(--text-muted); margin-top: 4px; }
.card.ok .value { color: var(--green); }
.card.warn .value { color: var(--yellow); }
.card.danger .value { color: var(--red); }
.card.critical .value { color: var(--red); font-weight: 900; }
.card.info .value { color: var(--blue); }

/* Tables */
.table-container { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 8px; overflow: hidden; margin-bottom: 24px; }
.table-header { display: flex; justify-content: space-between; align-items: center; padding: 12px 16px; border-bottom: 1px solid var(--border); }
.table-header h3 { margin: 0; }
.table-filter { background: var(--bg-tertiary); border: 1px solid var(--border); border-radius: 4px; color: var(--text); padding: 6px 10px; font-size: 13px; width: 250px; }
table { width: 100%; border-collapse: collapse; font-size: 14px; }
thead { background: var(--bg-tertiary); }
th { padding: 10px 12px; text-align: left; font-weight: 600; cursor: pointer; white-space: nowrap; user-select: none; }
th:hover { background: var(--border); }
th .sort-arrow { margin-left: 4px; font-size: 10px; color: var(--text-muted); }
td { padding: 8px 12px; border-top: 1px solid var(--border); }
tr:hover { background: var(--bg-tertiary); }
.numeric { text-align: right; font-variant-numeric: tabular-nums; }
.severity-critical { color: var(--red); font-weight: 700; }
.severity-high { color: var(--orange); font-weight: 600; }
.severity-medium { color: var(--yellow); }
.severity-ok { color: var(--green); }
.mono { font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace; font-size: 13px; }
.truncate { max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.count-badge { display: inline-block; background: var(--bg-tertiary); border: 1px solid var(--border); border-radius: 12px; padding: 2px 8px; font-size: 12px; color: var(--text-muted); margin-left: 8px; }

/* Charts */
.chart-container { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 8px; padding: 20px; margin-bottom: 24px; }
.chart-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
.bar-chart { display: flex; flex-direction: column; gap: 6px; }
.bar-row { display: flex; align-items: center; gap: 8px; }
.bar-label { width: 100px; text-align: right; font-size: 13px; color: var(--text-muted); flex-shrink: 0; }
.bar-track { flex: 1; background: var(--bg-tertiary); border-radius: 3px; height: 22px; position: relative; }
.bar-fill { height: 100%; border-radius: 3px; display: flex; align-items: center; padding-left: 8px; font-size: 12px; font-weight: 600; min-width: fit-content; }
.bar-count { font-size: 12px; color: var(--text-muted); margin-left: 8px; flex-shrink: 0; width: 60px; }

/* Language breakdown */
.lang-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin-bottom: 24px; }
.lang-card { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 6px; padding: 12px; text-align: center; }
.lang-name { font-size: 14px; font-weight: 600; }
.lang-stats { font-size: 12px; color: var(--text-muted); }

@media (max-width: 768px) {
    .chart-grid { grid-template-columns: 1fr; }
    .cards { grid-template-columns: repeat(2, 1fr); }
    .table-filter { width: 150px; }
}
";

    private void AppendHeader(StringBuilder sb, AnalysisResult data, int thresholdLines, int thresholdComplexity)
    {
        sb.AppendLine("<div class=\"container\">");
        sb.AppendLine("<h1>📊 Code Complexity Report</h1>");
        sb.AppendLine($"<p class=\"subtitle\">Generated {data.Metadata.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC} &nbsp;|&nbsp; " +
                       $"Thresholds: {thresholdLines} lines, {thresholdComplexity} complexity &nbsp;|&nbsp; " +
                       $"Root: {Encode(data.Metadata.RootPath)}</p>");
    }

    private void AppendSummaryCards(StringBuilder sb, AnalysisResult data, int longCount, int complexCount, int combinedCount)
    {
        sb.AppendLine("<div class=\"cards\">");
        AppendCard(sb, data.Summary.TotalRepos.ToString("N0"), "Repositories", "info");
        AppendCard(sb, data.Summary.TotalFiles.ToString("N0"), "Files Analyzed", "info");
        AppendCard(sb, data.Summary.TotalFunctions.ToString("N0"), "Functions", "info");
        AppendCard(sb, longCount.ToString("N0"), "Long Functions", longCount > 100 ? "danger" : longCount > 20 ? "warn" : "ok");
        AppendCard(sb, complexCount.ToString("N0"), "Complex Functions", complexCount > 100 ? "danger" : complexCount > 20 ? "warn" : "ok");
        AppendCard(sb, combinedCount.ToString("N0"), "Combined Risk", combinedCount > 50 ? "critical" : combinedCount > 10 ? "danger" : combinedCount > 0 ? "warn" : "ok");
        sb.AppendLine("</div>");
    }

    private void AppendCard(StringBuilder sb, string value, string label, string cssClass)
    {
        sb.AppendLine($"<div class=\"card {cssClass}\">");
        sb.AppendLine($"  <div class=\"value\">{value}</div>");
        sb.AppendLine($"  <div class=\"label\">{label}</div>");
        sb.AppendLine("</div>");
    }

    private void AppendLanguageBreakdown(StringBuilder sb, AnalysisResult data)
    {
        if (data.Summary.LanguageBreakdown.Count == 0)
        {
            return;
        }

        sb.AppendLine("<h2>Language Breakdown</h2>");
        sb.AppendLine("<div class=\"lang-grid\">");
        foreach (var (lang, stats) in data.Summary.LanguageBreakdown.OrderByDescending(kv => kv.Value.Functions))
        {
            sb.AppendLine("<div class=\"lang-card\">");
            sb.AppendLine($"  <div class=\"lang-name\">{Encode(lang)}</div>");
            sb.AppendLine($"  <div class=\"lang-stats\">{stats.Functions:N0} functions</div>");
            if (stats.Files > 0)
            {
                sb.AppendLine($"  <div class=\"lang-stats\">{stats.Files:N0} files</div>");
            }

            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div>");
    }

    private void AppendDistributionCharts(StringBuilder sb, AnalysisResult data, int thresholdLines, int thresholdComplexity)
    {
        sb.AppendLine("<h2>Distribution</h2>");
        sb.AppendLine("<div class=\"chart-container\">");
        sb.AppendLine("<div class=\"chart-grid\">");

        // Function length distribution
        var lengthBuckets = new (string label, int min, int max, string color)[]
        {
            ("0–50", 0, 50, "var(--green)"),
            ("51–100", 51, 100, "var(--green)"),
            ("101–200", 101, 200, "var(--yellow)"),
            ("201–500", 201, 500, "var(--orange)"),
            ("500+", 501, int.MaxValue, "var(--red)"),
        };

        sb.AppendLine("<div>");
        sb.AppendLine("<h3>Function Length (lines)</h3>");
        AppendBarChart(sb, data.Functions, f => f.LineCount, lengthBuckets);
        sb.AppendLine("</div>");

        // Complexity distribution
        var complexityBuckets = new (string label, int min, int max, string color)[]
        {
            ("1–5", 1, 5, "var(--green)"),
            ("6–10", 6, 10, "var(--green)"),
            ("11–15", 11, 15, "var(--yellow)"),
            ("16–25", 16, 25, "var(--orange)"),
            ("25+", 26, int.MaxValue, "var(--red)"),
        };

        sb.AppendLine("<div>");
        sb.AppendLine("<h3>Cyclomatic Complexity</h3>");
        AppendBarChart(sb, data.Functions, f => f.CyclomaticComplexity, complexityBuckets);
        sb.AppendLine("</div>");

        sb.AppendLine("</div>"); // chart-grid
        sb.AppendLine("</div>"); // chart-container
    }

    private void AppendBarChart(StringBuilder sb, List<FunctionMetrics> functions, Func<FunctionMetrics, int> valueSelector, (string label, int min, int max, string color)[] buckets)
    {
        var counts = buckets.Select(b => functions.Count(f =>
        {
            var v = valueSelector(f);
            return v >= b.min && v <= b.max;
        })).ToArray();

        int maxCount = counts.Max();
        if (maxCount == 0)
        {
            maxCount = 1;
        }

        sb.AppendLine("<div class=\"bar-chart\">");
        for (int i = 0; i < buckets.Length; i++)
        {
            double pct = (double)counts[i] / maxCount * 100;
            sb.AppendLine("<div class=\"bar-row\">");
            sb.AppendLine($"  <span class=\"bar-label\">{buckets[i].label}</span>");
            sb.AppendLine($"  <div class=\"bar-track\"><div class=\"bar-fill\" style=\"width: {pct:F1}%; background: {buckets[i].color};\">{(pct > 15 ? counts[i].ToString("N0") : "")}</div></div>");
            sb.AppendLine($"  <span class=\"bar-count\">{counts[i]:N0}</span>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div>");
    }

    private void AppendCombinedRiskTable(StringBuilder sb, List<FunctionMetrics> functions, Dictionary<string, string?> adoUrls)
    {
        sb.AppendLine("<h2>⚠️ Combined Risk — Long AND Complex</h2>");
        if (functions.Count == 0)
        {
            sb.AppendLine("<p style=\"color: var(--green);\">No functions exceed both thresholds. 🎉</p>");
            return;
        }
        AppendFunctionTable(sb, null, "Functions exceeding BOTH line count and complexity thresholds — highest refactoring priority", functions, adoUrls, "combined-risk");
    }

    private void AppendFunctionTable(StringBuilder sb, string? title, string description, List<FunctionMetrics> functions, Dictionary<string, string?> adoUrls, string tableId)
    {
        if (title != null)
        {
            sb.AppendLine($"<h2>{Encode(title)} <span class=\"count-badge\">{functions.Count}</span></h2>");
        }

        sb.AppendLine("<div class=\"table-container\">");
        sb.AppendLine("<div class=\"table-header\">");
        sb.AppendLine($"  <span style=\"color: var(--text-muted); font-size: 13px;\">{Encode(description)}</span>");
        sb.AppendLine($"  <input type=\"text\" class=\"table-filter\" placeholder=\"Filter...\" oninput=\"filterTable('{tableId}', this.value)\">");
        sb.AppendLine("</div>");

        sb.AppendLine($"<table id=\"{tableId}\">");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 0, 'string')\">Repo <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 1, 'string')\">File <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 2, 'string')\">Class <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 3, 'string')\">Function <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 4, 'number')\" class=\"numeric\">Lines <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 5, 'number')\" class=\"numeric\">Complexity <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 6, 'number')\" class=\"numeric\">Params <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 7, 'number')\" class=\"numeric\">Nesting <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 8, 'string')\">Language <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");

        foreach (var f in functions)
        {
            string fileLink = Encode(f.File);
            if (adoUrls.TryGetValue(f.Repo, out var baseUrl) && baseUrl != null)
            {
                var adoUrl = AdoUrlHelper.BuildFileUrl(baseUrl, f.File, f.StartLine, f.EndLine);
                fileLink = $"<a href=\"{Encode(adoUrl)}\" target=\"_blank\" title=\"{Encode(f.File)}\">{Encode(GetShortFileName(f.File))}</a>";
            }

            var lineSeverity = GetSeverityClass(f.LineCount, 500, 300, 200);
            var complexitySeverity = GetSeverityClass(f.CyclomaticComplexity, 30, 20, 15);
            var nestingSeverity = GetSeverityClass(f.MaxNestingDepth, 7, 5, 3);

            sb.AppendLine("<tr>");
            sb.AppendLine($"  <td class=\"mono\">{Encode(f.Repo)}</td>");
            sb.AppendLine($"  <td class=\"mono truncate\">{fileLink}</td>");
            sb.AppendLine($"  <td class=\"mono truncate\">{Encode(f.ClassName ?? "")}</td>");
            sb.AppendLine($"  <td class=\"mono\">{Encode(f.Function)}</td>");
            sb.AppendLine($"  <td class=\"numeric {lineSeverity}\" data-v=\"{f.LineCount}\">{f.LineCount}</td>");
            sb.AppendLine($"  <td class=\"numeric {complexitySeverity}\" data-v=\"{f.CyclomaticComplexity}\">{f.CyclomaticComplexity}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{f.ParameterCount}\">{f.ParameterCount}</td>");
            sb.AppendLine($"  <td class=\"numeric {nestingSeverity}\" data-v=\"{f.MaxNestingDepth}\">{f.MaxNestingDepth}</td>");
            sb.AppendLine($"  <td>{Encode(f.Language)}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
    }

    private void AppendRepoBreakdown(StringBuilder sb, AnalysisResult data, int thresholdLines, int thresholdComplexity)
    {
        sb.AppendLine("<h2>Per-Repository Breakdown</h2>");
        sb.AppendLine("<div class=\"table-container\">");
        sb.AppendLine("<div class=\"table-header\">");
        sb.AppendLine("  <span style=\"color: var(--text-muted); font-size: 13px;\">Aggregated metrics per repository</span>");
        sb.AppendLine($"  <input type=\"text\" class=\"table-filter\" placeholder=\"Filter...\" oninput=\"filterTable('repo-breakdown', this.value)\">");
        sb.AppendLine("</div>");

        sb.AppendLine("<table id=\"repo-breakdown\">");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 0, 'string')\">Repository <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 1, 'number')\" class=\"numeric\">Files <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 2, 'number')\" class=\"numeric\">Functions <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 3, 'number')\" class=\"numeric\">Long <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 4, 'number')\" class=\"numeric\">Complex <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 5, 'number')\" class=\"numeric\">Avg Complexity <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 6, 'number')\" class=\"numeric\">Max Complexity <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-breakdown', 7, 'number')\" class=\"numeric\">Max Lines <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");

        var repoGroups = data.Functions.GroupBy(f => f.Repo).OrderBy(g => g.Key);
        foreach (var group in repoGroups)
        {
            var repoFunctions = group.ToList();
            var longCount = repoFunctions.Count(f => f.LineCount >= thresholdLines);
            var complexCount = repoFunctions.Count(f => f.CyclomaticComplexity >= thresholdComplexity);
            var avgComplexity = repoFunctions.Count > 0 ? repoFunctions.Average(f => f.CyclomaticComplexity) : 0;
            var maxComplexity = repoFunctions.Count > 0 ? repoFunctions.Max(f => f.CyclomaticComplexity) : 0;
            var maxLines = repoFunctions.Count > 0 ? repoFunctions.Max(f => f.LineCount) : 0;
            var repoInfo = data.Repos.FirstOrDefault(r => r.Name == group.Key);
            int fileCount = repoInfo?.FileCount ?? 0;

            var longSeverity = longCount > 10 ? "severity-critical" : longCount > 3 ? "severity-high" : longCount > 0 ? "severity-medium" : "severity-ok";
            var complexSeverity = complexCount > 10 ? "severity-critical" : complexCount > 3 ? "severity-high" : complexCount > 0 ? "severity-medium" : "severity-ok";

            sb.AppendLine("<tr>");
            sb.AppendLine($"  <td class=\"mono\">{Encode(group.Key)}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{fileCount}\">{fileCount}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{repoFunctions.Count}\">{repoFunctions.Count}</td>");
            sb.AppendLine($"  <td class=\"numeric {longSeverity}\" data-v=\"{longCount}\">{longCount}</td>");
            sb.AppendLine($"  <td class=\"numeric {complexSeverity}\" data-v=\"{complexCount}\">{complexCount}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{avgComplexity:F1}\">{avgComplexity:F1}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{maxComplexity}\">{maxComplexity}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{maxLines}\">{maxLines}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
    }

    private void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("<div style=\"text-align: center; margin-top: 40px; padding: 20px; color: var(--text-muted); font-size: 12px;\">");
        sb.AppendLine("  Generated by CodeAnalyzer — Roslyn + Lizard hybrid analysis");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>"); // container

        // Inline JavaScript for sorting and filtering
        sb.AppendLine("<script>");
        sb.AppendLine(GetJavaScript());
        sb.AppendLine("</script>");
    }

    private string GetJavaScript() => @"
const sortState = {};

function sortTable(tableId, colIndex, type) {
    const table = document.getElementById(tableId);
    const tbody = table.querySelector('tbody');
    const rows = Array.from(tbody.querySelectorAll('tr'));
    
    const key = tableId + '_' + colIndex;
    sortState[key] = sortState[key] === 'asc' ? 'desc' : 'asc';
    const ascending = sortState[key] === 'asc';
    
    rows.sort((a, b) => {
        const cellA = a.cells[colIndex];
        const cellB = b.cells[colIndex];
        let valA, valB;
        
        if (type === 'number') {
            valA = parseFloat(cellA.getAttribute('data-v') || cellA.textContent) || 0;
            valB = parseFloat(cellB.getAttribute('data-v') || cellB.textContent) || 0;
        } else {
            valA = (cellA.textContent || '').toLowerCase();
            valB = (cellB.textContent || '').toLowerCase();
        }
        
        if (valA < valB) return ascending ? -1 : 1;
        if (valA > valB) return ascending ? 1 : -1;
        return 0;
    });
    
    rows.forEach(row => tbody.appendChild(row));
    
    // Update sort arrows
    const headers = table.querySelectorAll('th');
    headers.forEach((th, i) => {
        const arrow = th.querySelector('.sort-arrow');
        if (arrow) arrow.textContent = i === colIndex ? (ascending ? '↑' : '↓') : '⇅';
    });
}

function filterTable(tableId, query) {
    const table = document.getElementById(tableId);
    const rows = table.querySelectorAll('tbody tr');
    const q = query.toLowerCase();
    
    rows.forEach(row => {
        const text = row.textContent.toLowerCase();
        row.style.display = text.includes(q) ? '' : 'none';
    });
}
";

    private static string GetSeverityClass(int value, int criticalThreshold, int highThreshold, int mediumThreshold)
    {
        if (value >= criticalThreshold)
        {
            return "severity-critical";
        }

        if (value >= highThreshold)
        {
            return "severity-high";
        }

        if (value >= mediumThreshold)
        {
            return "severity-medium";
        }

        return "";
    }

    private static string GetShortFileName(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        if (parts.Length <= 2)
        {
            return path;
        }

        return ".../" + string.Join("/", parts[^2..]);
    }

    private static string Encode(string text) => HttpUtility.HtmlEncode(text);
}
