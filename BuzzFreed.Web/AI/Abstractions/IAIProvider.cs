namespace BuzzFreed.Web.AI.Abstractions
{
    /// <summary>
    /// Base interface for all AI providers
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>
        /// Unique identifier for the provider (e.g., "openai", "swarmui")
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Display name for the provider (e.g., "OpenAI", "SwarmUI")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Provider type (LLM, Image, Both)
        /// </summary>
        ProviderType Type { get; }

        /// <summary>
        /// Check if the provider is available and configured
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Get provider capabilities
        /// </summary>
        ProviderCapabilities GetCapabilities();
    }

    public enum ProviderType
    {
        LLM,
        Image,
        Both
    }
}
