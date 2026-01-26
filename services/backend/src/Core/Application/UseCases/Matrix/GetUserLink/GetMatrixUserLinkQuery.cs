using MediatR;

namespace Application.UseCases.Matrix.GetUserLink;

/// <summary>
/// Query to check if a Matrix user is linked to an Orchid account.
/// </summary>
public sealed record GetMatrixUserLinkQuery(string MatrixUserId) : IRequest<GetMatrixUserLinkDto>;

/// <summary>
/// Result containing the link status and user info if linked.
/// </summary>
public sealed record GetMatrixUserLinkDto
{
    public bool Linked { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }

    public static GetMatrixUserLinkDto NotLinked() => new() { Linked = false };

    public static GetMatrixUserLinkDto LinkedTo(Guid userId, string userName) => new()
    {
        Linked = true,
        UserId = userId,
        UserName = userName
    };
}
