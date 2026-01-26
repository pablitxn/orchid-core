using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.GetCreditPackages;

public sealed record GetCreditPackagesQuery : IRequest<List<CreditPackageDto>>;
