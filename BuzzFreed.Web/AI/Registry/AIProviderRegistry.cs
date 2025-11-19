using BuzzFreed.Web.AI.Abstractions;
using BuzzFreed.Web.AI.Models;

namespace BuzzFreed.Web.AI.Registry
{
    /// <summary>
    /// Central registry for managing all AI providers
    /// </summary>
    public class AIProviderRegistry
    {
        private readonly ILogger<AIProviderRegistry> _logger;
        private readonly AIProvidersConfig _config;
        private readonly Dictionary<string, ILLMProvider> _llmProviders;
        private readonly Dictionary<string, IImageProvider> _imageProviders;

        public AIProviderRegistry(
            ILogger<AIProviderRegistry> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _config = new AIProvidersConfig();
            configuration.GetSection("AI:Providers").Bind(_config);

            _llmProviders = new Dictionary<string, ILLMProvider>();
            _imageProviders = new Dictionary<string, IImageProvider>();
        }

        /// <summary>
        /// Register an LLM provider
        /// </summary>
        public void RegisterLLMProvider(ILLMProvider provider)
        {
            _llmProviders[provider.ProviderId] = provider;
            _logger.LogInformation($"Registered LLM provider: {provider.ProviderName} ({provider.ProviderId})");
        }

        /// <summary>
        /// Register an Image provider
        /// </summary>
        public void RegisterImageProvider(IImageProvider provider)
        {
            _imageProviders[provider.ProviderId] = provider;
            _logger.LogInformation($"Registered Image provider: {provider.ProviderName} ({provider.ProviderId})");
        }

        /// <summary>
        /// Get best available LLM provider
        /// </summary>
        public async Task<ILLMProvider?> GetLLMProviderAsync(string? preferredProviderId = null)
        {
            // Try preferred provider first
            if (!string.IsNullOrEmpty(preferredProviderId) && _llmProviders.ContainsKey(preferredProviderId))
            {
                var preferred = _llmProviders[preferredProviderId];
                if (await preferred.IsAvailableAsync())
                {
                    return preferred;
                }
            }

            // Try default provider
            if (_llmProviders.ContainsKey(_config.DefaultLLMProvider))
            {
                var defaultProvider = _llmProviders[_config.DefaultLLMProvider];
                if (await defaultProvider.IsAvailableAsync())
                {
                    return defaultProvider;
                }
            }

            // Fallback: try any available provider by priority
            if (_config.EnableFallback)
            {
                var sortedProviders = _llmProviders.Values
                    .OrderByDescending(p => GetProviderPriority(p.ProviderId))
                    .ToList();

                foreach (var provider in sortedProviders)
                {
                    if (await provider.IsAvailableAsync())
                    {
                        _logger.LogWarning($"Using fallback LLM provider: {provider.ProviderName}");
                        return provider;
                    }
                }
            }

            _logger.LogError("No available LLM providers found");
            return null;
        }

        /// <summary>
        /// Get best available Image provider
        /// </summary>
        public async Task<IImageProvider?> GetImageProviderAsync(string? preferredProviderId = null)
        {
            // Try preferred provider first
            if (!string.IsNullOrEmpty(preferredProviderId) && _imageProviders.ContainsKey(preferredProviderId))
            {
                var preferred = _imageProviders[preferredProviderId];
                if (await preferred.IsAvailableAsync())
                {
                    return preferred;
                }
            }

            // Try default provider
            if (_imageProviders.ContainsKey(_config.DefaultImageProvider))
            {
                var defaultProvider = _imageProviders[_config.DefaultImageProvider];
                if (await defaultProvider.IsAvailableAsync())
                {
                    return defaultProvider;
                }
            }

            // Fallback: try any available provider by priority
            if (_config.EnableFallback)
            {
                var sortedProviders = _imageProviders.Values
                    .OrderByDescending(p => GetProviderPriority(p.ProviderId))
                    .ToList();

                foreach (var provider in sortedProviders)
                {
                    if (await provider.IsAvailableAsync())
                    {
                        _logger.LogWarning($"Using fallback Image provider: {provider.ProviderName}");
                        return provider;
                    }
                }
            }

            _logger.LogError("No available Image providers found");
            return null;
        }

        /// <summary>
        /// Get all registered LLM providers
        /// </summary>
        public IEnumerable<ILLMProvider> GetAllLLMProviders() => _llmProviders.Values;

        /// <summary>
        /// Get all registered Image providers
        /// </summary>
        public IEnumerable<IImageProvider> GetAllImageProviders() => _imageProviders.Values;

        /// <summary>
        /// Get provider configuration
        /// </summary>
        public AIProviderConfig? GetProviderConfig(string providerId)
        {
            return _config.Providers.FirstOrDefault(p => p.ProviderId == providerId);
        }

        /// <summary>
        /// Get provider priority from config
        /// </summary>
        private int GetProviderPriority(string providerId)
        {
            var config = GetProviderConfig(providerId);
            return config?.Priority ?? 0;
        }

        /// <summary>
        /// Check if a provider is enabled
        /// </summary>
        public bool IsProviderEnabled(string providerId)
        {
            var config = GetProviderConfig(providerId);
            return config?.Enabled ?? false;
        }
    }
}
