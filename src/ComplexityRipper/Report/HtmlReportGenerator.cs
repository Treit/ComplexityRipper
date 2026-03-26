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
    public void Generate(AnalysisResult data, string outputPath, int thresholdLines = 200, int thresholdComplexity = 25, string theme = "light")
    {
        var sb = new StringBuilder();

        var repoLookup = data.Repos.ToDictionary(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase);

        var repoStats = data.Functions
            .GroupBy(f => f.Repo)
            .Select(g => BuildRepoStats(g.Key, g.ToList(), repoLookup, thresholdLines, thresholdComplexity))
            .OrderByDescending(r => r.ConcernScore)
            .ToList();

        int totalLong = repoStats.Sum(r => r.LongCount);
        int totalComplex = repoStats.Sum(r => r.ComplexCount);
        int totalCombined = repoStats.Sum(r => r.CombinedCount);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        AppendHead(sb, theme);
        sb.AppendLine("<body>");
        AppendHeader(sb, data, thresholdLines, thresholdComplexity);
        AppendSummaryCards(sb, data, totalLong, totalComplex, totalCombined);
        AppendRepoRanking(sb, repoStats, thresholdLines, thresholdComplexity);
        AppendPerRepoDetails(sb, repoStats, repoLookup, thresholdLines, thresholdComplexity);
        AppendFooter(sb);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(outputPath, sb.ToString());
    }

    private record RepoStatsRow(
        string Name,
        string? BaseUrl,
        string DefaultBranch,
        string? Lifecycle,
        int FileCount,
        int FunctionCount,
        int LongCount,
        int ComplexCount,
        int CombinedCount,
        double AvgComplexity,
        int MaxComplexity,
        int MaxLines,
        int ConcernScore,
        List<FunctionMetrics> All,
        List<FunctionMetrics> Long,
        List<FunctionMetrics> Complex,
        List<FunctionMetrics> Combined);

    private static RepoStatsRow BuildRepoStats(
        string repoName,
        List<FunctionMetrics> functions,
        Dictionary<string, RepoInfo> repoLookup,
        int thresholdLines,
        int thresholdComplexity)
    {
        repoLookup.TryGetValue(repoName, out var info);

        var longFuncs = functions.Where(f => f.LineCount >= thresholdLines).OrderByDescending(f => f.LineCount).ToList();
        var complexFuncs = functions.Where(f => f.CyclomaticComplexity >= thresholdComplexity).OrderByDescending(f => f.CyclomaticComplexity).ToList();
        var combinedFuncs = functions.Where(f => f.LineCount >= thresholdLines && f.CyclomaticComplexity >= thresholdComplexity)
            .OrderByDescending(f => f.CyclomaticComplexity * f.LineCount).ToList();

        // Concern score: weighted sum of flagged functions
        // Combined risk (both thresholds) = 10 points each
        // Complex only = 3 points each
        // Long only = 2 points each
        // Extra points for extreme values
        int score = combinedFuncs.Count * 10
            + (complexFuncs.Count - combinedFuncs.Count) * 3
            + (longFuncs.Count - combinedFuncs.Count) * 2;

        foreach (var f in functions)
        {
            if (f.CyclomaticComplexity >= thresholdComplexity * 2)
            {
                score += 5;
            }

            if (f.LineCount >= thresholdLines * 2)
            {
                score += 3;
            }
        }

        return new RepoStatsRow(
            Name: repoName,
            BaseUrl: info?.AdoBaseUrl,
            DefaultBranch: info?.DefaultBranch ?? "main",
            Lifecycle: info?.Lifecycle,
            FileCount: info?.FileCount ?? 0,
            FunctionCount: functions.Count,
            LongCount: longFuncs.Count,
            ComplexCount: complexFuncs.Count,
            CombinedCount: combinedFuncs.Count,
            AvgComplexity: functions.Count > 0 ? functions.Average(f => f.CyclomaticComplexity) : 0,
            MaxComplexity: functions.Count > 0 ? functions.Max(f => f.CyclomaticComplexity) : 0,
            MaxLines: functions.Count > 0 ? functions.Max(f => f.LineCount) : 0,
            ConcernScore: score,
            All: functions,
            Long: longFuncs,
            Complex: complexFuncs,
            Combined: combinedFuncs);
    }

    private void AppendHead(StringBuilder sb, string defaultTheme)
    {
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Code Complexity Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(GetThemeCss());
        sb.AppendLine(GetLayoutCss());
        sb.AppendLine("</style>");
        sb.AppendLine($@"<script>(function() {{
  var t = localStorage.getItem('sentinel-reports-theme');
  if (!t) {{
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) t = 'dark';
    else if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) t = 'light';
    else t = '{defaultTheme}';
  }}
  document.documentElement.setAttribute('data-theme', t);
}})()</script>");
        sb.AppendLine("</head>");
    }

    private static string GetThemeCss() => @"
