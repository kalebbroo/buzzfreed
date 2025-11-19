using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.AI.Registry;

namespace BuzzFreed.Web.AI.Providers.OpenAI
{
    /// <summary>
    /// OpenAI Image Provider (DALL-E 2, DALL-E 3)
    /// </summary>
    public class OpenAIImageProvider : IImageProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAIImageProvider> _logger;
        private readonly AIProviderConfig _config;
        private const string ApiEndpoint = "https://api.openai.com/v1/images/generations";

        public string ProviderId => "openai-image";
        public string ProviderName => "OpenAI DALL-E";
        public ProviderType Type => ProviderType.Image;

        public List<string> SupportedSizes => new List<string>
        {
            "1024x1024",
            "1792x1024",
            "1024x1792",
            "256x256",
            "512x512" // DALL-E 2
        };

        public List<string> SupportedFormats => new List<string> { "png", "url" };

        public OpenAIImageProvider(
            ILogger<OpenAIImageProvider> logger,
            AIProviderRegistry registry)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _config = registry.GetProviderConfig(ProviderId) ?? registry.GetProviderConfig("openai") ?? new AIProviderConfig();

            // Setup HTTP client
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        }

        public async Task<bool> IsAvailableAsync()
        {
            return !string.IsNullOrEmpty(_config.ApiKey) && _config.Enabled;
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
                var model = request.Model ?? _config.DefaultModel ?? "dall-e-3";
                var size = request.Size ?? $"{request.Width}x{request.Height}";

                progressCallback?.Invoke(10, "Preparing image generation request...");

                var payload = new
                {
                    model = model,
                    prompt = request.Prompt,
                    n = Math.Min(request.Count, model == "dall-e-3" ? 1 : 10), // DALL-E 3 only supports n=1
                    size = size,
                    quality = request.Quality ?? "standard",
                    style = request.Style ?? "vivid",
                    response_format = "url" // or "b64_json"
                };

                var payloadString = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                var httpContent = new StringContent(payloadString, Encoding.UTF8, "application/json");

                progressCallback?.Invoke(30, "Calling OpenAI DALL-E API...");
                _logger.LogDebug($"Calling OpenAI DALL-E with model: {model}");

                var response = await _httpClient.PostAsync(ApiEndpoint, httpContent, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"OpenAI DALL-E API error: {response.StatusCode} - {responseContent}");
                    return new ImageResponse
                    {
                        Error = $"OpenAI DALL-E API error: {response.StatusCode}",
                        Provider = ProviderName
                    };
                }

                progressCallback?.Invoke(80, "Processing generated images...");

                var responseObject = JObject.Parse(responseContent);
                var dataArray = responseObject["data"] as JArray;

                var images = new List<GeneratedImage>();
                if (dataArray != null)
                {
                    foreach (var item in dataArray)
                    {
                        var imageUrl = item["url"]?.ToString();
                        var revisedPrompt = item["revised_prompt"]?.ToString();

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
                _logger.LogError(ex, "Error calling OpenAI DALL-E API");
                return new ImageResponse
                {
                    Error = ex.Message,
                    Provider = ProviderName
                };
            }
        }
    }
}
