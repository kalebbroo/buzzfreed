namespace BuzzFreed.Web.Utils;

/// <summary>
/// Helper class for reading configuration values with environment variable expansion and defaults
/// </summary>
public static class ConfigHelper
{
    /// <summary>
    /// Gets a configuration value with environment variable expansion
    /// Supports ${ENV_VAR} and ${ENV_VAR:-default} syntax
    /// </summary>
    public static string GetValue(IConfiguration configuration, string key, string defaultValue = "")
    {
        string? value = configuration[key];

        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        return ExpandEnvironmentVariables(value, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as integer
    /// </summary>
    public static int GetInt(IConfiguration configuration, string key, int defaultValue = 0)
    {
        string value = GetValue(configuration, key, defaultValue.ToString());

        if (int.TryParse(value, out int result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets a configuration value as boolean
    /// </summary>
    public static bool GetBool(IConfiguration configuration, string key, bool defaultValue = false)
    {
        string value = GetValue(configuration, key, defaultValue.ToString());

        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets a required configuration value, throws if not found
    /// </summary>
    public static string GetRequiredValue(IConfiguration configuration, string key)
    {
        string value = GetValue(configuration, key, string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required configuration key '{key}' is missing or empty");
        }

        return value;
    }

    /// <summary>
    /// Expands environment variables in a string
    /// Supports ${ENV_VAR} and ${ENV_VAR:-default} syntax
    /// </summary>
    public static string ExpandEnvironmentVariables(string value, string defaultIfEmpty = "")
    {
        if (string.IsNullOrEmpty(value))
        {
            return defaultIfEmpty;
        }

        // Handle ${VAR:-default} syntax
        int startIndex = 0;
        while (true)
        {
            int dollarIndex = value.IndexOf("${", startIndex);
            if (dollarIndex == -1)
            {
                break;
            }

            int endIndex = value.IndexOf("}", dollarIndex);
            if (endIndex == -1)
            {
                break;
            }

            string varExpression = value.Substring(dollarIndex + 2, endIndex - dollarIndex - 2);
            string varName = varExpression;
            string varDefault = "";

            // Check for :-default syntax
            int defaultIndex = varExpression.IndexOf(":-");
            if (defaultIndex != -1)
            {
                varName = varExpression.Substring(0, defaultIndex);
                varDefault = varExpression.Substring(defaultIndex + 2);
            }

            string? envValue = Environment.GetEnvironmentVariable(varName);
            string replacement = !string.IsNullOrEmpty(envValue) ? envValue : varDefault;

            value = value.Substring(0, dollarIndex) + replacement + value.Substring(endIndex + 1);
            startIndex = dollarIndex + replacement.Length;
        }

        return value;
    }

    /// <summary>
    /// Gets a configuration section as a dictionary
    /// </summary>
    public static Dictionary<string, string> GetSection(IConfiguration configuration, string sectionKey)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        IConfigurationSection? section = configuration.GetSection(sectionKey);

        if (section == null)
        {
            return result;
        }

        foreach (IConfigurationSection child in section.GetChildren())
        {
            string? value = child.Value;
            if (value != null)
            {
                result[child.Key] = ExpandEnvironmentVariables(value);
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if a configuration key exists and has a non-empty value
    /// </summary>
    public static bool HasValue(IConfiguration configuration, string key)
    {
        string value = GetValue(configuration, key);
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Gets an environment variable with optional default
    /// </summary>
    public static string GetEnvironmentVariable(string name, string defaultValue = "")
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrEmpty(value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets a required environment variable, throws if not found
    /// </summary>
    public static string GetRequiredEnvironmentVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable '{name}' is missing or empty");
        }

        return value;
    }
}
