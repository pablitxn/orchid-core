using Application.UseCases.User.Common;
using Domain.Entities;

namespace Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(UserEntity user, IEnumerable<string> roles, bool rememberMe = false);
    string GenerateToken(UserDto user, IEnumerable<string> roles, bool rememberMe = false);
    string GenerateServiceAccountToken(ServiceAccountEntity serviceAccount);
    
    /// <summary>
    /// Gets the token expiration in hours based on the remember me flag.
    /// </summary>
    double GetTokenExpirationHours(bool rememberMe);
}