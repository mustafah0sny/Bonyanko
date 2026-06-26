using System.Text.Json.Serialization;

namespace BonyankopAPI.Models;

/// <summary>
/// Strongly-typed model of the JSON returned by the CrackVision AI service
/// (POST https://nourgad12-structural-damage-detection1.hf.space/predict).
/// Only the fields the application consumes are mapped; the full raw JSON is
/// persisted separately on <see cref="Diagnostic.AiRawResponseJson"/>.
/// </summary>
public class CrackVisionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("has_defects")]
    public bool HasDefects { get; set; }

    [JsonPropertyName("defect_types_count")]
    public int DefectTypesCount { get; set; }

    [JsonPropertyName("total_instances")]
    public int TotalInstances { get; set; }

    [JsonPropertyName("overall")]
    public CrackVisionOverall? Overall { get; set; }

    [JsonPropertyName("primary_defect")]
    public CrackVisionDefect? PrimaryDefect { get; set; }

    [JsonPropertyName("defects")]
    public List<CrackVisionDefect> Defects { get; set; } = new();

    [JsonPropertyName("image_size")]
    public CrackVisionImageSize? ImageSize { get; set; }

    [JsonPropertyName("inference_ms")]
    public int InferenceMs { get; set; }

    [JsonPropertyName("elapsed_ms")]
    public int ElapsedMs { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    /// <summary>Base64-encoded annotated result image (PNG).</summary>
    [JsonPropertyName("image_base64")]
    public string? ImageBase64 { get; set; }

    /// <summary>Base64-encoded heatmap image (PNG).</summary>
    [JsonPropertyName("heatmap_base64")]
    public string? HeatmapBase64 { get; set; }
}

public class CrackVisionOverall
{
    /// <summary>Severity label: "Mild" | "Moderate" | "Severe".</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("color_hex")]
    public string? ColorHex { get; set; }

    [JsonPropertyName("requires_closure")]
    public bool RequiresClosure { get; set; }

    [JsonPropertyName("defect_types")]
    public int DefectTypes { get; set; }
}

public class CrackVisionDefect
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("label_ar")]
    public string? LabelAr { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("severity_label")]
    public string? SeverityLabel { get; set; }

    [JsonPropertyName("severity_score")]
    public double SeverityScore { get; set; }

    [JsonPropertyName("area_pct")]
    public double AreaPct { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("color_hex")]
    public string? ColorHex { get; set; }

    [JsonPropertyName("requires_closure")]
    public bool RequiresClosure { get; set; }

    [JsonPropertyName("treatment")]
    public CrackVisionTreatment? Treatment { get; set; }
}

public class CrackVisionTreatment
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("level")]
    public string? Level { get; set; }
}

public class CrackVisionImageSize
{
    [JsonPropertyName("w")]
    public int W { get; set; }

    [JsonPropertyName("h")]
    public int H { get; set; }
}
