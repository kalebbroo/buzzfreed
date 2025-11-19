namespace BuzzFreed.Web.AI.Models
{
    /// <summary>
    /// Response from LLM text generation
    /// </summary>
    public class LLMResponse
    {
        /// <summary>
        /// Generated text
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Model used for generation
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Provider that generated the response
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Number of tokens used
        /// </summary>
        public int TokensUsed { get; set; }

        /// <summary>
        /// Finish reason (stop, length, content_filter, etc.)
        /// </summary>
        public string? FinishReason { get; set; }

        /// <summary>
        /// Was this from a cached/fallback provider
        /// </summary>
        public bool IsFallback { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Error message if generation failed
        /// </summary>
        public string? Error { get; set; }

        public bool IsSuccess => string.IsNullOrEmpty(Error);
    }
}
