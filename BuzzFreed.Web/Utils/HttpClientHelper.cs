using System.Net.Http.Headers;

namespace BuzzFreed.Web.Utils;

/// <summary>
/// Helper class for creating and configuring HttpClient instances
/// </summary>
public static class HttpClientHelper
{
    /// <summary>
    /// Creates a new HttpClient with standard configuration
    /// </summary>
    public static HttpClient CreateClient(int timeoutSeconds = 30)
    {
        HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("BuzzFreed/1.0");

        return client;
    }

    /// <summary>
    /// Creates an HttpClient with Bearer token authentication
    /// </summary>
    public static HttpClient CreateClientWithBearerToken(string token, int timeoutSeconds = 30)
    {
        HttpClient client = CreateClient(timeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Creates an HttpClient with custom base URL and optional auth token
    /// </summary>
    public static HttpClient CreateClientWithBaseUrl(string baseUrl, string? authToken = null, int timeoutSeconds = 30)
    {
        HttpClient client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("BuzzFreed/1.0");

        if (!string.IsNullOrEmpty(authToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
        }

        return client;
    }

    /// <summary>
    /// Adds an authorization header to an existing HttpClient if not already present
    /// </summary>
    public static void AddAuthorizationHeader(HttpClient client, string token, string scheme = "Bearer")
    {
        if (!client.DefaultRequestHeaders.Contains("Authorization"))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, token);
        }
    }

    /// <summary>
    /// Adds a custom header to an HttpClient if not already present
    /// </summary>
    public static void AddHeader(HttpClient client, string name, string value)
    {
        if (!client.DefaultRequestHeaders.Contains(name))
        {
            client.DefaultRequestHeaders.Add(name, value);
        }
    }

    /// <summary>
    /// Sets the timeout for an HttpClient
    /// </summary>
    public static void SetTimeout(HttpClient client, int timeoutSeconds)
    {
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }
}
