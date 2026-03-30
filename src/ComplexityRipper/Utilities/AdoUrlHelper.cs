using System.Diagnostics;

namespace ComplexityRipper.Utilities;

/// <summary>
/// Parses git remote URLs to extract base URLs for ADO and GitHub repos.
/// Builds file URLs with line number ranges.
/// </summary>
public static class AdoUrlHelper
{
    /// <summary>
    /// Reads git remote -v from a repo directory and extracts the base URL.
    /// Supports ADO and GitHub remotes.
    /// Returns null if git fails or the remote is unrecognized.
    /// </summary>
    public static string? GetAdoBaseUrl(string repoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "remote -v",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return ParseRemoteUrl(output);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the default branch for a repo (main or master).
    /// Returns "main" if detection fails.
    /// </summary>
    public static string GetDefaultBranch(string repoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "symbolic-ref refs/remotes/origin/HEAD",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return "main";
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            if (!string.IsNullOrEmpty(output))
            {
                var lastSlash = output.LastIndexOf('/');
                return lastSlash >= 0 ? output[(lastSlash + 1)..] : output;
            }
        }
        catch
        {
            // fall through
        }

        return "main";
    }

    /// <summary>
    /// Parses the output of `git remote -v` to extract a base URL.
    /// Supports ADO (HTTPS and SSH) and GitHub remotes.
    /// </summary>
    public static string? ParseRemoteUrl(string gitRemoteOutput)
    {
        foreach (var line in gitRemoteOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var tabParts = trimmed.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (tabParts.Length < 2)
            {
                continue;
            }

            var urlAndSuffix = tabParts[1];
            if (!urlAndSuffix.Contains("(fetch)", StringComparison.Ordinal))
            {
                continue;
            }

            // Strip the " (fetch)" suffix to get the raw URL
            var fetchIdx = urlAndSuffix.LastIndexOf(" (fetch)", StringComparison.Ordinal);
            var urlPart = fetchIdx >= 0 ? urlAndSuffix[..fetchIdx].Trim() : urlAndSuffix.Trim();

            // ADO HTTPS: https://msdata@dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT
            // Legacy:   https://msdata.visualstudio.com/DefaultCollection/Sentinel%20Graph/_git/PerfBenchInfra
            if ((urlPart.Contains("dev.azure.com", StringComparison.Ordinal) || urlPart.Contains("visualstudio.com", StringComparison.Ordinal)) && urlPart.Contains("/_git/", StringComparison.Ordinal))
            {
                var uri = new Uri(urlPart.Replace(" ", "%20"));
                var segments = uri.AbsolutePath.Trim('/').Split('/');

                // Find the _git segment and extract project + repo from around it
                int gitIdx = Array.IndexOf(segments, "_git");
                if (gitIdx >= 1 && gitIdx < segments.Length - 1)
                {
                    var repo = segments[gitIdx + 1];

                    // For dev.azure.com: /{org}/{project}/_git/{repo}
                    // For visualstudio.com: /DefaultCollection/{project}/_git/{repo}
                    // We need the org. For visualstudio.com, org is the subdomain.
                    string org;
                    string project;

                    if (urlPart.Contains("dev.azure.com", StringComparison.Ordinal))
                    {
                        org = segments[0];
                        project = Uri.EscapeDataString(Uri.UnescapeDataString(
                            string.Join("/", segments[1..gitIdx])));
                    }
                    else
                    {
                        org = uri.Host.Split('.')[0];
                        var projectParts = new List<string>();
                        foreach (var s in segments)
                        {
                            if (s == "_git")
                            {
                                break;
                            }

                            if (!s.Equals("DefaultCollection", StringComparison.OrdinalIgnoreCase))
                            {
                                projectParts.Add(s);
                            }
                        }

                        project = Uri.EscapeDataString(Uri.UnescapeDataString(
                            string.Join("/", projectParts)));
                    }

                    return $"https://dev.azure.com/{org}/{project}/_git/{repo}";
                }
            }

            // ADO SSH: git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
            if (urlPart.Contains("ssh.dev.azure.com", StringComparison.Ordinal))
            {
                var parts = urlPart.Split(':');
                if (parts.Length >= 2)
                {
                    var pathParts = parts[1].Split('/');
                    if (pathParts.Length >= 4 && pathParts[0] == "v3")
                    {
                        var org = pathParts[1];
                        var project = Uri.EscapeDataString(pathParts[2]);
                        var repo = pathParts[3].Split([' ', '\t'])[0];
                        if (repo.EndsWith(".git", StringComparison.Ordinal))
                        {
                            repo = repo[..^4];
                        }

                        return $"https://dev.azure.com/{org}/{project}/_git/{repo}";
                    }
                }
            }

            // GitHub HTTPS: https://github.com/{owner}/{repo}.git
            if (urlPart.Contains("github.com", StringComparison.Ordinal))
            {
                var cleaned = urlPart.TrimEnd('/');
                if (cleaned.EndsWith(".git", StringComparison.Ordinal))
                {
                    cleaned = cleaned[..^4];
                }

                try
                {
                    var uri = new Uri(cleaned);
                    var segments = uri.AbsolutePath.Trim('/').Split('/');
                    if (segments.Length >= 2)
                    {
                        return $"https://github.com/{segments[0]}/{segments[1]}";
                    }
                }
                catch
                {
                    // fall through
                }
            }

            // GitHub SSH: git@github.com:{owner}/{repo}.git
            if (urlPart.StartsWith("git@github.com:", StringComparison.Ordinal))
            {
                var path = urlPart["git@github.com:".Length..];
                if (path.EndsWith(".git", StringComparison.Ordinal))
                {
                    path = path[..^4];
                }

                var segments = path.Split('/');
                if (segments.Length >= 2)
                {
                    return $"https://github.com/{segments[0]}/{segments[1]}";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Parses the output of `git remote -v` to extract an ADO base URL.
    /// Kept for backward compatibility with tests.
    /// </summary>
    public static string? ParseAdoBaseUrl(string gitRemoteOutput, string fallbackRepoName) =>
        ParseRemoteUrl(gitRemoteOutput);

    /// <summary>
    /// Returns true if the base URL is a GitHub URL.
    /// </summary>
    public static bool IsGitHub(string baseUrl) => baseUrl.Contains("github.com", StringComparison.Ordinal);

    /// <summary>
    /// Builds a URL to a specific file and line range, supporting both ADO and GitHub.
    /// </summary>
    public static string BuildFileUrl(string baseUrl, string relativeFilePath, int startLine, int endLine, string defaultBranch = "main")
    {
        var normalizedPath = relativeFilePath.Replace('\\', '/');

        if (IsGitHub(baseUrl))
        {
            return $"{baseUrl}/blob/{defaultBranch}/{normalizedPath}#L{startLine}-L{endLine}";
        }

        // ADO format
        var encodedPath = Uri.EscapeDataString("/" + normalizedPath);
        return $"{baseUrl}?path={encodedPath}&line={startLine}&lineEnd={endLine}&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents";
    }

    /// <summary>
    /// Builds a URL to a file (without line numbers), supporting both ADO and GitHub.
    /// </summary>
    public static string BuildFileUrl(string baseUrl, string relativeFilePath, string defaultBranch = "main")
    {
        var normalizedPath = relativeFilePath.Replace('\\', '/');

        if (IsGitHub(baseUrl))
        {
            return $"{baseUrl}/blob/{defaultBranch}/{normalizedPath}";
        }

        var encodedPath = Uri.EscapeDataString("/" + normalizedPath);
        return $"{baseUrl}?path={encodedPath}&_a=contents";
    }
}
