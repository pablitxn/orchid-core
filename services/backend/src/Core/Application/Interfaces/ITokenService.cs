using Application.UseCases.User.Common;
using Domain.Entities;

namespace Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(UserEntity user, IEnumerable<string> roles);
    string GenerateToken(UserDto user, IEnumerable<string> roles);
    string GenerateServiceAccountToken(ServiceAccountEntity serviceAccount);
}