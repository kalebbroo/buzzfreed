namespace BuzzFreed.Web.AI.Models
{
    /// <summary>
    /// Describes the capabilities of an AI provider
    /// </summary>
    public class ProviderCapabilities
    {
        public bool SupportsStreaming { get; set; }
        public bool SupportsChat { get; set; }
        public bool SupportsImages { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public int MaxTokens { get; set; }
        public int MaxImageSize { get; set; }
        public List<string> SupportedLanguages { get; set; } = new List<string>();
        public Dictionary<string, object> CustomCapabilities { get; set; } = new Dictionary<string, object>();
    }
}