/* ── Dark ── */
[data-theme=""dark""] {
    --bg: #0d1117;
    --bg-secondary: #161b22;
    --bg-tertiary: #1c2128;
    --border: #30363d;
    --text: #e6edf3;
    --text-muted: #8b949e;
    --link: #58a6ff;
    --severity-ok: #3fb950;
    --severity-medium: #d29922;
    --severity-high: #db6d28;
    --severity-critical: #f85149;
    --info: #58a6ff;
    --bar-1: #3fb950;
    --bar-2: #3fb950;
    --bar-3: #d29922;
    --bar-4: #db6d28;
    --bar-5: #f85149;
}

/* ── Light ── */
[data-theme=""light""] {
    --bg: #ffffff;
    --bg-secondary: #f6f8fa;
    --bg-tertiary: #eaeef2;
    --border: #d0d7de;
    --text: #1f2328;
    --text-muted: #656d76;
    --link: #0969da;
    --severity-ok: #1a7f37;
    --severity-medium: #9a6700;
    --severity-high: #bc4c00;
    --severity-critical: #cf222e;
    --info: #0969da;
    --bar-1: #1a7f37;
    --bar-2: #1a7f37;
    --bar-3: #9a6700;
    --bar-4: #bc4c00;
    --bar-5: #cf222e;
}

/* ── High-contrast dark ── */
[data-theme=""high-contrast""] {
    --bg: #000000;
    --bg-secondary: #0a0a0a;
    --bg-tertiary: #151515;
    --border: #444444;
    --text: #ffffff;
    --text-muted: #b0b0b0;
    --link: #71b7ff;
    --severity-ok: #56d364;
    --severity-medium: #f0c000;
    --severity-high: #ff9e3a;
    --severity-critical: #ff6a69;
    --info: #71b7ff;
    --bar-1: #56d364;
    --bar-2: #56d364;
    --bar-3: #f0c000;
    --bar-4: #ff9e3a;
    --bar-5: #ff6a69;
}

/* ── Ink (print-friendly, minimal color) ── */
[data-theme=""ink""] {
    --bg: #ffffff;
    --bg-secondary: #fafafa;
    --bg-tertiary: #f0f0f0;
    --border: #cccccc;
    --text: #111111;
    --text-muted: #666666;
    --link: #1a5276;
    --severity-ok: #444444;
    --severity-medium: #7d5a00;
    --severity-high: #9e3500;
    --severity-critical: #8b0000;
    --info: #1a5276;
    --bar-1: #a0a0a0;
    --bar-2: #888888;
    --bar-3: #7d5a00;
    --bar-4: #9e3500;
    --bar-5: #8b0000;
}
";

    private static string GetLayoutCss() => @"
* { box-sizing: border-box; margin: 0; padding: 0; }
body { background: var(--bg); color: var(--text); font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; line-height: 1.6; padding: 20px; }
.container { max-width: 1600px; margin: 0 auto; }
h1 { font-size: 28px; margin-bottom: 8px; }
h2 { font-size: 22px; margin: 32px 0 16px 0; padding-bottom: 8px; border-bottom: 1px solid var(--border); }
h3 { font-size: 18px; margin: 16px 0 8px 0; }
.subtitle { color: var(--text-muted); font-size: 14px; margin-bottom: 24px; }
a { color: var(--link); text-decoration: none; }
a:hover { text-decoration: underline; }

