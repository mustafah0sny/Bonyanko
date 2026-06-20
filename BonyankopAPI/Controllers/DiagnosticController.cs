using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BonyankopAPI.DTOs;
using BonyankopAPI.Models;
using BonyankopAPI.Repositories;
using BonyankopAPI.Interfaces;
using System.Security.Claims;

namespace BonyankopAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticController : ControllerBase
{
    //test
    private readonly IDiagnosticRepository _diagnosticRepository;
    private readonly IAiDiagnosticService _aiService;
    private readonly IUserRepository _userRepository;

    public DiagnosticController(
        IDiagnosticRepository diagnosticRepository,
        IAiDiagnosticService aiService,
        IUserRepository userRepository)
    {
        _diagnosticRepository = diagnosticRepository;
        _aiService = aiService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Analyze an image using AI to diagnose home maintenance issues
    /// </summary>
    [HttpPost("analyze")]
    //[Authorize(Roles = "CITIZEN,ADMIN")]
    public async Task<IActionResult> AnalyzeImage([FromBody] AnalyzeImageDto dto)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user token" });
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Prepare metadata
        var metadata = dto.Metadata != null ? new ImageMetadata
        {
            Width = dto.Metadata.Width,
            Height = dto.Metadata.Height,
            Format = dto.Metadata.Format,
            Size = dto.Metadata.Size,
            CapturedAt = dto.Metadata.CapturedAt
        } : new ImageMetadata
        {
            Width = 1920,
            Height = 1080,
            Format = "jpeg",
            Size = 0,
            CapturedAt = DateTime.UtcNow
        };

        // Call AI service to analyze image
        var aiResult = await _aiService.AnalyzeImageAsync(dto.ImageUrl, metadata);

        // Create diagnostic record
        var diagnostic = new Diagnostic
        {
            DiagnosticId = Guid.NewGuid(),
            CitizenId = userId,
            ImageUrl = dto.ImageUrl,
            ImageMetadata = metadata,
            RiskLevel = aiResult.RiskLevel,
            ProblemCategory = aiResult.ProblemCategory,
            ProblemSubcategory = aiResult.ProblemSubcategory,
            ProbableCause = aiResult.ProbableCause,
            RiskPrediction = aiResult.RiskPrediction,
            RecommendedAction = aiResult.RecommendedAction,
            AiConfidenceScore = aiResult.AiConfidenceScore,
            AiModelVersion = "v1.0.0-mock",
            ProcessingTimeMs = aiResult.ProcessingTimeMs,
            IsDiyPossible = aiResult.IsDiyPossible,
            EstimatedCostRange = aiResult.EstimatedCostRange,
            UrgencyLevel = aiResult.UrgencyLevel,
            CreatedAt = DateTime.UtcNow
        };

        await _diagnosticRepository.AddAsync(diagnostic);
        await _diagnosticRepository.SaveChangesAsync();

        // Get recommendations
        var recommendations = _aiService.GetRecommendations(
            aiResult.ProblemCategory,
            aiResult.RiskLevel,
            aiResult.IsDiyPossible
        );

        var response = MapToResponseDto(diagnostic, recommendations);
        return CreatedAtAction(nameof(GetDiagnostic), new { diagnosticId = diagnostic.DiagnosticId }, response);
    }

