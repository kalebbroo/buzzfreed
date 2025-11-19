namespace BuzzFreed.Web.AI.Models
{
    /// <summary>
    /// Response from image generation
    /// </summary>
    public class ImageResponse
    {
        /// <summary>
        /// Generated images
        /// </summary>
        public List<GeneratedImage> Images { get; set; } = new List<GeneratedImage>();

        /// <summary>
        /// Model used for generation
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Provider that generated the images
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Was this from a fallback provider
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

    /// <summary>
    /// Represents a single generated image
    /// </summary>
    public class GeneratedImage
    {
        /// <summary>
        /// URL to access the image
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Base64 encoded image data (if available)
        /// </summary>
        public string? Base64Data { get; set; }

        /// <summary>
        /// Local file path (if saved to server)
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Image dimensions
        /// </summary>
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// Seed used for generation
        /// </summary>
        public long? Seed { get; set; }

        /// <summary>
        /// Revised/enhanced prompt (if provider modified it)
        /// </summary>
        public string? RevisedPrompt { get; set; }
    }
}
