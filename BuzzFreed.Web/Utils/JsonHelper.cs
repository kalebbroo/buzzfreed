using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BuzzFreed.Web.Utils;

/// <summary>
/// Helper class for JSON serialization and deserialization with consistent settings and error handling
/// </summary>
public static class JsonHelper
{
    public static readonly JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    public static readonly JsonSerializerSettings PrettySettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    /// <summary>
    /// Safely serializes an object to JSON string
    /// </summary>
    public static string Serialize(object obj, bool pretty = false)
    {
        try
        {
            JsonSerializerSettings settings = pretty ? PrettySettings : DefaultSettings;
            return JsonConvert.SerializeObject(obj, settings);
        }
        catch (Exception ex)
        {
            Logs.Error($"JSON serialization error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Safely deserializes a JSON string to an object
    /// </summary>
    public static T? Deserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json, DefaultSettings);
        }
        catch (Exception ex)
        {
            Logs.Error($"JSON deserialization error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Safely deserializes a JSON string to an object with a fallback value
    /// </summary>
    public static T DeserializeOrDefault<T>(string json, T defaultValue) where T : class
    {
        T? result = Deserialize<T>(json);
        return result ?? defaultValue;
    }

    /// <summary>
    /// Parses a JSON string to JObject safely
    /// </summary>
    public static JObject? ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JObject.Parse(json);
        }
        catch (Exception ex)
        {
            Logs.Error($"JSON parse error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a JSON string to JArray safely
    /// </summary>
    public static JArray? ParseArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JArray.Parse(json);
        }
        catch (Exception ex)
        {
            Logs.Error($"JSON parse error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validates if a string is valid JSON
    /// </summary>
    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            JToken.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to get a value from a JObject safely
    /// </summary>
    public static T? GetValue<T>(JObject obj, string path, T? defaultValue = default)
    {
        try
        {
            JToken? token = obj.SelectToken(path);
            return token != null ? token.Value<T>() : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Tries to get a string value from a JObject safely
    /// </summary>
    public static string GetString(JObject obj, string path, string defaultValue = "")
    {
        return GetValue(obj, path, defaultValue) ?? defaultValue;
    }

    /// <summary>
    /// Tries to get an integer value from a JObject safely
    /// </summary>
    public static int GetInt(JObject obj, string path, int defaultValue = 0)
    {
        return GetValue(obj, path, defaultValue);
    }

    /// <summary>
    /// Tries to get a boolean value from a JObject safely
    /// </summary>
    public static bool GetBool(JObject obj, string path, bool defaultValue = false)
    {
        return GetValue(obj, path, defaultValue);
    }
}
