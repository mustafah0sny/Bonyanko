using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BonyankopAPI.Interfaces;
using BonyankopAPI.Models;

namespace BonyankopAPI.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"]!);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public Guid GetUserIdFromToken(ClaimsPrincipal user)
        {
            // Try all NameIdentifier claims — JWT middleware may remap or duplicate them
            foreach (var claim in user.FindAll(ClaimTypes.NameIdentifier))
            {
                if (Guid.TryParse(claim.Value, out var guid))
                    return guid;
            }

            // Fallback: check the raw "nameid" claim used by some JWT handlers
            var nameIdClaim = user.FindFirst("nameid");
            if (nameIdClaim != null && Guid.TryParse(nameIdClaim.Value, out var nameIdGuid))
                return nameIdGuid;

            throw new UnauthorizedAccessException("User ID not found in token");
        }
    }
}
