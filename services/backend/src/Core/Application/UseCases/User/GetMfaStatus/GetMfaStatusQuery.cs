using MediatR;

namespace Application.UseCases.User.GetMfaStatus;

/// <summary>
/// Query to get the current MFA status for a user.
/// </summary>
public record GetMfaStatusQuery(Guid UserId) : IRequest<MfaStatusDto>;
