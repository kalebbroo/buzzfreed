using BuzzFreed.Web.AI.Models;

namespace BuzzFreed.Web.AI.Abstractions
{
    /// <summary>
    /// Interface for Large Language Model (LLM) providers
    /// </summary>
    public interface ILLMProvider : IAIProvider
    {
        /// <summary>
        /// Generate text completion from a prompt
        /// </summary>
        Task<LLMResponse> GenerateCompletionAsync(LLMRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate text completion with chat messages
        /// </summary>
        Task<LLMResponse> GenerateChatCompletionAsync(
            List<ChatMessage> messages,
            LLMRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Supported models for this provider
        /// </summary>
        List<string> SupportedModels { get; }
    }
}
