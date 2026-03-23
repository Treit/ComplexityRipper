using System.Diagnostics;

namespace ComplexityRipper.Utilities;

/// <summary>
/// Parses git remote URLs to extract ADO organization, project, and repo name.
/// Builds ADO file URLs with line number ranges.
/// </summary>
public static class AdoUrlHelper
{
    /// <summary>
    /// Reads git remote -v from a repo directory and extracts the ADO base URL.
    /// Returns null if the remote is not an ADO URL or if git fails.
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

            return ParseAdoBaseUrl(output, Path.GetFileName(repoPath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the output of `git remote -v` to extract an ADO base URL.
    /// Supports both HTTPS and SSH remote formats.
    /// </summary>
    public static string? ParseAdoBaseUrl(string gitRemoteOutput, string fallbackRepoName)
    {
        foreach (var line in gitRemoteOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();

            // HTTPS format: https://msdata@dev.azure.com/msdata/Sentinel%20Graph/_git/NEXT
            // or: https://dev.azure.com/msdata/Sentinel Graph/_git/NEXT
            if (trimmed.Contains("dev.azure.com"))
            {
                var urlPart = trimmed.Split('\t', ' ')[1];
                if (urlPart.Contains("/_git/"))
                {
                    // Extract org and project from URL
                    var uri = new Uri(urlPart.Replace(" ", "%20"));
                    var segments = uri.AbsolutePath.Trim('/').Split('/');

                    // Path: /{org}/{project}/_git/{repo}
                    if (segments.Length >= 4 && segments[^2] == "_git")
                    {
                        var org = segments[0];
                        var project = Uri.EscapeDataString(Uri.UnescapeDataString(
                            string.Join("/", segments[1..^2])));
                        var repo = segments[^1];

                        return $"https://dev.azure.com/{org}/{project}/_git/{repo}";
                    }
                }
            }

            // SSH format: git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
            if (trimmed.Contains("ssh.dev.azure.com"))
            {
                var parts = trimmed.Split(':');
                if (parts.Length >= 2)
                {
                    var pathParts = parts[1].Split('/');
                    if (pathParts.Length >= 4 && pathParts[0] == "v3")
                    {
                        var org = pathParts[1];
                        var project = Uri.EscapeDataString(pathParts[2]);
                        var repo = pathParts[3].Split(new[] { ' ', '\t' })[0];
                        if (repo.EndsWith(".git"))
                        {
                            repo = repo[..^4];
                        }

                        return $"https://dev.azure.com/{org}/{project}/_git/{repo}";
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a full ADO URL to a specific file and line range.
    /// </summary>
    public static string BuildFileUrl(string adoBaseUrl, string relativeFilePath, int startLine, int endLine)
    {
        var normalizedPath = "/" + relativeFilePath.Replace('\\', '/');
        var encodedPath = Uri.EscapeDataString(normalizedPath);
        return $"{adoBaseUrl}?path={encodedPath}&line={startLine}&lineEnd={endLine}&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents";
    }
}
