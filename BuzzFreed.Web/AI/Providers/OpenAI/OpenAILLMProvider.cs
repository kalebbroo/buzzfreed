using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.AI.Registry;

namespace BuzzFreed.Web.AI.Providers.OpenAI
{
    /// <summary>
    /// OpenAI LLM Provider (GPT-4, GPT-3.5, etc.)
    /// </summary>
    public class OpenAILLMProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAILLMProvider> _logger;
        private readonly AIProviderConfig _config;
        private const string ApiEndpoint = "https://api.openai.com/v1/chat/completions";

        public string ProviderId => "openai";
        public string ProviderName => "OpenAI";
        public ProviderType Type => ProviderType.LLM;

        public List<string> SupportedModels => new List<string>
        {
            "gpt-4o",
            "gpt-4o-mini",
            "gpt-4-turbo",
            "gpt-4",
            "gpt-3.5-turbo"
        };

        public OpenAILLMProvider(
            ILogger<OpenAILLMProvider> logger,
            AIProviderRegistry registry)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _config = registry.GetProviderConfig(ProviderId) ?? new AIProviderConfig();

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
                SupportsStreaming = true,
                SupportsChat = true,
                SupportsImages = false,
                SupportsVision = true,
                SupportsFunctionCalling = true,
                MaxTokens = 128000, // gpt-4o context window
                SupportedLanguages = new List<string> { "en", "es", "fr", "de", "it", "pt", "ja", "ko", "zh" }
            };
        }

        public async Task<LLMResponse> GenerateCompletionAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>();

            if (!string.IsNullOrEmpty(request.SystemMessage))
            {
                messages.Add(ChatMessage.System(request.SystemMessage));
            }

            messages.Add(ChatMessage.User(request.Prompt));

            return await GenerateChatCompletionAsync(messages, request, cancellationToken);
        }

        public async Task<LLMResponse> GenerateChatCompletionAsync(
            List<ChatMessage> messages,
            LLMRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var model = request.Model ?? _config.DefaultModel ?? "gpt-4o-mini";

                var payload = new
                {
                    model = model,
                    messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                    max_tokens = request.MaxTokens,
                    temperature = request.Temperature,
                    top_p = request.TopP,
                    frequency_penalty = request.FrequencyPenalty,
                    presence_penalty = request.PresencePenalty,
                    stop = request.StopSequences
                };

                var payloadString = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                var httpContent = new StringContent(payloadString, Encoding.UTF8, "application/json");

                _logger.LogDebug($"Calling OpenAI API with model: {model}");
                var response = await _httpClient.PostAsync(ApiEndpoint, httpContent, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"OpenAI API error: {response.StatusCode} - {responseContent}");
                    return new LLMResponse
                    {
                        Error = $"OpenAI API error: {response.StatusCode}",
                        Provider = ProviderName
                    };
                }

                var responseObject = JObject.Parse(responseContent);
                var messageContent = responseObject["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
                var finishReason = responseObject["choices"]?[0]?["finish_reason"]?.ToString();
                var usage = responseObject["usage"];

                return new LLMResponse
                {
                    Text = messageContent ?? string.Empty,
                    Model = model,
                    Provider = ProviderName,
                    TokensUsed = usage?["total_tokens"]?.Value<int>() ?? 0,
                    FinishReason = finishReason,
                    IsFallback = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return new LLMResponse
                {
                    Error = ex.Message,
                    Provider = ProviderName
                };
            }
        }
    }
}
