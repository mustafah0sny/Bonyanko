using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BonyankopAPI.DTOs;
using BonyankopAPI.Interfaces;
using BCrypt.Net;

namespace BonyankopAPI.Controllers
{
    /// <summary>
    /// Handles user profile management operations
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(
            IUserRepository userRepository,
            ITokenService tokenService,
            IWebHostEnvironment environment)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _environment = environment;
        }

        /// <summary>
        /// Builds an absolute, client-loadable URL from a stored profile picture path.
        /// Returns the value unchanged if it is already absolute (e.g. an external/social URL),
        /// and null if no picture is set.
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
        /// Validates an uploaded image, stores it under wwwroot/images/{userId}/
        /// (replacing any previous image), and returns the stored relative URL.
        /// Throws <see cref="InvalidImageException"/> when the file fails validation.
        /// </summary>
        private async Task<string> SaveProfileImageAsync(IFormFile file, Guid userId)
        {
            if (file == null || file.Length == 0)
                throw new InvalidImageException("No file was provided");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidImageException("File exceeds the maximum allowed size of 5 MB");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                throw new InvalidImageException(
                    $"Invalid file type. Allowed types: {string.Join(", ", AllowedExtensions)}");

            // wwwroot/images/{userId}
            var webRootPath = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var userFolder = Path.Combine(webRootPath, "images", userId.ToString());
            Directory.CreateDirectory(userFolder);

            // Remove any previously stored image for this user
            foreach (var existing in Directory.EnumerateFiles(userFolder))
            {
                System.IO.File.Delete(existing);
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var absolutePath = Path.Combine(userFolder, fileName);

            await using (var stream = new FileStream(absolutePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Web-accessible relative path (served via UseStaticFiles)
            return $"/images/{userId}/{fileName}";
        }

        /// <summary>
        /// Raised when an uploaded image fails validation (missing, too large, or wrong type).
        /// </summary>
        private sealed class InvalidImageException : Exception
        {
            public InvalidImageException(string message) : base(message) { }
        }

        /// <summary>
        /// Get current user profile
        /// </summary>
        /// <returns>User profile information</returns>
        /// <response code="200">Profile retrieved successfully</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> GetProfile()
        {
            try
            {
                var userId = _tokenService.GetUserIdFromToken(User);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(new
                {
                    userId = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    phoneNumber = user.PhoneNumber,
                    profilePictureUrl = BuildImageUrl(user.ProfilePictureUrl),
                    role = user.Role.ToString(),
                    isVerified = user.IsVerified,
                    isActive = user.IsActive,
                    lastLoginAt = user.LastLoginAt,
                    createdAt = user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        /// <summary>
        /// Update user profile
        /// </summary>
        /// <param name="updateDto">Profile update data</param>
        /// <returns>Updated user information</returns>
        /// <response code="200">Profile updated successfully</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> UpdateProfile([FromBody] UpdateProfileDto updateDto)
        {
            try
            {
                var userId = _tokenService.GetUserIdFromToken(User);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (!string.IsNullOrEmpty(updateDto.FullName))
                    user.FullName = updateDto.FullName;

                if (updateDto.PhoneNumber != null)
                    user.PhoneNumber = updateDto.PhoneNumber;

                if (updateDto.ProfilePictureUrl != null)
                    user.ProfilePictureUrl = updateDto.ProfilePictureUrl;

                user.UpdatedAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return Ok(new
                {
                    message = "Profile updated successfully",
                    user = new
                    {
                        userId = user.Id,
                        email = user.Email,
                        fullName = user.FullName,
                        phoneNumber = user.PhoneNumber,
                        profilePictureUrl = BuildImageUrl(user.ProfilePictureUrl),
                        role = user.Role.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        /// <summary>
        /// Upload a profile picture for the current user.
        /// The image is stored under wwwroot/images/{userId}/ and the relative path
        /// is saved on the user's ProfilePictureUrl.
        /// </summary>
        /// <param name="file">The image file to upload (multipart/form-data)</param>
        /// <returns>The stored image URL</returns>
        /// <response code="200">Image uploaded successfully</response>
        /// <response code="400">No file provided, invalid type, or file too large</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpPost("upload-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> UploadProfilePicture(IFormFile file)
        {
            try
            {
                var userId = _tokenService.GetUserIdFromToken(User);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var relativeUrl = await SaveProfileImageAsync(file, userId);
                user.ProfilePictureUrl = relativeUrl;
                user.UpdatedAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return Ok(new
                {
                    message = "Profile picture uploaded successfully",
                    profilePictureUrl = BuildImageUrl(relativeUrl)
                });
            }
            catch (InvalidImageException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        /// <summary>
        /// Update profile data and (optionally) the profile picture in a single request.
        /// Send as multipart/form-data: text fields for FullName/PhoneNumber and an
        /// optional Image file. When an image is supplied it is stored under
        /// wwwroot/images/{userId}/ and replaces the previous one.
        /// </summary>
        /// <param name="updateDto">Profile fields plus an optional image file</param>
        /// <returns>Updated user information</returns>
        /// <response code="200">Profile updated successfully</response>
        /// <response code="400">Invalid image file</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpPut("with-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> UpdateProfileWithImage([FromForm] UpdateProfileWithImageDto updateDto)
        {
            try
            {
                var userId = _tokenService.GetUserIdFromToken(User);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (!string.IsNullOrEmpty(updateDto.FullName))
                    user.FullName = updateDto.FullName;

                if (updateDto.PhoneNumber != null)
                    user.PhoneNumber = updateDto.PhoneNumber;

                if (updateDto.Image != null && updateDto.Image.Length > 0)
                {
                    user.ProfilePictureUrl = await SaveProfileImageAsync(updateDto.Image, userId);
                }

                user.UpdatedAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return Ok(new
                {
                    message = "Profile updated successfully",
                    user = new
                    {
                        userId = user.Id,
                        email = user.Email,
                        fullName = user.FullName,
                        phoneNumber = user.PhoneNumber,
                        profilePictureUrl = BuildImageUrl(user.ProfilePictureUrl),
                        role = user.Role.ToString()
                    }
                });
            }
            catch (InvalidImageException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        /// <summary>
        /// Change user password
        /// </summary>
        /// <param name="changePasswordDto">Old and new password</param>
        /// <returns>Success message</returns>
        /// <response code="200">Password changed successfully</response>
        /// <response code="400">Invalid old password</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpPost("change-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                var userId = _tokenService.GetUserIdFromToken(User);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.OldPassword, user.PasswordHash))
                {
                    return BadRequest(new { message = "Invalid old password" });
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        /// <summary>
        /// Deactivate user account
        /// </summary>
        /// <returns>Success message</returns>
        /// <response code="200">Account deactivated successfully</response>
        /// <response code="401">Unauthorized</response>
        /// <response code="404">User not found</response>
        [HttpPost("deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeactivateAccount()
        {
            try
            {
                var userId = _tokenService.GetUserIdFromToken(User);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return Ok(new { message = "Account deactivated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }
    }
}
