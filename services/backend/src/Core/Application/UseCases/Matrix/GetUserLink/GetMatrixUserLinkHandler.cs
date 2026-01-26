using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Matrix.GetUserLink;

public sealed class GetMatrixUserLinkHandler(IMatrixUserLinkRepository matrixUserLinkRepository)
    : IRequestHandler<GetMatrixUserLinkQuery, GetMatrixUserLinkDto>
{
    public async Task<GetMatrixUserLinkDto> Handle(
        GetMatrixUserLinkQuery request,
        CancellationToken cancellationToken)
    {
        var link = await matrixUserLinkRepository.GetByMatrixUserIdWithUserAsync(
            request.MatrixUserId,
            cancellationToken);

        if (link is not { IsActive: true })
        {
            return GetMatrixUserLinkDto.NotLinked();
        }

        var userName = link.User?.Name ?? link.User?.Email ?? "Unknown";
        return GetMatrixUserLinkDto.LinkedTo(link.UserId, userName);
    }
}
