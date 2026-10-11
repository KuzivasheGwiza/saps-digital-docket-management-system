using Microsoft.IdentityModel.Tokens;
using SAPS.DDMS.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SAPS.DDMS.API.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _configuration;

        public JwtHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateJwtToken(User user, string role, int expiryMinutes)
        {
            var jwtKey = _configuration["JwtSettings:SecretKey"] ?? _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["JwtSettings:Issuer"] ?? _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["JwtSettings:Audience"] ?? _configuration["Jwt:Audience"];

            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
            {
                throw new InvalidOperationException("JWT SecretKey is missing or too short. Please check appsettings.json");
            }

            if (string.IsNullOrEmpty(jwtIssuer))
            {
                throw new InvalidOperationException("JWT Issuer is missing. Please check appsettings.json");
            }

            if (string.IsNullOrEmpty(jwtAudience))
            {
                throw new InvalidOperationException("JWT Audience is missing. Please check appsettings.json");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserID.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.EmailAddress ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty),
                new Claim("userId", user.UserID.ToString()),
                new Claim("personnelNumber", user.Username ?? string.Empty),
                new Claim("role", role ?? string.Empty),
                new Claim("stationId", user.FK_StationID.ToString()),
                new Claim(ClaimTypes.Role, role ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Math.Max(1, expiryMinutes)),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
