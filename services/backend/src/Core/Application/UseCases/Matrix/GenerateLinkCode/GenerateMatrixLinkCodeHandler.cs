using System.Security.Cryptography;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Matrix.GenerateLinkCode;

public sealed class GenerateMatrixLinkCodeHandler(
    IMatrixUserLinkRepository matrixUserLinkRepository,
    IMatrixLinkCodeRepository matrixLinkCodeRepository)
    : IRequestHandler<GenerateMatrixLinkCodeCommand, GenerateMatrixLinkCodeDto>
{
    private const string CodePrefix = "ORCHID-";
    private const int CodeSuffixLength = 4;
    private const string CodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public async Task<GenerateMatrixLinkCodeDto> Handle(
        GenerateMatrixLinkCodeCommand request,
        CancellationToken cancellationToken)
    {
        // Check if user already has an active Matrix link
        var existingLink = await matrixUserLinkRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existingLink is { IsActive: true })
        {
            return GenerateMatrixLinkCodeDto.AlreadyLinked(existingLink.MatrixUserId);
        }

        // Invalidate any existing active codes for this user
        var existingCode = await matrixLinkCodeRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken);
        if (existingCode != null)
        {
            existingCode.MarkAsUsed();
            await matrixLinkCodeRepository.UpdateAsync(existingCode, cancellationToken);
        }

        // Generate a unique code
        var code = await GenerateUniqueCodeAsync(cancellationToken);

        // Create the link code entity
        var expiresAt = DateTime.UtcNow.AddMinutes(MatrixLinkCodeEntity.ExpirationMinutes);
        var linkCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Code = code,
            ExpiresAt = expiresAt
        };

        await matrixLinkCodeRepository.CreateAsync(linkCode, cancellationToken);

        return GenerateMatrixLinkCodeDto.Success(code, expiresAt);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateCode();
            var exists = await matrixLinkCodeRepository.CodeExistsAsync(code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        // Fallback: include a timestamp component to ensure uniqueness
        return GenerateCode() + DateTime.UtcNow.Ticks.ToString("X")[^2..];
    }

    private static string GenerateCode()
    {
        Span<char> suffix = stackalloc char[CodeSuffixLength];
        for (var i = 0; i < CodeSuffixLength; i++)
        {
            suffix[i] = CodeCharacters[RandomNumberGenerator.GetInt32(CodeCharacters.Length)];
        }
        return CodePrefix + new string(suffix);
    }
}
