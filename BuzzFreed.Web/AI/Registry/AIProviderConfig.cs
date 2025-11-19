namespace BuzzFreed.Web.AI.Registry
{
    /// <summary>
    /// Configuration for an AI provider
    /// </summary>
    public class AIProviderConfig
    {
        public string ProviderId { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public int Priority { get; set} = 0; // Higher priority = preferred
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; }
        public string? DefaultModel { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 2;
        public Dictionary<string, string>? CustomSettings { get; set; }
    }

    /// <summary>
    /// Root configuration for all AI providers
    /// </summary>
    public class AIProvidersConfig
    {
        public List<AIProviderConfig> Providers { get; set; } = new List<AIProviderConfig>();
        public string DefaultLLMProvider { get; set; } = "openai";
        public string DefaultImageProvider { get; set; } = "openai";
        public bool EnableFallback { get; set; } = true;
    }
}
