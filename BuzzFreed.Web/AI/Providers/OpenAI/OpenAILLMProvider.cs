using Newtonsoft.Json.Linq;
using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;
using BuzzFreed.Web.AI.Registry;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.AI.Providers.OpenAI;

/// <summary>
/// OpenAI LLM Provider (GPT-4, GPT-3.5, etc.)
/// </summary>
public class OpenAILLMProvider(AIProviderRegistry registry) : ILLMProvider
{
    public readonly HttpClient HttpClient = HttpClientHelper.CreateClient();
    public readonly AIProviderConfig Config = registry.GetProviderConfig("openai") ?? new AIProviderConfig();
    public const string ApiEndpoint = "https://api.openai.com/v1/chat/completions";

    public string ProviderId => "openai";
    public string ProviderName => "OpenAI";
    public ProviderType Type => ProviderType.LLM;

    public List<string> SupportedModels => new()
    {
        "gpt-4o",
        "gpt-4o-mini",
        "gpt-4-turbo",
        "gpt-4",
        "gpt-3.5-turbo"
    };

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
        List<ChatMessage> messages = new();

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
            string model = request.Model ?? Config.DefaultModel ?? "gpt-4o-mini";

            object payload = new
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

            Logs.Debug($"Calling OpenAI API with model: {model}");
            HttpResult<string> result = await HttpHelper.PostJsonAsync(HttpClient, ApiEndpoint, payload, cancellationToken);

            if (!result.IsSuccess)
            {
                Logs.Error($"OpenAI API error: {result.Error}");
                return new LLMResponse
                {
                    Error = $"OpenAI API error: {result.Error}",
                    Provider = ProviderName
                };
            }

            JObject? responseObject = JsonHelper.ParseObject(result.Data);

            if (responseObject == null)
            {
                Logs.Error("Failed to parse OpenAI API response");
                return new LLMResponse
                {
                    Error = "Failed to parse API response",
                    Provider = ProviderName
                };
            }

            string messageContent = JsonHelper.GetString(responseObject, "choices[0].message.content");
            string finishReason = JsonHelper.GetString(responseObject, "choices[0].finish_reason");
            int tokensUsed = JsonHelper.GetInt(responseObject, "usage.total_tokens");

            return new LLMResponse
            {
                Text = messageContent.Trim(),
                Model = model,
                Provider = ProviderName,
                TokensUsed = tokensUsed,
                FinishReason = finishReason,
                IsFallback = false
            };
        }
        catch (Exception ex)
        {
            Logs.Error($"Error calling OpenAI API: {ex.Message}");
            return new LLMResponse
            {
                Error = ex.Message,
                Provider = ProviderName
            };
        }
    }
}