/* Theme switcher */
.theme-switcher { display: inline-flex; align-items: center; gap: 6px; float: right; margin-top: 4px; }
.theme-switcher label { font-size: 12px; color: var(--text-muted); }
.theme-switcher select { background: var(--bg-tertiary); border: 1px solid var(--border); border-radius: 4px; color: var(--text); padding: 4px 8px; font-size: 12px; cursor: pointer; }

/* Summary table */
.summary-table { border-collapse: collapse; margin-bottom: 24px; font-size: 14px; width: auto; }
.summary-table td { padding: 4px 8px 4px 0; white-space: nowrap; border: none; }
.summary-table td:first-child { color: var(--text-muted); padding-right: 12px; }
.summary-table td:last-child { font-weight: 600; text-align: left; }

/* Tables */
.table-container { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 8px; overflow-x: auto; margin-bottom: 24px; }
.table-header { display: flex; justify-content: space-between; align-items: center; padding: 12px 16px; border-bottom: 1px solid var(--border); flex-wrap: wrap; gap: 8px; }
.table-header h3 { margin: 0; }
.table-filter { background: var(--bg-tertiary); border: 1px solid var(--border); border-radius: 4px; color: var(--text); padding: 6px 10px; font-size: 13px; width: 250px; }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
thead { background: var(--bg-tertiary); }
th { padding: 8px 10px; text-align: left; font-weight: 600; cursor: pointer; white-space: nowrap; user-select: none; }
th:hover { background: var(--border); }
th .sort-arrow { margin-left: 4px; font-size: 10px; color: var(--text-muted); }
td { padding: 6px 10px; border-top: 1px solid var(--border); white-space: nowrap; }
tr:hover { background: var(--bg-tertiary); }
.numeric { text-align: right; font-variant-numeric: tabular-nums; }
.severity-critical { color: var(--severity-critical); font-weight: 700; }
.severity-high { color: var(--severity-high); font-weight: 600; }
.severity-medium { color: var(--severity-medium); }
.severity-ok { color: var(--severity-ok); }
.mono { font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace; font-size: 12px; }
.truncate { max-width: 400px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.count-badge { display: inline-block; background: var(--bg-tertiary); border: 1px solid var(--border); border-radius: 12px; padding: 2px 8px; font-size: 12px; color: var(--text-muted); margin-left: 8px; }
.count-badge.severity-critical { background: var(--severity-critical); color: #fff; border-color: var(--severity-critical); font-weight: 700; }
.count-badge.severity-high { background: var(--severity-high); color: #fff; border-color: var(--severity-high); font-weight: 700; }
.count-badge.severity-medium { background: var(--severity-medium); color: #fff; border-color: var(--severity-medium); font-weight: 700; }
.count-badge.severity-ok { color: var(--severity-ok); font-weight: 700; }
.lc-ga { color: var(--severity-ok); font-weight: 600; }
.lc-dev { color: var(--severity-medium); }
.lc-preview { color: var(--info); }
.lc-closing { color: var(--text-muted); font-style: italic; }
.repo-separator td { background: var(--bg-tertiary); border-top: 2px solid var(--border); }
.repo-heading { font-weight: 700; font-size: 14px; padding: 10px 12px; }
.repo-detail-section { margin: 24px 0; padding: 20px; background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 8px; }
.repo-detail-section h3 { margin: 0 0 4px 0; font-size: 20px; }
.repo-detail-section .subtitle { margin-bottom: 16px; }
.repo-detail-section .table-container { border: 1px solid var(--border); margin-bottom: 16px; }

/* Charts */
.chart-container { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 8px; padding: 20px; margin-bottom: 24px; }
.chart-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
.bar-chart { display: flex; flex-direction: column; gap: 6px; }
.bar-row { display: flex; align-items: center; gap: 8px; }
.bar-label { width: 100px; text-align: right; font-size: 13px; color: var(--text-muted); flex-shrink: 0; }
.bar-track { flex: 1; background: var(--bg-tertiary); border-radius: 3px; height: 22px; position: relative; }
.bar-fill { height: 100%; border-radius: 3px; display: flex; align-items: center; padding-left: 8px; font-size: 12px; font-weight: 600; min-width: fit-content; color: #fff; }
.bar-count { font-size: 12px; color: var(--text-muted); margin-left: 8px; flex-shrink: 0; width: 60px; }

@media (max-width: 768px) {
    .chart-grid { grid-template-columns: 1fr; }
    .cards { grid-template-columns: repeat(2, 1fr); }
    .table-filter { width: 150px; }
}
";

    private void AppendHeader(StringBuilder sb, AnalysisResult data, int thresholdLines, int thresholdComplexity)
    {
        sb.AppendLine("<div class=\"container\">");
        sb.AppendLine("<div class=\"theme-switcher\">");
        sb.AppendLine("  <label>Theme:</label>");
        sb.AppendLine("  <select onchange=\"setTheme(this.value)\" id=\"theme-select\">");
        sb.AppendLine("    <option value=\"dark\">Dark</option>");
        sb.AppendLine("    <option value=\"light\">Light</option>");
        sb.AppendLine("    <option value=\"high-contrast\">High contrast</option>");
        sb.AppendLine("    <option value=\"ink\">Ink (print)</option>");
        sb.AppendLine("  </select>");
        sb.AppendLine("</div>");
        sb.AppendLine("<h1>Code Complexity Report</h1>");
        sb.AppendLine($"<p class=\"subtitle\">Generated {data.Metadata.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC} &nbsp;|&nbsp; " +
                       $"Thresholds: {thresholdLines} lines, {thresholdComplexity} complexity &nbsp;|&nbsp; " +
                       $"Root: {Encode(data.Metadata.RootPath)}</p>");
    }

    private void AppendSummaryCards(StringBuilder sb, AnalysisResult data, int longCount, int complexCount, int combinedCount)
    {
        var longClass = longCount > 100 ? "severity-critical" : longCount > 20 ? "severity-high" : longCount > 0 ? "severity-medium" : "";
        var complexClass = complexCount > 100 ? "severity-critical" : complexCount > 20 ? "severity-high" : complexCount > 0 ? "severity-medium" : "";
        var combinedClass = combinedCount > 50 ? "severity-critical" : combinedCount > 10 ? "severity-high" : combinedCount > 0 ? "severity-medium" : "";

        sb.AppendLine("<table class=\"summary-table\"><tbody>");
        sb.AppendLine($"<tr><td>Repositories</td><td>{data.Summary.TotalRepos:N0}</td></tr>");
        sb.AppendLine($"<tr><td>Files analyzed</td><td>{data.Summary.TotalFiles:N0}</td></tr>");
        sb.AppendLine($"<tr><td>Functions</td><td>{data.Summary.TotalFunctions:N0}</td></tr>");
        sb.AppendLine($"<tr><td>Long functions</td><td class=\"{longClass}\">{longCount:N0}</td></tr>");
        sb.AppendLine($"<tr><td>Complex functions</td><td class=\"{complexClass}\">{complexCount:N0}</td></tr>");
        sb.AppendLine($"<tr><td>Combined risk</td><td class=\"{combinedClass}\">{combinedCount:N0}</td></tr>");
        sb.AppendLine("</tbody></table>");
    }

    private void AppendDistributionCharts(StringBuilder sb, List<FunctionMetrics> functions)
    {
        sb.AppendLine("<div class=\"chart-container\">");
        sb.AppendLine("<div class=\"chart-grid\">");

        var lengthBuckets = new (string label, int min, int max, string color)[]
        {
            ("0-50", 0, 50, "var(--bar-1)"),
            ("51-100", 51, 100, "var(--bar-2)"),
            ("101-200", 101, 200, "var(--bar-3)"),
            ("201-500", 201, 500, "var(--bar-4)"),
            ("500+", 501, int.MaxValue, "var(--bar-5)"),
        };

        sb.AppendLine("<div>");
        sb.AppendLine("<h3>Function Length (lines)</h3>");
        AppendBarChart(sb, functions, f => f.LineCount, lengthBuckets);
        sb.AppendLine("</div>");

        var complexityBuckets = new (string label, int min, int max, string color)[]
        {
            ("1-5", 1, 5, "var(--bar-1)"),
            ("6-10", 6, 10, "var(--bar-2)"),
            ("11-25", 11, 25, "var(--bar-3)"),
            ("26-50", 26, 50, "var(--bar-4)"),
            ("50+", 51, int.MaxValue, "var(--bar-5)"),
        };

        sb.AppendLine("<div>");
        sb.AppendLine("<h3>Cyclomatic Complexity</h3>");
        AppendBarChart(sb, functions, f => f.CyclomaticComplexity, complexityBuckets);
        sb.AppendLine("</div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
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

    private void AppendRepoRanking(StringBuilder sb, List<RepoStatsRow> repoStats, int thresholdLines, int thresholdComplexity)
    {
        sb.AppendLine("<h2>Repository Ranking</h2>");
        sb.AppendLine("<div class=\"table-container\">");
        sb.AppendLine("<div class=\"table-header\">");
        sb.AppendLine("  <span style=\"color: var(--text-muted); font-size: 13px;\">Ranked by concern score (worst first)</span>");
        sb.AppendLine($"  <input type=\"text\" class=\"table-filter\" placeholder=\"Filter...\" oninput=\"filterTable('repo-ranking', this.value)\">");
        sb.AppendLine("</div>");

        sb.AppendLine("<table id=\"repo-ranking\">");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 0, 'number')\" class=\"numeric\"># <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 1, 'string')\">Repository <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 2, 'string')\">Lifecycle <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine($"<th onclick=\"sortTable('repo-ranking', 3, 'number')\" class=\"numeric\" title=\"Weighted sum: 10 pts per combined-risk function (exceeds both thresholds), 3 pts per complex-only, 2 pts per long-only. Bonus: +5 if complexity &gt;= {thresholdComplexity * 2}, +3 if lines &gt;= {thresholdLines * 2}.\">Concern Score <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 4, 'number')\" class=\"numeric\">Files <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 5, 'number')\" class=\"numeric\">Functions <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 6, 'number')\" class=\"numeric\">Combined <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 7, 'number')\" class=\"numeric\">Long <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 8, 'number')\" class=\"numeric\">Complex <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 9, 'number')\" class=\"numeric\">Max CC <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('repo-ranking', 10, 'number')\" class=\"numeric\">Max Lines <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");

        int rank = 1;
        foreach (var r in repoStats)
        {
            var scoreSeverity = r.ConcernScore >= 50 ? "severity-critical" : r.ConcernScore >= 20 ? "severity-high" : r.ConcernScore >= 5 ? "severity-medium" : "severity-ok";
            var combinedSeverity = r.CombinedCount > 5 ? "severity-critical" : r.CombinedCount > 0 ? "severity-high" : "";

            string repoCell = r.ConcernScore > 0
                ? $"<a href=\"#repo-{Encode(r.Name.Replace(" ", "-"))}\">{Encode(r.Name)}</a>"
                : Encode(r.Name);

            sb.AppendLine("<tr>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{rank}\">{rank}</td>");
            sb.AppendLine($"  <td class=\"mono\">{repoCell}</td>");
            var lcClass = GetLifecycleCssClass(r.Lifecycle);
            var lcDisplay = Encode(r.Lifecycle ?? "-");
            sb.AppendLine($"  <td class=\"{lcClass}\">{lcDisplay}</td>");
            sb.AppendLine($"  <td class=\"numeric {scoreSeverity}\" data-v=\"{r.ConcernScore}\">{r.ConcernScore}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{r.FileCount}\">{r.FileCount}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{r.FunctionCount}\">{r.FunctionCount}</td>");
            sb.AppendLine($"  <td class=\"numeric {combinedSeverity}\" data-v=\"{r.CombinedCount}\">{r.CombinedCount}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{r.LongCount}\">{r.LongCount}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{r.ComplexCount}\">{r.ComplexCount}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{r.MaxComplexity}\">{r.MaxComplexity}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{r.MaxLines}\">{r.MaxLines}</td>");
            sb.AppendLine("</tr>");
            rank++;
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
    }

    private void AppendPerRepoDetails(StringBuilder sb, List<RepoStatsRow> repoStats, Dictionary<string, RepoInfo> repoLookup, int thresholdLines, int thresholdComplexity)
    {
        sb.AppendLine("<h2>Per-Repository Details</h2>");

        var reposWithIssues = repoStats.Where(r => r.ConcernScore > 0).ToList();
        var cleanRepos = repoStats.Where(r => r.ConcernScore == 0).ToList();

        if (reposWithIssues.Count == 0)
        {
            sb.AppendLine("<p style=\"color: var(--severity-ok);\">No repositories have flagged functions.</p>");
            return;
        }

        foreach (var repo in reposWithIssues)
        {
            var anchor = repo.Name.Replace(" ", "-");
            var scoreSeverity = repo.ConcernScore >= 50 ? "severity-critical" : repo.ConcernScore >= 20 ? "severity-high" : repo.ConcernScore >= 5 ? "severity-medium" : "severity-ok";

            string repoLabel = Encode(repo.Name);
            if (repo.BaseUrl != null)
            {
                repoLabel = $"<a href=\"{Encode(repo.BaseUrl)}\" target=\"_blank\">{Encode(repo.Name)}</a>";
            }

            sb.AppendLine($"<div class=\"repo-detail-section\" id=\"repo-{Encode(anchor)}\">");
            sb.AppendLine($"<h3>{repoLabel} <span class=\"count-badge {scoreSeverity}\" title=\"Weighted sum: 10 pts per combined-risk function, 3 pts per complex-only, 2 pts per long-only, plus bonuses for extreme values.\">score: {repo.ConcernScore}</span></h3>");
            sb.AppendLine($"<p class=\"subtitle\">{repo.FunctionCount:N0} functions across {repo.FileCount:N0} files &nbsp;|&nbsp; " +
                          $"Combined: {repo.CombinedCount} &nbsp;|&nbsp; Long: {repo.LongCount} &nbsp;|&nbsp; Complex: {repo.ComplexCount}</p>");

            var tablePrefix = $"repo-{anchor}";

            AppendDistributionCharts(sb, repo.All);

            if (repo.Combined.Count > 0)
            {
                AppendRepoFunctionTable(sb, "Combined Risk (Long + Complex)", repo.Combined, repo, $"{tablePrefix}-combined");
            }

            if (repo.Long.Count > 0)
            {
                AppendRepoFunctionTable(sb, $"Long Functions ({thresholdLines}+ lines)", repo.Long, repo, $"{tablePrefix}-long");
            }

            if (repo.Complex.Count > 0)
            {
                AppendRepoFunctionTable(sb, $"High Complexity (CC >= {thresholdComplexity})", repo.Complex, repo, $"{tablePrefix}-complex");
            }

            sb.AppendLine("</div>");
        }

        if (cleanRepos.Count > 0)
        {
            sb.AppendLine($"<p style=\"color: var(--text-muted); margin-top: 16px;\">{cleanRepos.Count} repositories have no flagged functions: ");
            sb.AppendLine(string.Join(", ", cleanRepos.Select(r => Encode(r.Name))));
            sb.AppendLine("</p>");
        }
    }

    private void AppendRepoFunctionTable(StringBuilder sb, string title, List<FunctionMetrics> functions, RepoStatsRow repo, string tableId)
    {
        sb.AppendLine($"<div class=\"table-container\">");
        sb.AppendLine("<div class=\"table-header\">");
        sb.AppendLine($"  <span style=\"font-weight: 600; font-size: 13px;\">{Encode(title)} <span class=\"count-badge\">{functions.Count}</span></span>");
        sb.AppendLine($"  <input type=\"text\" class=\"table-filter\" placeholder=\"Filter...\" oninput=\"filterTable('{tableId}', this.value)\">");
        sb.AppendLine("</div>");

        sb.AppendLine($"<table id=\"{tableId}\">");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 0, 'string')\">Project <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 1, 'string')\">File <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 2, 'string')\">Class <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 3, 'string')\">Function <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 4, 'number')\" class=\"numeric\">Lines <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 5, 'number')\" class=\"numeric\">Complexity <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 6, 'number')\" class=\"numeric\">Params <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("<th onclick=\"sortTable('" + tableId + "', 7, 'number')\" class=\"numeric\">Nesting <span class=\"sort-arrow\">⇅</span></th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");

        foreach (var f in functions)
        {
            string fileCell = Encode(f.File);
            if (repo.BaseUrl != null)
            {
                var fileUrl = AdoUrlHelper.BuildFileUrl(repo.BaseUrl, f.File, f.StartLine, f.EndLine, repo.DefaultBranch);
                fileCell = $"<a href=\"{Encode(fileUrl)}\" target=\"_blank\" title=\"{Encode(f.File)}\">{Encode(GetShortFileName(f.File))}</a>";
            }

            string classCell = Encode(f.ClassName ?? "");
            if (repo.BaseUrl != null && f.ClassName != null)
            {
                var classFileUrl = AdoUrlHelper.BuildFileUrl(repo.BaseUrl, f.File, repo.DefaultBranch);
                classCell = $"<a href=\"{Encode(classFileUrl)}\" target=\"_blank\">{Encode(f.ClassName)}</a>";
            }

            string funcCell = Encode(f.Function);
            if (repo.BaseUrl != null)
            {
                var funcUrl = AdoUrlHelper.BuildFileUrl(repo.BaseUrl, f.File, f.StartLine, f.EndLine, repo.DefaultBranch);
                funcCell = $"<a href=\"{Encode(funcUrl)}\" target=\"_blank\">{Encode(f.Function)}</a>";
            }

            var lineSeverity = GetSeverityClass(f.LineCount, 500, 300, 200);
            var complexitySeverity = GetSeverityClass(f.CyclomaticComplexity, 30, 20, 15);
            var nestingSeverity = GetSeverityClass(f.MaxNestingDepth, 7, 5, 3);

            sb.AppendLine("<tr>");
            sb.AppendLine($"  <td class=\"mono\">{Encode(f.Project ?? "")}</td>");
            sb.AppendLine($"  <td class=\"mono truncate\">{fileCell}</td>");
            sb.AppendLine($"  <td class=\"mono truncate\">{classCell}</td>");
            sb.AppendLine($"  <td class=\"mono\">{funcCell}</td>");
            sb.AppendLine($"  <td class=\"numeric {lineSeverity}\" data-v=\"{f.LineCount}\">{f.LineCount}</td>");
            sb.AppendLine($"  <td class=\"numeric {complexitySeverity}\" data-v=\"{f.CyclomaticComplexity}\">{f.CyclomaticComplexity}</td>");
            sb.AppendLine($"  <td class=\"numeric\" data-v=\"{f.ParameterCount}\">{f.ParameterCount}</td>");
            sb.AppendLine($"  <td class=\"numeric {nestingSeverity}\" data-v=\"{f.MaxNestingDepth}\">{f.MaxNestingDepth}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
    }

    private void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("<div style=\"text-align: center; margin-top: 40px; padding: 20px; color: var(--text-muted); font-size: 12px;\">");
        sb.AppendLine("  Generated by ComplexityRipper");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>"); // container

        // Inline JavaScript for sorting and filtering
        sb.AppendLine("<script>");
        sb.AppendLine(GetJavaScript());
        sb.AppendLine("</script>");
    }

    private string GetJavaScript() => @"
function setTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem('sentinel-reports-theme', theme); } catch(e) {}
    var sel = document.getElementById('theme-select');
    if (sel) sel.value = theme;
}

(function() {
    var theme = document.documentElement.getAttribute('data-theme') || 'dark';
    var sel = document.getElementById('theme-select');
    if (sel) sel.value = theme;
})();

window.addEventListener('pageshow', function(e) {
    var t = localStorage.getItem('sentinel-reports-theme');
    if (t) setTheme(t);
});

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
    var table = document.getElementById(tableId);
    if (!table) return;
    var rows = table.querySelectorAll('tbody tr');
    var q = query.toLowerCase();

    rows.forEach(function(row) {
        var text = row.textContent.toLowerCase();
        row.style.display = (!q || text.includes(q)) ? '' : 'none';
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

    private static string GetLifecycleCssClass(string? lifecycle) => lifecycle switch
    {
        "GA" => "lc-ga",
        "In Development" => "lc-dev",
        "Public Preview" or "Private Preview" => "lc-preview",
        "Closing Down" => "lc-closing",
        _ => ""
    };
}
