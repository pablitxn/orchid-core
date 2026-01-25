using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Matrix.ValidateLinkCode;

public sealed class ValidateMatrixLinkCodeHandler(
    IMatrixLinkCodeRepository matrixLinkCodeRepository,
    IMatrixUserLinkRepository matrixUserLinkRepository)
    : IRequestHandler<ValidateMatrixLinkCodeCommand, ValidateMatrixLinkCodeDto>
{
    public async Task<ValidateMatrixLinkCodeDto> Handle(
        ValidateMatrixLinkCodeCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the code in DB
        var linkCode = await matrixLinkCodeRepository.GetByCodeAsync(request.Code, cancellationToken);

        if (linkCode == null)
        {
            return ValidateMatrixLinkCodeDto.InvalidCode();
        }

        // 2. Check if code was already used
        if (linkCode.IsUsed)
        {
            return ValidateMatrixLinkCodeDto.CodeAlreadyUsed();
        }

        // 3. Check if code is expired
        if (linkCode.IsExpired)
        {
            return ValidateMatrixLinkCodeDto.ExpiredCode();
        }

        // 4. Check if MatrixUserId is already linked to another account
        var existingLink = await matrixUserLinkRepository.GetByMatrixUserIdAsync(request.MatrixUserId, cancellationToken);
        if (existingLink is { IsActive: true })
        {
            return ValidateMatrixLinkCodeDto.AlreadyLinked();
        }

        // 5. Create the MatrixUserLinkEntity
        var newLink = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = linkCode.UserId,
            MatrixUserId = request.MatrixUserId,
            LinkedAt = DateTime.UtcNow,
            IsActive = true
        };

        await matrixUserLinkRepository.CreateAsync(newLink, cancellationToken);

        // 6. Mark code as used
        linkCode.MarkAsUsed();
        await matrixLinkCodeRepository.UpdateAsync(linkCode, cancellationToken);

        // 7. Return user data
        var userName = linkCode.User?.Name ?? linkCode.User?.Email ?? "Unknown";
        return ValidateMatrixLinkCodeDto.Success(linkCode.UserId, userName);
    }
}
