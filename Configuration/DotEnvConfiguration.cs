// Smart Solar Microgrid Trading System - local .env configuration loader.
namespace SolarGridX.Api.Configuration;

/// <summary>
/// Loads development-only settings from a .env file before ASP.NET Core builds its configuration.
/// </summary>
public static class DotEnvConfiguration
{
    /// <summary>
    /// Adds unset environment variables from the project's .env file.
    /// </summary>
    public static void Load(string? directory = null)
    {
        var filePath = ResolveEnvFilePath(directory);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            // Environment variables set by OS or deployment host take precedence over .env
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            // Also ensure the colon-delimited version is populated if using double underscores
            if (key.Contains("__"))
            {
                var colonKey = key.Replace("__", ":");
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(colonKey)))
                {
                    Environment.SetEnvironmentVariable(colonKey, value);
                }
            }
        }
    }

    private static string? ResolveEnvFilePath(string? directory)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(directory))
        {
            candidates.Add(directory);
        }

        candidates.Add(Directory.GetCurrentDirectory());
        candidates.Add(AppContext.BaseDirectory);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                var filePath = Path.Combine(current.FullName, ".env");
                if (File.Exists(filePath))
                {
                    return filePath;
                }

                current = current.Parent;
            }
        }

        return null;
    }
}