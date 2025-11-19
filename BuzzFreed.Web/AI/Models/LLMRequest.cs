namespace BuzzFreed.Web.AI.Models
{
    /// <summary>
    /// Request parameters for LLM text generation
    /// </summary>
    public class LLMRequest
    {
        /// <summary>
        /// The text prompt to generate from
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Model to use (e.g., "gpt-4o-mini", "gpt-3.5-turbo")
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Maximum number of tokens to generate
        /// </summary>
        public int MaxTokens { get; set; } = 500;

        /// <summary>
        /// Temperature for randomness (0.0 to 2.0)
        /// </summary>
        public double Temperature { get; set; } = 0.8;

        /// <summary>
        /// Top P sampling parameter
        /// </summary>
        public double? TopP { get; set; }

        /// <summary>
        /// Frequency penalty (-2.0 to 2.0)
        /// </summary>
        public double? FrequencyPenalty { get; set; }

        /// <summary>
        /// Presence penalty (-2.0 to 2.0)
        /// </summary>
        public double? PresencePenalty { get; set; }

        /// <summary>
        /// Stop sequences
        /// </summary>
        public List<string>? StopSequences { get; set; }

        /// <summary>
        /// System message for chat completions
        /// </summary>
        public string? SystemMessage { get; set; }

        /// <summary>
        /// Additional provider-specific parameters
        /// </summary>
        public Dictionary<string, object>? CustomParameters { get; set; }
    }
}
