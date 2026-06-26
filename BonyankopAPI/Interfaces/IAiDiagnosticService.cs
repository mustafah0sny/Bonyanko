using BonyankopAPI.Models;

namespace BonyankopAPI.Interfaces;

public interface IAiDiagnosticService
{
    /// <summary>
    /// Analyze an image with the CrackVision AI model and produce a diagnostic result.
    /// </summary>
    /// <param name="imageBytes">Raw image content.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">Image MIME type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DiagnosticResult> AnalyzeImageAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recommendations based on diagnostic results
    /// </summary>
    List<string> GetRecommendations(ProblemCategory category, RiskLevel riskLevel, bool isDiyPossible);
}

public class DiagnosticResult
{
    public RiskLevel RiskLevel { get; set; }
    public ProblemCategory ProblemCategory { get; set; }
    public string ProblemSubcategory { get; set; } = string.Empty;
    public string ProbableCause { get; set; } = string.Empty;
    public string RiskPrediction { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public decimal AiConfidenceScore { get; set; }
    public bool IsDiyPossible { get; set; }
    public string EstimatedCostRange { get; set; } = string.Empty;
    public UrgencyLevel UrgencyLevel { get; set; }
    public int ProcessingTimeMs { get; set; }
    public string AiModelVersion { get; set; } = string.Empty;

    /// <summary>Full raw JSON returned by the model (persisted verbatim).</summary>
    public string RawJson { get; set; } = string.Empty;

    /// <summary>Base64-encoded annotated result image (PNG), if any.</summary>
    public string? ResultImageBase64 { get; set; }

    /// <summary>Base64-encoded heatmap image (PNG), if any.</summary>
    public string? HeatmapImageBase64 { get; set; }
}
