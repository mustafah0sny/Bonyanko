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
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public ProfileController(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
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
                    profilePictureUrl = user.ProfilePictureUrl,
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
                        profilePictureUrl = user.ProfilePictureUrl,
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
