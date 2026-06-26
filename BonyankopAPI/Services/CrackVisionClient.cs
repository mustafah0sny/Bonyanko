using System.Net.Http.Headers;
using System.Text.Json;
using BonyankopAPI.Interfaces;
using BonyankopAPI.Models;
using Microsoft.Extensions.Options;

namespace BonyankopAPI.Services;

/// <summary>
/// Options for the CrackVision AI service. Bound from configuration section "CrackVision".
/// </summary>
public class CrackVisionOptions
{
    public const string SectionName = "CrackVision";

    /// <summary>Base address of the model, e.g. https://nourgad12-structural-damage-detection1.hf.space/.</summary>
    public string BaseUrl { get; set; } = "https://nourgad12-structural-damage-detection1.hf.space/";

    /// <summary>Relative path of the prediction endpoint.</summary>
    public string PredictPath { get; set; } = "predict";

    /// <summary>Request timeout in seconds (the model runs on CPU and can be slow / cold-start).</summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper that calls the CrackVision <c>/predict</c> endpoint.
/// </summary>
public class CrackVisionClient : ICrackVisionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly CrackVisionOptions _options;
    private readonly ILogger<CrackVisionClient> _logger;

    public CrackVisionClient(
        HttpClient httpClient,
        IOptions<CrackVisionOptions> options,
        ILogger<CrackVisionClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(CrackVisionResponse Result, string RawJson)> PredictAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            throw new ArgumentException("Image content is empty", nameof(imageBytes));

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        // The model expects the multipart field name to be "file".
        content.Add(imageContent, "file", string.IsNullOrWhiteSpace(fileName) ? "upload.jpg" : fileName);

        _logger.LogInformation("Sending image '{FileName}' ({Bytes} bytes) to CrackVision /predict", fileName, imageBytes.Length);

        using var response = await _httpClient.PostAsync(_options.PredictPath, content, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("CrackVision returned {StatusCode}: {Body}", (int)response.StatusCode, rawJson);
            throw new HttpRequestException(
                $"CrackVision prediction failed with status {(int)response.StatusCode}.");
        }

        var result = JsonSerializer.Deserialize<CrackVisionResponse>(rawJson, JsonOptions)
            ?? throw new InvalidOperationException("CrackVision returned an empty or unparsable response.");

        return (result, rawJson);
    }
}
