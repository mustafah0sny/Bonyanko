using System.ComponentModel.DataAnnotations;

namespace BonyankopAPI.DTOs
{
    /// <summary>
    /// Update profile data together with an optional uploaded image file
    /// (sent as multipart/form-data).
    /// </summary>
    public class UpdateProfileWithImageDto
    {
        [StringLength(200)]
        public string? FullName { get; set; }

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Optional new profile picture. When provided it replaces any existing image.
        /// </summary>
        public IFormFile? Image { get; set; }
    }
}
