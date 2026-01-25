using MediatR;

namespace Application.UseCases.Matrix.GenerateLinkCode;

public sealed record GenerateMatrixLinkCodeCommand(Guid UserId) : IRequest<GenerateMatrixLinkCodeDto>;

public sealed record GenerateMatrixLinkCodeDto(
    bool IsSuccess,
    string? Code = null,
    DateTime? ExpiresAt = null,
    string? ErrorCode = null,
    string? MatrixUserId = null)
{
    public static GenerateMatrixLinkCodeDto Success(string code, DateTime expiresAt)
        => new(true, code, expiresAt);

    public static GenerateMatrixLinkCodeDto AlreadyLinked(string matrixUserId)
        => new(false, ErrorCode: "ALREADY_LINKED", MatrixUserId: matrixUserId);
}
