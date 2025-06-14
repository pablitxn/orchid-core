using MediatR;

namespace Application.UseCases.Auth.ResetPassword;

public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<bool>;