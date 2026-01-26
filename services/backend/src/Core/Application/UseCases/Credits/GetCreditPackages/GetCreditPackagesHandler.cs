using Application.Interfaces;
using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.GetCreditPackages;

public sealed class GetCreditPackagesHandler(
    ICreditPackageRepository creditPackageRepository)
    : IRequestHandler<GetCreditPackagesQuery, List<CreditPackageDto>>
{
    public async Task<List<CreditPackageDto>> Handle(
        GetCreditPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var packages = await creditPackageRepository.GetActivePackagesAsync(cancellationToken);

        return packages
            .OrderBy(p => p.DisplayOrder)
            .Select(CreditPackageDto.FromEntity)
            .ToList();
    }
}
