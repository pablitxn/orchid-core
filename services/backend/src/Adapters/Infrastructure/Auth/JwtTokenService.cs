using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.User.Common;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public class JwtTokenService : ITokenService
{
    private readonly string _audience;
    private readonly double _expireHours;
    private readonly string _issuer;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IConfiguration config)
    {
        var secretKey = config["Jwt:Key"];
        if (string.IsNullOrEmpty(secretKey))
            throw new InvalidOperationException("JWT secret key 'Jwt:Key' is not configured.");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        _issuer = config["Jwt:Issuer"]
                  ?? throw new InvalidOperationException("JWT issuer 'Jwt:Issuer' is not configured.");
        _audience = config["Jwt:Audience"]
                    ?? throw new InvalidOperationException("JWT audience 'Jwt:Audience' is not configured.");
        if (!double.TryParse(config["Jwt:ExpireHours"], out _expireHours)) _expireHours = 24; // default 24 hours
    }

    public string GenerateToken(UserEntity user, IEnumerable<string> roles)
    {
        return GenerateTokenInternal(user.Id, user.Email, roles);
    }

    public string GenerateToken(UserDto user, IEnumerable<string> roles)
    {
        return GenerateTokenInternal(user.Id, user.Email, roles);
    }

    private string GenerateTokenInternal(Guid userId, string? email, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            // Security: Add unique token ID (jti) for blacklist support
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email)
        };

        foreach (var role in roles) claims.Add(new Claim(ClaimTypes.Role, role));

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _issuer,
            _audience,
            claims,
            expires: DateTime.UtcNow.AddHours(_expireHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateServiceAccountToken(ServiceAccountEntity serviceAccount)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, $"service:{serviceAccount.ClientId}"),
            new("type", "service_account"),
            new("client_id", serviceAccount.ClientId),
            new("scopes", JsonSerializer.Serialize(serviceAccount.Scopes))
        };

        foreach (var scope in serviceAccount.Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _issuer,
            _audience,
            claims,
            expires: DateTime.UtcNow.AddHours(1), // Service tokens expire in 1 hour
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
