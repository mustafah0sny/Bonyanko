using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BonyankopAPI.Models;

public class Diagnostic
{
    [Key]
    public Guid DiagnosticId { get; set; }

    [Required]
    public Guid CitizenId { get; set; }

    [Required]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [Required]
    public string ImageMetadataJson { get; set; } = string.Empty;

    [NotMapped]
    public ImageMetadata ImageMetadata
    {
        get => string.IsNullOrEmpty(ImageMetadataJson) 
            ? new ImageMetadata() 
            : System.Text.Json.JsonSerializer.Deserialize<ImageMetadata>(ImageMetadataJson) ?? new ImageMetadata();
        set => ImageMetadataJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Required]
    public RiskLevel RiskLevel { get; set; }

    [Required]
    public ProblemCategory ProblemCategory { get; set; }

    [MaxLength(200)]
    public string? ProblemSubcategory { get; set; }

    [Required]
    [MaxLength(500)]
    public string ProbableCause { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string RiskPrediction { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string RecommendedAction { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    public decimal AiConfidenceScore { get; set; }

    [MaxLength(50)]
    public string? AiModelVersion { get; set; }

    public int? ProcessingTimeMs { get; set; }

    public bool IsDiyPossible { get; set; }

    [MaxLength(100)]
    public string? EstimatedCostRange { get; set; }

    public UrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Full, raw JSON response returned by the CrackVision AI model.
    /// Stored verbatim so no detail (per-defect detections, treatment, factors…) is lost.
    /// </summary>
    public string? AiRawResponseJson { get; set; }

    /// <summary>
    /// Web-accessible relative path to the saved heatmap image produced by the model.
    /// </summary>
    [MaxLength(500)]
    public string? HeatmapImageUrl { get; set; }

    /// <summary>
    /// Web-accessible relative path to the saved annotated result image produced by the model.
    /// </summary>
    [MaxLength(500)]
    public string? ResultImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey(nameof(CitizenId))]
    public virtual User Citizen { get; set; } = null!;
}
