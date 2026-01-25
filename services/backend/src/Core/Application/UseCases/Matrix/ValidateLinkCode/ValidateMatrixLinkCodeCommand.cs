using MediatR;

namespace Application.UseCases.Matrix.ValidateLinkCode;

public sealed record ValidateMatrixLinkCodeCommand(
    string MatrixUserId,
    string Code) : IRequest<ValidateMatrixLinkCodeDto>;

public sealed record ValidateMatrixLinkCodeDto(
    bool IsSuccess,
    Guid? UserId = null,
    string? UserName = null,
    string? ErrorCode = null)
{
    public static ValidateMatrixLinkCodeDto Success(Guid userId, string userName)
        => new(true, userId, userName);

    public static ValidateMatrixLinkCodeDto InvalidCode()
        => new(false, ErrorCode: "INVALID_CODE");

    public static ValidateMatrixLinkCodeDto ExpiredCode()
        => new(false, ErrorCode: "EXPIRED_CODE");

    public static ValidateMatrixLinkCodeDto AlreadyLinked()
        => new(false, ErrorCode: "ALREADY_LINKED");

    public static ValidateMatrixLinkCodeDto CodeAlreadyUsed()
        => new(false, ErrorCode: "CODE_ALREADY_USED");
}
