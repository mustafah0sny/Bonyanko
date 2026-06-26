using System.ComponentModel.DataAnnotations;
using BonyankopAPI.Models;

namespace BonyankopAPI.DTOs;

/// <summary>
/// Request to analyze an uploaded image with the CrackVision AI model
/// (sent as multipart/form-data).
/// </summary>
public class AnalyzeImageUploadDto
{
    /// <summary>The image file to analyze.</summary>
    [Required]
    public IFormFile Image { get; set; } = default!;
}

public class ImageMetadataDto
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = string.Empty;
    public int Size { get; set; }
    public DateTime? CapturedAt { get; set; }
}

public class DiagnosticResponseDto
{
    public Guid DiagnosticId { get; set; }
    public Guid CitizenId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public ImageMetadataDto Metadata { get; set; } = new();
    public string RiskLevel { get; set; } = string.Empty;
    public string ProblemCategory { get; set; } = string.Empty;
    public string? ProblemSubcategory { get; set; }
    public string ProbableCause { get; set; } = string.Empty;
    public string RiskPrediction { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public decimal AiConfidenceScore { get; set; }
    public string? AiModelVersion { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public bool IsDiyPossible { get; set; }
    public string? EstimatedCostRange { get; set; }
    public string? UrgencyLevel { get; set; }
    public string? HeatmapImageUrl { get; set; }
    public string? ResultImageUrl { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class DiagnosticStatisticsDto
{
    public int TotalDiagnostics { get; set; }
    public Dictionary<string, int> CategoryBreakdown { get; set; } = new();
    public Dictionary<string, int> RiskLevelBreakdown { get; set; } = new();
    public decimal AverageConfidenceScore { get; set; }
    public int HighRiskCount { get; set; }
}
