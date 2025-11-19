using System.Text;

namespace BuzzFreed.Web.Utils;

/// <summary>
/// Helper class for making HTTP requests with consistent error handling
/// </summary>
public static class HttpHelper
{
    /// <summary>
    /// Makes a GET request and returns the response as a string
    /// </summary>
    public static async Task<HttpResult<string>> GetStringAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logs.Error($"HTTP GET failed: {response.StatusCode} - {url}");
                return HttpResult<string>.Failure($"HTTP {response.StatusCode}", (int)response.StatusCode);
            }

            return HttpResult<string>.Success(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logs.Error($"HTTP GET exception: {ex.Message} - {url}");
            return HttpResult<string>.Failure(ex.Message, 0);
        }
    }

    /// <summary>
    /// Makes a GET request and deserializes the JSON response
    /// </summary>
    public static async Task<HttpResult<T>> GetJsonAsync<T>(
        HttpClient client,
        string url,
        CancellationToken cancellationToken = default) where T : class
    {
        HttpResult<string> stringResult = await GetStringAsync(client, url, cancellationToken);

        if (!stringResult.IsSuccess)
        {
            return HttpResult<T>.Failure(stringResult.Error, stringResult.StatusCode);
        }

        T? data = JsonHelper.Deserialize<T>(stringResult.Data);

        if (data == null)
        {
            return HttpResult<T>.Failure("Failed to deserialize response", stringResult.StatusCode);
        }

        return HttpResult<T>.Success(data, stringResult.StatusCode);
    }

    /// <summary>
    /// Makes a POST request with JSON body
    /// </summary>
    public static async Task<HttpResult<string>> PostJsonAsync(
        HttpClient client,
        string url,
        object payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string jsonPayload = JsonHelper.Serialize(payload);
            StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(url, content, cancellationToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logs.Error($"HTTP POST failed: {response.StatusCode} - {url}");
                return HttpResult<string>.Failure($"HTTP {response.StatusCode}", (int)response.StatusCode, responseContent);
            }

            return HttpResult<string>.Success(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logs.Error($"HTTP POST exception: {ex.Message} - {url}");
            return HttpResult<string>.Failure(ex.Message, 0);
        }
    }

    /// <summary>
    /// Makes a POST request with JSON body and deserializes the response
    /// </summary>
    public static async Task<HttpResult<T>> PostJsonAsync<T>(
        HttpClient client,
        string url,
        object payload,
        CancellationToken cancellationToken = default) where T : class
    {
        HttpResult<string> stringResult = await PostJsonAsync(client, url, payload, cancellationToken);

        if (!stringResult.IsSuccess)
        {
            return HttpResult<T>.Failure(stringResult.Error, stringResult.StatusCode, stringResult.RawResponse);
        }

        T? data = JsonHelper.Deserialize<T>(stringResult.Data);

        if (data == null)
        {
            return HttpResult<T>.Failure("Failed to deserialize response", stringResult.StatusCode, stringResult.Data);
        }

        return HttpResult<T>.Success(data, stringResult.StatusCode);
    }

    /// <summary>
    /// Makes a POST request with form URL encoded content
    /// </summary>
    public static async Task<HttpResult<string>> PostFormAsync(
        HttpClient client,
        string url,
        Dictionary<string, string> formData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            FormUrlEncodedContent content = new FormUrlEncodedContent(formData);

            HttpResponseMessage response = await client.PostAsync(url, content, cancellationToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logs.Error($"HTTP POST (form) failed: {response.StatusCode} - {url}");
                return HttpResult<string>.Failure($"HTTP {response.StatusCode}", (int)response.StatusCode, responseContent);
            }

            return HttpResult<string>.Success(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logs.Error($"HTTP POST (form) exception: {ex.Message} - {url}");
            return HttpResult<string>.Failure(ex.Message, 0);
        }
    }

    /// <summary>
    /// Makes a POST request with form URL encoded content and deserializes the response
    /// </summary>
    public static async Task<HttpResult<T>> PostFormAsync<T>(
        HttpClient client,
        string url,
        Dictionary<string, string> formData,
        CancellationToken cancellationToken = default) where T : class
    {
        HttpResult<string> stringResult = await PostFormAsync(client, url, formData, cancellationToken);

        if (!stringResult.IsSuccess)
        {
            return HttpResult<T>.Failure(stringResult.Error, stringResult.StatusCode, stringResult.RawResponse);
        }

        T? data = JsonHelper.Deserialize<T>(stringResult.Data);

        if (data == null)
        {
            return HttpResult<T>.Failure("Failed to deserialize response", stringResult.StatusCode, stringResult.Data);
        }

        return HttpResult<T>.Success(data, stringResult.StatusCode);
    }
}

/// <summary>
/// Represents the result of an HTTP request
/// </summary>
public class HttpResult<T>
{
    public bool IsSuccess { get; set; }
    public T Data { get; set; } = default!;
    public string Error { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string RawResponse { get; set; } = string.Empty;

    public static HttpResult<T> Success(T data, int statusCode)
    {
        return new HttpResult<T>
        {
            IsSuccess = true,
            Data = data,
            StatusCode = statusCode,
            RawResponse = data is string str ? str : string.Empty
        };
    }

    public static HttpResult<T> Failure(string error, int statusCode, string rawResponse = "")
    {
        return new HttpResult<T>
        {
            IsSuccess = false,
            Error = error,
            StatusCode = statusCode,
            RawResponse = rawResponse
        };
    }
}
