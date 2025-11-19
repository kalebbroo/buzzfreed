using BuzzFreed.Web.AI.Models;

namespace BuzzFreed.Web.AI.Abstractions
{
    /// <summary>
    /// Interface for Image Generation providers
    /// </summary>
    public interface IImageProvider : IAIProvider
    {
        /// <summary>
        /// Generate image from text prompt
        /// </summary>
        Task<ImageResponse> GenerateImageAsync(ImageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate image with progress updates via callback
        /// </summary>
        Task<ImageResponse> GenerateImageWithProgressAsync(
            ImageRequest request,
            Action<int, string>? progressCallback = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Supported image sizes
        /// </summary>
        List<string> SupportedSizes { get; }

        /// <summary>
        /// Supported image formats
        /// </summary>
        List<string> SupportedFormats { get; }
    }
}
