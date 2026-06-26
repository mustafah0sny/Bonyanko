using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BonyankopAPI.DTOs;
using BonyankopAPI.Models;
using BonyankopAPI.Repositories;
using BonyankopAPI.Interfaces;
using System.Security.Claims;
using BonyankopAPI.Services;
using Microsoft.IdentityModel.Tokens;

namespace BonyankopAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly IDiagnosticRepository _diagnosticRepository;
    private readonly IAiDiagnosticService _aiService;
    private readonly IUserRepository _userRepository;
    private readonly IWebHostEnvironment _environment;
    private readonly ITokenService _tokenService;


    public DiagnosticController(
        IDiagnosticRepository diagnosticRepository,
        IAiDiagnosticService aiService,
        IUserRepository userRepository,
        IWebHostEnvironment environment,
        ITokenService tokenService)
    {
        _diagnosticRepository = diagnosticRepository;
        _aiService = aiService;
        _userRepository = userRepository;
        _environment = environment;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Analyze an uploaded image with the CrackVision AI model to detect structural damage.
    /// The uploaded image is stored under wwwroot/images/{userId}/uploads/ and the model's
    /// annotated result and heatmap images are saved alongside it.
    /// Send as multipart/form-data with an "Image" file field.
    /// </summary>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    //[Authorize(Roles = "CITIZEN,ADMIN")]
    public async Task<IActionResult> AnalyzeImage([FromForm] AnalyzeImageUploadDto dto)
    {
        var userId = _tokenService.GetUserIdFromToken(User);
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var file = dto.Image;
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No image file was provided" });
        }
        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File exceeds the maximum allowed size of 10 MB" });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = $"Invalid file type. Allowed types: {string.Join(", ", AllowedExtensions)}"
            });
        }

        // Read the uploaded file into memory once (used for both storage and AI call).
        byte[] imageBytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            imageBytes = ms.ToArray();
        }

        // Save the user-uploaded image: wwwroot/images/{userId}/uploads/{userId}_{date}.{ext}
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var uploadFileName = $"{userId}_{timestamp}{extension}";
        var uploadRelativeUrl = SaveImageBytes(imageBytes, userId, "uploads", uploadFileName);

        // Call the CrackVision AI model.
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
        DiagnosticResult aiResult;
        try
        {
            aiResult = await _aiService.AnalyzeImageAsync(imageBytes, file.FileName, contentType, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "The AI analysis service is currently unavailable. Please try again.",
                error = ex.Message
            });
        }

        // Persist the model's annotated result / heatmap images (decoded from base64), if present.
        var resultImageUrl = SaveBase64Image(aiResult.ResultImageBase64, userId, "results", $"{userId}_{timestamp}_result.png");
        var heatmapImageUrl = SaveBase64Image(aiResult.HeatmapImageBase64, userId, "results", $"{userId}_{timestamp}_heatmap.png");

        var metadata = new ImageMetadata
        {
            Format = extension.TrimStart('.'),
            Size = imageBytes.Length,
            CapturedAt = DateTime.UtcNow
        };

        // Create diagnostic record
        var diagnostic = new Diagnostic
        {
            DiagnosticId = Guid.NewGuid(),
            CitizenId = userId,
            ImageUrl = uploadRelativeUrl,
            ImageMetadata = metadata,
            RiskLevel = aiResult.RiskLevel,
            ProblemCategory = aiResult.ProblemCategory,
            ProblemSubcategory = aiResult.ProblemSubcategory,
            ProbableCause = aiResult.ProbableCause,
            RiskPrediction = aiResult.RiskPrediction,
            RecommendedAction = aiResult.RecommendedAction,
            AiConfidenceScore = aiResult.AiConfidenceScore,
            AiModelVersion = aiResult.AiModelVersion,
            ProcessingTimeMs = aiResult.ProcessingTimeMs,
            IsDiyPossible = aiResult.IsDiyPossible,
            EstimatedCostRange = aiResult.EstimatedCostRange,
            UrgencyLevel = aiResult.UrgencyLevel,
            AiRawResponseJson = aiResult.RawJson,
            ResultImageUrl = resultImageUrl,
            HeatmapImageUrl = heatmapImageUrl,
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
    /// Writes raw image bytes under wwwroot/images/{userId}/{subFolder}/{fileName}
    /// and returns the web-accessible relative path.
    /// </summary>
    private string SaveImageBytes(byte[] bytes, Guid userId, string subFolder, string fileName)
    {
        var webRootPath = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRootPath, "images", userId.ToString(), subFolder);
        Directory.CreateDirectory(folder);

        var absolutePath = Path.Combine(folder, fileName);
        System.IO.File.WriteAllBytes(absolutePath, bytes);

        return $"/images/{userId}/{subFolder}/{fileName}";
    }

    /// <summary>
    /// Decodes a (possibly null / data-URI-prefixed) base64 image string and saves it.
    /// Returns null when no image data is supplied.
    /// </summary>
    private string? SaveBase64Image(string? base64, Guid userId, string subFolder, string fileName)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        // Strip an optional data-URI prefix (e.g. "data:image/png;base64,").
        var commaIndex = base64.IndexOf(',');
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            base64 = base64[(commaIndex + 1)..];
        }

        try
        {
            var bytes = Convert.FromBase64String(base64);
            return SaveImageBytes(bytes, userId, subFolder, fileName);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds an absolute, client-loadable URL from a stored relative image path.
    /// </summary>
    private string? BuildImageUrl(string? storedPath)
    {
        if (string.IsNullOrEmpty(storedPath))
            return null;
        if (Uri.IsWellFormedUriString(storedPath, UriKind.Absolute))
            return storedPath;
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}{storedPath}";
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
        var userId = _tokenService.GetUserIdFromToken(User);
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
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
        var userId = _tokenService.GetUserIdFromToken(User);
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
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

    private DiagnosticResponseDto MapToResponseDto(Diagnostic diagnostic, List<string> recommendations)
    {
        return new DiagnosticResponseDto
        {
            DiagnosticId = diagnostic.DiagnosticId,
            CitizenId = diagnostic.CitizenId,
            ImageUrl = BuildImageUrl(diagnostic.ImageUrl) ?? diagnostic.ImageUrl,
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
            HeatmapImageUrl = BuildImageUrl(diagnostic.HeatmapImageUrl),
            ResultImageUrl = BuildImageUrl(diagnostic.ResultImageUrl),
            Recommendations = recommendations,
            CreatedAt = diagnostic.CreatedAt
        };
    }
}
