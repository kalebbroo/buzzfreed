using Newtonsoft.Json.Linq;
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.AI.Providers.OpenAI;

/// <summary>
/// OpenAI Image Provider (DALL-E 2, DALL-E 3)
/// </summary>
public class OpenAIImageProvider(AIProviderRegistry registry) : IImageProvider
{
    public readonly HttpClient HttpClient = HttpClientHelper.CreateClient();
    public readonly AIProviderConfig Config = registry.GetProviderConfig("openai-image") ?? registry.GetProviderConfig("openai") ?? new AIProviderConfig();
    public const string ApiEndpoint = "https://api.openai.com/v1/images/generations";

    public string ProviderId => "openai-image";
    public string ProviderName => "OpenAI DALL-E";
    public ProviderType Type => ProviderType.Image;

    public List<string> SupportedSizes => new()
    {
        "1024x1024",
        "1792x1024",
        "1024x1792",
        "256x256",
        "512x512" // DALL-E 2
    };

    public List<string> SupportedFormats => new() { "png", "url" };

    public Task<bool> IsAvailableAsync()
    {
        bool isAvailable = !ValidationHelper.IsNullOrEmpty(Config.ApiKey) && Config.Enabled;
        if (isAvailable)
        {
            HttpClientHelper.AddAuthorizationHeader(HttpClient, Config.ApiKey!);
            HttpClientHelper.SetTimeout(HttpClient, Config.TimeoutSeconds);
        }
        return Task.FromResult(isAvailable);
    }

    public ProviderCapabilities GetCapabilities()
    {
        return new ProviderCapabilities
        {
            SupportsStreaming = false,
            SupportsImages = true,
            MaxImageSize = 1792,
            CustomCapabilities = new Dictionary<string, object>
            {
                { "supports_hd_quality", true },
                { "supports_styles", true }, // vivid, natural
                { "max_images_per_request", 10 }
            }
        };
    }

    public List<AIModel> GetAvailableModels()
    {
        List<AIModel> models = new List<AIModel>();
        bool isAvailable = Config.Enabled && !ValidationHelper.IsNullOrEmpty(Config.ApiKey);
        int basePriority = Config.Priority;

        models.Add(new AIModel
        {
            ModelId = "dall-e-3",
            DisplayName = "DALL-E 3",
            ProviderId = ProviderId,
            ProviderName = ProviderName,
            Type = ModelType.Image,
            IsAvailable = isAvailable,
            Priority = basePriority + 100,
            Capabilities = new ModelCapabilities
            {
                MaxImageSize = 1792,
                SupportedFormats = new List<string> { "png", "url" }
            },
            Pricing = new ModelPricing { ImageCostPerGeneration = 0.04m }
        });

        models.Add(new AIModel
        {
            ModelId = "dall-e-2",
            DisplayName = "DALL-E 2",
            ProviderId = ProviderId,
            ProviderName = ProviderName,
            Type = ModelType.Image,
            IsAvailable = isAvailable,
            Priority = basePriority + 80,
            Capabilities = new ModelCapabilities
            {
                MaxImageSize = 1024,
                SupportedFormats = new List<string> { "png", "url" }
            },
            Pricing = new ModelPricing { ImageCostPerGeneration = 0.02m }
        });

        return models;
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
            string model = request.Model ?? Config.DefaultModel ?? "dall-e-3";
            string size = request.Size ?? $"{request.Width}x{request.Height}";

            progressCallback?.Invoke(10, "Preparing image generation request...");

            object payload = new
            {
                model = model,
                prompt = request.Prompt,
                n = Math.Min(request.Count, model == "dall-e-3" ? 1 : 10), // DALL-E 3 only supports n=1
                size = size,
                quality = request.Quality ?? "standard",
                style = request.Style ?? "vivid",
                response_format = "url" // or "b64_json"
            };

            progressCallback?.Invoke(30, "Calling OpenAI DALL-E API...");
            Logs.Debug($"Calling OpenAI DALL-E with model: {model}");

            HttpResult<string> result = await HttpHelper.PostJsonAsync(HttpClient, ApiEndpoint, payload, cancellationToken);

            if (!result.IsSuccess)
            {
                Logs.Error($"OpenAI DALL-E API error: {result.Error}");
                return new ImageResponse
                {
                    Error = $"OpenAI DALL-E API error: {result.Error}",
                    Provider = ProviderName
                };
            }

            progressCallback?.Invoke(80, "Processing generated images...");

            JObject? responseObject = JsonHelper.ParseObject(result.Data);

            if (responseObject == null)
            {
                Logs.Error("Failed to parse OpenAI DALL-E API response");
                return new ImageResponse
                {
                    Error = "Failed to parse API response",
                    Provider = ProviderName
                };
            }

            JArray? dataArray = responseObject["data"] as JArray;

            List<GeneratedImage> images = new();
            if (dataArray != null)
            {
                foreach (JToken item in dataArray)
                {
                    string? imageUrl = item["url"]?.ToString();
                    string? revisedPrompt = item["revised_prompt"]?.ToString();

                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        images.Add(new GeneratedImage
                        {
                            Url = imageUrl,
                            Width = request.Width,
                            Height = request.Height,
                            RevisedPrompt = revisedPrompt
                        });
                    }
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
            Logs.Error($"Error calling OpenAI DALL-E API: {ex.Message}");
            return new ImageResponse
            {
                Error = ex.Message,
                Provider = ProviderName
            };
        }
    }
}
