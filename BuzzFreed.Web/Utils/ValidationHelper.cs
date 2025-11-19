using System.Text.RegularExpressions;

namespace BuzzFreed.Web.Utils;

/// <summary>
/// Helper class for common validation operations
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Checks if a string is null or empty
    /// </summary>
    public static bool IsNullOrEmpty(string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Checks if a string is null, empty, or whitespace
    /// </summary>
    public static bool IsNullOrWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Validates that a string is not null or empty, throws if invalid
    /// </summary>
    public static string RequireNotEmpty(string? value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null or empty", paramName);
        }

        return value;
    }

    /// <summary>
    /// Validates that an object is not null, throws if invalid
    /// </summary>
    public static T RequireNotNull<T>(T? value, string paramName) where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName, $"Parameter '{paramName}' cannot be null");
        }

        return value;
    }

    /// <summary>
    /// Validates that a value is within a range
    /// </summary>
    public static bool IsInRange(int value, int min, int max)
    {
        return value >= min && value <= max;
    }

    /// <summary>
    /// Validates that a value is within a range, throws if invalid
    /// </summary>
    public static int RequireInRange(int value, int min, int max, string paramName)
    {
        if (!IsInRange(value, min, max))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Parameter '{paramName}' must be between {min} and {max}");
        }

        return value;
    }

    /// <summary>
    /// Validates an email address format
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            // Basic email validation regex
            Regex emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            return emailRegex.IsMatch(email);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a URL format
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Validates a Discord User ID format (should be numeric and 17-19 digits)
    /// </summary>
    public static bool IsValidDiscordId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        Regex idRegex = new Regex(@"^\d{17,19}$");
        return idRegex.IsMatch(id);
    }

    /// <summary>
    /// Validates a quiz answer (A, B, C, or D)
    /// </summary>
    public static bool IsValidQuizAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        string upperAnswer = answer.ToUpper();
        return upperAnswer == "A" || upperAnswer == "B" || upperAnswer == "C" || upperAnswer == "D";
    }

    /// <summary>
    /// Validates that a collection is not null or empty
    /// </summary>
    public static bool IsNullOrEmpty<T>(ICollection<T>? collection)
    {
        return collection == null || collection.Count == 0;
    }

    /// <summary>
    /// Validates that a collection is not null or empty, throws if invalid
    /// </summary>
    public static ICollection<T> RequireNotEmpty<T>(ICollection<T>? collection, string paramName)
    {
        if (IsNullOrEmpty(collection))
        {
            throw new ArgumentException($"Collection '{paramName}' cannot be null or empty", paramName);
        }

        return collection!;
    }

    /// <summary>
    /// Validates a string length
    /// </summary>
    public static bool IsValidLength(string? value, int minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return minLength == 0;
        }

        return value.Length >= minLength && value.Length <= maxLength;
    }

    /// <summary>
    /// Validates a string length, throws if invalid
    /// </summary>
    public static string RequireValidLength(string? value, int minLength, int maxLength, string paramName)
    {
        if (!IsValidLength(value, minLength, maxLength))
        {
            throw new ArgumentException($"Parameter '{paramName}' must be between {minLength} and {maxLength} characters", paramName);
        }

        return value ?? string.Empty;
    }

    /// <summary>
    /// Sanitizes a string by removing potentially dangerous characters
    /// </summary>
    public static string SanitizeString(string? value, bool allowWhitespace = true)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Remove control characters
        string sanitized = Regex.Replace(value, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Optionally remove extra whitespace
        if (!allowWhitespace)
        {
            sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
        }

        return sanitized;
    }

    /// <summary>
    /// Validates a GUID format
    /// </summary>
    public static bool IsValidGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return false;
        }

        return Guid.TryParse(guid, out _);
    }
}
