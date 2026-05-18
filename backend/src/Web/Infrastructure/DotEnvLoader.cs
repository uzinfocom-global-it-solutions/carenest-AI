namespace Backend.Web.Infrastructure;

/// <summary>
/// Minimal `.env` loader so secrets can sit in a per-developer file alongside
/// the project instead of in source. Loaded BEFORE the host builder so the
/// standard <c>AddEnvironmentVariables()</c> source picks the values up and
/// they override anything in <c>appsettings.json</c>.
///
/// Syntax supported:
/// <list type="bullet">
///   <item><description><c>KEY=value</c></description></item>
///   <item><description>Lines starting with <c>#</c> are comments</description></item>
///   <item><description>Surrounding double or single quotes are stripped</description></item>
///   <item><description>Existing process env vars are not overwritten</description></item>
/// </list>
///
/// Use the .NET configuration convention for nested keys: <c>LLM__Token=...</c>
/// becomes the <c>LLM:Token</c> setting.
/// </summary>
internal static class DotEnvLoader
{
    public static void Load(params string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (!File.Exists(path)) continue;
            LoadFile(path);
            return;
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Don't clobber values the host already provided (real env, k8s, etc).
            if (Environment.GetEnvironmentVariable(key) is not null) continue;
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
