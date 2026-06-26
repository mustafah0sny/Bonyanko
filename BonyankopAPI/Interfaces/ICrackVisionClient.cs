using BonyankopAPI.Models;

namespace BonyankopAPI.Interfaces;

/// <summary>
/// Client for the external CrackVision structural-damage-detection AI service.
/// </summary>
public interface ICrackVisionClient
{
    /// <summary>
    /// Sends the raw image bytes to the model's <c>/predict</c> endpoint and
    /// returns the parsed prediction together with the raw JSON payload.
    /// </summary>
    /// <param name="imageBytes">The image content to analyze.</param>
    /// <param name="fileName">Original file name (used for the multipart part).</param>
    /// <param name="contentType">MIME type of the image (e.g. image/jpeg).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed response and the raw JSON string.</returns>
    Task<(CrackVisionResponse Result, string RawJson)> PredictAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
