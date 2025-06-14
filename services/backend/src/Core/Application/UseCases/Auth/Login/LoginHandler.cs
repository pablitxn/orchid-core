using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Auth.Login;

public class LoginHandler(IUserRepository userRepository, ITokenService tokenService, IPasswordHasher passwordHasher)
    : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
            return new LoginResult(false, string.Empty, [], "User not found");

        // Validate password if provided
        if (!string.IsNullOrEmpty(request.Password))
            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
                return new LoginResult(false, string.Empty, [], "Invalid password");

        var roles = user.UserRoles?.Select(ur => ur.Role.Name) ?? [];
        var enumerable = roles as string[] ?? roles.ToArray();
        var token = _tokenService.GenerateToken(user, enumerable);
        return new LoginResult(true, token, enumerable);
    }
}