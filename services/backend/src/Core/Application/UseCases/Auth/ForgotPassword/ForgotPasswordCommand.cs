using MediatR;

namespace Application.UseCases.Auth.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<string?>;