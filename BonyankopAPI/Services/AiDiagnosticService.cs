using System.Diagnostics;
using BonyankopAPI.Interfaces;
using BonyankopAPI.Models;

namespace BonyankopAPI.Services;

public class AiDiagnosticService : IAiDiagnosticService
{
    private const string AI_MODEL_VERSION = "CrackVision v5.1.0";

    private readonly ICrackVisionClient _crackVision;
    private readonly ILogger<AiDiagnosticService> _logger;

    public AiDiagnosticService(ICrackVisionClient crackVision, ILogger<AiDiagnosticService> logger)
    {
        _crackVision = crackVision;
        _logger = logger;
    }

    public async Task<DiagnosticResult> AnalyzeImageAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting CrackVision analysis for '{FileName}'", fileName);

        var (response, rawJson) = await _crackVision.PredictAsync(imageBytes, fileName, contentType, cancellationToken);

        stopwatch.Stop();

        var result = MapToDiagnosticResult(response, rawJson);
        // Prefer the model's own inference time; fall back to round-trip time.
        result.ProcessingTimeMs = response.InferenceMs > 0 ? response.InferenceMs : (int)stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "CrackVision analysis completed: risk={Risk}, defects={Defects}, confidence={Confidence}%",
            result.RiskLevel, response.TotalInstances, result.AiConfidenceScore);

        return result;
    }

    private DiagnosticResult MapToDiagnosticResult(CrackVisionResponse response, string rawJson)
    {
        var overall = response.Overall;
        var primary = response.PrimaryDefect;

        var riskLevel = MapSeverityToRiskLevel(overall?.Label);
        var urgency = MapToUrgency(riskLevel, overall?.RequiresClosure ?? false);

        // No structural damage detected -> clean, low-risk result.
        if (!response.HasDefects || primary == null)
        {
            return new DiagnosticResult
            {
                RiskLevel = RiskLevel.LOW,
                ProblemCategory = ProblemCategory.STRUCTURAL,
                ProblemSubcategory = "No Defect Detected",
                ProbableCause = "No structural defects were detected in the supplied image.",
                RiskPrediction = "No immediate structural risk identified.",
                RecommendedAction = "No action required. Continue routine periodic inspection.",
                AiConfidenceScore = 0m,
                IsDiyPossible = false,
                EstimatedCostRange = "N/A",
                UrgencyLevel = UrgencyLevel.LOW,
                AiModelVersion = AI_MODEL_VERSION,
                RawJson = rawJson,
                ResultImageBase64 = response.ImageBase64,
                HeatmapImageBase64 = response.HeatmapBase64
            };
        }

        var subcategory = primary.Label ?? "Structural Defect";
        var treatmentAction = primary.Treatment?.Action;

        var probableCause = response.TotalInstances > 1
            ? $"{response.TotalInstances} defect instance(s) detected across {response.DefectTypesCount} type(s); primary type: '{subcategory}' covering ~{primary.AreaPct:0.#}% of the image."
            : $"'{subcategory}' detected covering ~{primary.AreaPct:0.#}% of the image.";

        var riskPrediction = (overall?.RequiresClosure ?? false)
            ? $"Severity '{overall?.Label}' (score {overall?.Score:0.#}). Closure / restricted access is recommended until inspected."
            : $"Severity '{overall?.Label}' (score {overall?.Score:0.#}). Condition may deteriorate if left untreated.";

        var recommendedAction = !string.IsNullOrWhiteSpace(treatmentAction)
            ? treatmentAction!
            : "Consult a qualified structural engineer for a detailed on-site assessment.";

        return new DiagnosticResult
        {
            RiskLevel = riskLevel,
            ProblemCategory = ProblemCategory.STRUCTURAL,
            ProblemSubcategory = subcategory,
            ProbableCause = Truncate(probableCause, 500),
            RiskPrediction = Truncate(riskPrediction, 1000),
            RecommendedAction = Truncate(recommendedAction, 1000),
            AiConfidenceScore = Math.Round((decimal)primary.Confidence, 2),
            IsDiyPossible = riskLevel == RiskLevel.LOW && !(overall?.RequiresClosure ?? false),
            EstimatedCostRange = EstimateCostRange(riskLevel),
            UrgencyLevel = urgency,
            AiModelVersion = AI_MODEL_VERSION,
            RawJson = rawJson,
            ResultImageBase64 = response.ImageBase64,
            HeatmapImageBase64 = response.HeatmapBase64
        };
    }

    private static RiskLevel MapSeverityToRiskLevel(string? severityLabel)
        => (severityLabel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "severe" or "high" or "critical" => RiskLevel.HIGH,
            "moderate" or "medium" => RiskLevel.MEDIUM,
            _ => RiskLevel.LOW
        };

    private static UrgencyLevel MapToUrgency(RiskLevel riskLevel, bool requiresClosure)
    {
        if (requiresClosure) return UrgencyLevel.URGENT;
        return riskLevel switch
        {
            RiskLevel.HIGH => UrgencyLevel.HIGH,
            RiskLevel.MEDIUM => UrgencyLevel.MEDIUM,
            _ => UrgencyLevel.LOW
        };
    }

    private static string EstimateCostRange(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.HIGH => "$3,000-$15,000",
        RiskLevel.MEDIUM => "$800-$3,000",
        _ => "$200-$800"
    };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    public List<string> GetRecommendations(ProblemCategory category, RiskLevel riskLevel, bool isDiyPossible)
    {
        var recommendations = new List<string>();

        // General recommendations based on risk level
        switch (riskLevel)
        {
            case RiskLevel.HIGH:
                recommendations.Add("⚠️ Immediate professional attention required");
                recommendations.Add("Do not attempt DIY repairs");
                recommendations.Add("Contact a licensed professional within 24 hours");
                break;
            case RiskLevel.MEDIUM:
                recommendations.Add("Professional inspection recommended");
                recommendations.Add("Address this issue within the next week");
                break;
            case RiskLevel.LOW:
                if (isDiyPossible)
                {
                    recommendations.Add("This may be suitable for DIY repair");
                    recommendations.Add("Ensure you have proper tools and safety equipment");
                }
                else
                {
                    recommendations.Add("Consider hiring a professional for quality assurance");
                }
                break;
        }

        // Category-specific recommendations
        switch (category)
        {
            case ProblemCategory.ELECTRICAL:
                recommendations.Add("⚡ Always turn off power at the breaker before inspection");
                recommendations.Add("Hire a licensed electrician for safety");
                recommendations.Add("Check for code compliance");
                break;

            case ProblemCategory.PLUMBING:
                recommendations.Add("💧 Turn off water supply if there's active leaking");
                recommendations.Add("Document water damage for insurance");
                recommendations.Add("Check for mold growth in affected areas");
                break;

            case ProblemCategory.STRUCTURAL:
                recommendations.Add("🏗️ Structural issues can affect building safety");
                recommendations.Add("Get multiple professional assessments");
                recommendations.Add("May require building permit for repairs");
                break;

            case ProblemCategory.HVAC:
                recommendations.Add("❄️ Schedule regular maintenance to prevent issues");
                recommendations.Add("Check air filters and replace if needed");
                recommendations.Add("Consider energy efficiency upgrades");
                break;

            case ProblemCategory.ROOFING:
                recommendations.Add("🏠 Inspect attic for water damage or leaks");
                recommendations.Add("Schedule repair before rainy season");
                recommendations.Add("Get warranty information from contractor");
                break;
        }

        recommendations.Add("💰 Get 2-3 quotes before hiring a contractor");
        recommendations.Add("📸 Document the issue with photos for records");
        recommendations.Add("✅ Verify contractor licenses and insurance");

        return recommendations;
    }
}
