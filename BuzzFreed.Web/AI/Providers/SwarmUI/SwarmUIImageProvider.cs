using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.AI.Registry;

namespace BuzzFreed.Web.AI.Providers.SwarmUI
{
    /// <summary>
    /// SwarmUI Image Provider (Stable Diffusion models via SwarmUI)
    /// </summary>
    public class SwarmUIImageProvider : IImageProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SwarmUIImageProvider> _logger;
        private readonly AIProviderConfig _config;
        private string? _sessionId;

        public string ProviderId => "swarmui";
        public string ProviderName => "SwarmUI";
        public ProviderType Type => ProviderType.Image;

        public List<string> SupportedSizes => new List<string>
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

        public List<string> SupportedFormats => new List<string> { "png", "jpg", "webp" };

        public SwarmUIImageProvider(
            ILogger<SwarmUIImageProvider> logger,
            AIProviderRegistry registry)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _config = registry.GetProviderConfig(ProviderId) ?? new AIProviderConfig();

            // Setup HTTP client
            var baseUrl = _config.BaseUrl ?? "http://127.0.0.1:7801";
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Try to get a session - if successful, SwarmUI is available
                await GetOrCreateSessionAsync();
                return !string.IsNullOrEmpty(_sessionId);
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

                if (string.IsNullOrEmpty(_sessionId))
                {
                    return new ImageResponse
                    {
                        Error = "Failed to create SwarmUI session",
                        Provider = ProviderName
                    };
                }

                progressCallback?.Invoke(10, "Preparing image generation request...");

                var model = request.Model ?? _config.DefaultModel ?? "OfficialStableDiffusion/sd_xl_base_1.0";

                // Build SwarmUI API request
                var payload = new
                {
                    session_id = _sessionId,
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

                var payloadString = JsonConvert.SerializeObject(payload);
                var httpContent = new StringContent(payloadString, Encoding.UTF8, "application/json");

                progressCallback?.Invoke(30, "Calling SwarmUI API...");
                _logger.LogDebug($"Calling SwarmUI with model: {model}");

                var response = await _httpClient.PostAsync("/API/GenerateText2Image", httpContent, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"SwarmUI API error: {response.StatusCode} - {responseContent}");
                    return new ImageResponse
                    {
                        Error = $"SwarmUI API error: {response.StatusCode}",
                        Provider = ProviderName
                    };
                }

                progressCallback?.Invoke(80, "Processing generated images...");

                var responseObject = JObject.Parse(responseContent);

                // Check for errors
                var errorId = responseObject["error_id"]?.ToString();
                if (!string.IsNullOrEmpty(errorId))
                {
                    _logger.LogError($"SwarmUI error: {errorId}");

                    // If session is invalid, clear it
                    if (errorId == "invalid_session_id")
                    {
                        _sessionId = null;
                    }

                    return new ImageResponse
                    {
                        Error = $"SwarmUI error: {errorId}",
                        Provider = ProviderName
                    };
                }

                // Parse image results
                var images = new List<GeneratedImage>();
                var imagesArray = responseObject["images"] as JArray;

                if (imagesArray != null)
                {
                    foreach (var item in imagesArray)
                    {
                        var imagePath = item.ToString();
                        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/');
                        var imageUrl = $"{baseUrl}{imagePath}";

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
                _logger.LogError(ex, "Error calling SwarmUI API");
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
        private async Task GetOrCreateSessionAsync()
        {
            if (!string.IsNullOrEmpty(_sessionId))
            {
                return;
            }

            try
            {
                var content = new StringContent("{}", Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/API/GetNewSession", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseObject = JObject.Parse(responseContent);
                    _sessionId = responseObject["session_id"]?.ToString();
                    _logger.LogInformation($"SwarmUI session created: {_sessionId}");
                }
                else
                {
                    _logger.LogError($"Failed to create SwarmUI session: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating SwarmUI session");
            }
        }
    }
}
