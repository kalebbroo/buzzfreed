namespace BuzzFreed.Web.AI.Models
{
    /// <summary>
    /// Request parameters for image generation
    /// </summary>
    public class ImageRequest
    {
        /// <summary>
        /// Text prompt describing the image
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Negative prompt (what to avoid)
        /// </summary>
        public string? NegativePrompt { get; set; }

        /// <summary>
        /// Model to use (e.g., "dall-e-3", "sd_xl_base_1.0")
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Number of images to generate
        /// </summary>
        public int Count { get; set; } = 1;

        /// <summary>
        /// Image width in pixels
        /// </summary>
        public int Width { get; set; } = 1024;

        /// <summary>
        /// Image height in pixels
        /// </summary>
        public int Height { get; set; } = 1024;

        /// <summary>
        /// Image size preset (e.g., "1024x1024", "landscape", "portrait")
        /// </summary>
        public string? Size { get; set; }

        /// <summary>
        /// Number of inference steps (for diffusion models)
        /// </summary>
        public int? Steps { get; set; }

        /// <summary>
        /// CFG scale / Guidance scale
        /// </summary>
        public double? GuidanceScale { get; set; }

        /// <summary>
        /// Random seed for reproducibility (-1 for random)
        /// </summary>
        public long Seed { get; set; } = -1;

        /// <summary>
        /// Image quality (e.g., "standard", "hd")
        /// </summary>
        public string? Quality { get; set; }

        /// <summary>
        /// Image style (e.g., "vivid", "natural")
        /// </summary>
        public string? Style { get; set; }

        /// <summary>
        /// Output format (e.g., "png", "jpg", "webp")
        /// </summary>
        public string Format { get; set; } = "png";

        /// <summary>
        /// Whether to save the image on the server
        /// </summary>
        public bool SaveToServer { get; set; } = true;

        /// <summary>
        /// Additional provider-specific parameters
        /// </summary>
        public Dictionary<string, object>? CustomParameters { get; set; }
    }
}
