using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.AI.Providers.SwarmUI;

/// <summary>
/// SwarmUI Image Provider (Stable Diffusion models via SwarmUI)
/// </summary>
public class SwarmUIImageProvider(AIProviderRegistry registry) : IImageProvider
{
    public readonly HttpClient HttpClient = InitializeHttpClient(registry);
    public readonly AIProviderConfig Config = registry.GetProviderConfig("swarmui") ?? new AIProviderConfig();
    public string? SessionId = null;

    public string ProviderId => "swarmui";
    public string ProviderName => "SwarmUI";
    public ProviderType Type => ProviderType.Image;

    public List<string> SupportedSizes => new()
    {
        "512x512",
        "768x768",
        "1024x1024",
        "1024x768",
        "768x1024",
        "1280x720",
        "720x1280",
        "1920x1080",
        "1080x1920"
    };

    public List<string> SupportedFormats => new() { "png", "jpg", "webp" };

    public static HttpClient InitializeHttpClient(AIProviderRegistry registry)
    {
        AIProviderConfig config = registry.GetProviderConfig("swarmui") ?? new AIProviderConfig();
        string baseUrl = config.BaseUrl ?? "http://127.0.0.1:7801";
        HttpClient client = new()
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };
        return client;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Try to get a session - if successful, SwarmUI is available
            await GetOrCreateSessionAsync();
            return !string.IsNullOrEmpty(SessionId);
        }
        catch
        {
            return false;
        }
    }

    public ProviderCapabilities GetCapabilities()
    {
        return new ProviderCapabilities
        {
            SupportsStreaming = true, // Via WebSocket
            SupportsImages = true,
            MaxImageSize = 2048,
            CustomCapabilities = new Dictionary<string, object>
            {
                { "supports_negative_prompt", true },
                { "supports_cfg_scale", true },
                { "supports_steps", true },
                { "supports_seed", true },
                { "supports_multiple_models", true }
            }
        };
    }

    public async Task<ImageResponse> GenerateImageAsync(ImageRequest request, CancellationToken cancellationToken = default)
    {
        return await GenerateImageWithProgressAsync(request, null, cancellationToken);
    }

    public async Task<ImageResponse> GenerateImageWithProgressAsync(
        ImageRequest request,
        Action<int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure we have a session
            await GetOrCreateSessionAsync();

            if (string.IsNullOrEmpty(SessionId))
            {
                return new ImageResponse
                {
                    Error = "Failed to create SwarmUI session",
                    Provider = ProviderName
                };
            }

            progressCallback?.Invoke(10, "Preparing image generation request...");

            string model = request.Model ?? Config.DefaultModel ?? "OfficialStableDiffusion/sd_xl_base_1.0";

            // Build SwarmUI API request
            object payload = new
            {
                session_id = SessionId,
                prompt = request.Prompt,
                negativeprompt = request.NegativePrompt ?? "",
                model = model,
                images = request.Count,
                width = request.Width,
                height = request.Height,
                steps = request.Steps ?? 20,
                cfgscale = request.GuidanceScale ?? 7.5,
                seed = request.Seed,
                donotsave = !request.SaveToServer
            };

            string payloadString = JsonConvert.SerializeObject(payload);
            StringContent httpContent = new(payloadString, Encoding.UTF8, "application/json");

            progressCallback?.Invoke(30, "Calling SwarmUI API...");
            Logs.Debug($"Calling SwarmUI with model: {model}");

            HttpResponseMessage response = await HttpClient.PostAsync("/API/GenerateText2Image", httpContent, cancellationToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logs.Error($"SwarmUI API error: {response.StatusCode} - {responseContent}");
                return new ImageResponse
                {
                    Error = $"SwarmUI API error: {response.StatusCode}",
                    Provider = ProviderName
                };
            }

            progressCallback?.Invoke(80, "Processing generated images...");

            JObject responseObject = JObject.Parse(responseContent);

            // Check for errors
            string? errorId = responseObject["error_id"]?.ToString();
            if (!string.IsNullOrEmpty(errorId))
            {
                Logs.Error($"SwarmUI error: {errorId}");

                // If session is invalid, clear it
                if (errorId == "invalid_session_id")
                {
                    SessionId = null;
                }

                return new ImageResponse
                {
                    Error = $"SwarmUI error: {errorId}",
                    Provider = ProviderName
                };
            }

            // Parse image results
            List<GeneratedImage> images = new();
            JArray? imagesArray = responseObject["images"] as JArray;

            if (imagesArray != null)
            {
                foreach (JToken item in imagesArray)
                {
                    string imagePath = item.ToString();
                    string? baseUrl = HttpClient.BaseAddress?.ToString().TrimEnd('/');
                    string imageUrl = $"{baseUrl}{imagePath}";

                    images.Add(new GeneratedImage
                    {
                        Url = imageUrl,
                        FilePath = imagePath,
                        Width = request.Width,
                        Height = request.Height,
                        Seed = request.Seed
                    });
                }
            }

            progressCallback?.Invoke(100, "Image generation complete!");

            return new ImageResponse
            {
                Images = images,
                Model = model,
                Provider = ProviderName,
                IsFallback = false
            };
        }
        catch (Exception ex)
        {
            Logs.Error($"Error calling SwarmUI API: {ex.Message}");
            return new ImageResponse
            {
                Error = ex.Message,
                Provider = ProviderName
            };
        }
    }

    /// <summary>
    /// Get or create a SwarmUI session
    /// </summary>
    public async Task GetOrCreateSessionAsync()
    {
        if (!string.IsNullOrEmpty(SessionId))
        {
            return;
        }

        try
        {
            StringContent content = new("{}", Encoding.UTF8, "application/json");
            HttpResponseMessage response = await HttpClient.PostAsync("/API/GetNewSession", content);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                JObject responseObject = JObject.Parse(responseContent);
                SessionId = responseObject["session_id"]?.ToString();
                Logs.Info($"SwarmUI session created: {SessionId}");
            }
            else
            {
                Logs.Error($"Failed to create SwarmUI session: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Logs.Error($"Error creating SwarmUI session: {ex.Message}");
        }
    }
}
