using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.User.GetMfaStatus;

public class GetMfaStatusHandler(IUserRepository userRepository)
    : IRequestHandler<GetMfaStatusQuery, MfaStatusDto>
{
    public async Task<MfaStatusDto> Handle(
        GetMfaStatusQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);

        if (user == null)
            throw new UserNotFoundException(request.UserId);

        return new MfaStatusDto(
            Enabled: user.MfaEnabled,
            Method: user.MfaMethod,
            EnrolledAt: user.MfaEnrolledAt,
            LastVerifiedAt: user.MfaLastVerifiedAt);
    }
}