    /// <summary>
    /// Get diagnostic by ID
    /// </summary>
    [HttpGet("{diagnosticId}")]
    //[Authorize]
    public async Task<IActionResult> GetDiagnostic(Guid diagnosticId)
    {
        var diagnostic = await _diagnosticRepository.GetByIdAsync(diagnosticId);
        if (diagnostic == null)
        {
            return NotFound(new { message = "Diagnostic not found" });
        }

        // Check if user has access (owner or admin)
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user?.Role != UserRole.ADMIN && diagnostic.CitizenId != userId)
            {
                return Forbid();
            }
        }

        var recommendations = _aiService.GetRecommendations(
            diagnostic.ProblemCategory,
            diagnostic.RiskLevel,
            diagnostic.IsDiyPossible
        );

        var response = MapToResponseDto(diagnostic, recommendations);
        return Ok(response);
    }

    /// <summary>
    /// Get all diagnostics for current user
    /// </summary>
    [HttpGet("my-diagnostics")]
    //[Authorize(Roles = "CITIZEN,ADMIN")]
    public async Task<IActionResult> GetMyDiagnostics()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user token" });
        }

        var diagnostics = await _diagnosticRepository.GetByCitizenIdAsync(userId);
        var responses = diagnostics.Select(d => MapToResponseDto(d, new List<string>())).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Get diagnostics by risk level
    /// </summary>
    [HttpGet("by-risk/{riskLevel}")]
    //[Authorize(Roles = "ADMIN,GOVERNMENT")]
    public async Task<IActionResult> GetByRiskLevel(string riskLevel)
    {
        if (!Enum.TryParse<RiskLevel>(riskLevel.ToUpper(), out var parsedRiskLevel))
        {
            return BadRequest(new { message = "Invalid risk level. Valid values: LOW, MEDIUM, HIGH" });
        }

        var diagnostics = await _diagnosticRepository.GetByRiskLevelAsync(parsedRiskLevel);
        var responses = diagnostics.Select(d => MapToResponseDto(d, new List<string>())).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Get diagnostics by problem category
    /// </summary>
    [HttpGet("by-category/{category}")]
    //[Authorize(Roles = "ADMIN,GOVERNMENT")]
    public async Task<IActionResult> GetByCategory(string category)
    {
        if (!Enum.TryParse<ProblemCategory>(category.ToUpper(), out var parsedCategory))
        {
            return BadRequest(new { message = "Invalid category. Valid values: PLUMBING, ELECTRICAL, STRUCTURAL, HVAC, ROOFING" });
        }

        var diagnostics = await _diagnosticRepository.GetByProblemCategoryAsync(parsedCategory);
        var responses = diagnostics.Select(d => MapToResponseDto(d, new List<string>())).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Get recent diagnostics
    /// </summary>
    [HttpGet("recent")]
    //[Authorize(Roles = "ADMIN,GOVERNMENT")]
    public async Task<IActionResult> GetRecentDiagnostics([FromQuery] int count = 10)
    {
        if (count < 1 || count > 100)
        {
            return BadRequest(new { message = "Count must be between 1 and 100" });
        }

        var diagnostics = await _diagnosticRepository.GetRecentDiagnosticsAsync(count);
        var responses = diagnostics.Select(d => MapToResponseDto(d, new List<string>())).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Get diagnostic statistics
    /// </summary>
    [HttpGet("statistics")]
    //[Authorize(Roles = "ADMIN,GOVERNMENT")]
    public async Task<IActionResult> GetStatistics()
    {
        var categoryStats = await _diagnosticRepository.GetCategoryStatisticsAsync();
        var riskLevelStats = await _diagnosticRepository.GetRiskLevelStatisticsAsync();
        var allDiagnostics = await _diagnosticRepository.GetAllAsync();

        var statistics = new DiagnosticStatisticsDto
        {
            TotalDiagnostics = allDiagnostics.Count(),
            CategoryBreakdown = categoryStats.ToDictionary(x => x.Key.ToString(), x => x.Value),
            RiskLevelBreakdown = riskLevelStats.ToDictionary(x => x.Key.ToString(), x => x.Value),
            AverageConfidenceScore = allDiagnostics.Any() ? allDiagnostics.Average(d => d.AiConfidenceScore) : 0,
            HighRiskCount = riskLevelStats.ContainsKey(RiskLevel.HIGH) ? riskLevelStats[RiskLevel.HIGH] : 0
        };

        return Ok(statistics);
    }

    private static DiagnosticResponseDto MapToResponseDto(Diagnostic diagnostic, List<string> recommendations)
    {
        return new DiagnosticResponseDto
        {
            DiagnosticId = diagnostic.DiagnosticId,
            CitizenId = diagnostic.CitizenId,
            ImageUrl = diagnostic.ImageUrl,
            Metadata = new ImageMetadataDto
            {
                Width = diagnostic.ImageMetadata.Width,
                Height = diagnostic.ImageMetadata.Height,
                Format = diagnostic.ImageMetadata.Format,
                Size = diagnostic.ImageMetadata.Size,
                CapturedAt = diagnostic.ImageMetadata.CapturedAt
            },
            RiskLevel = diagnostic.RiskLevel.ToString(),
            ProblemCategory = diagnostic.ProblemCategory.ToString(),
            ProblemSubcategory = diagnostic.ProblemSubcategory,
            ProbableCause = diagnostic.ProbableCause,
            RiskPrediction = diagnostic.RiskPrediction,
            RecommendedAction = diagnostic.RecommendedAction,
            AiConfidenceScore = diagnostic.AiConfidenceScore,
            AiModelVersion = diagnostic.AiModelVersion,
            ProcessingTimeMs = diagnostic.ProcessingTimeMs,
            IsDiyPossible = diagnostic.IsDiyPossible,
            EstimatedCostRange = diagnostic.EstimatedCostRange,
            UrgencyLevel = diagnostic.UrgencyLevel?.ToString(),
            Recommendations = recommendations,
            CreatedAt = diagnostic.CreatedAt
        };
    }
}
