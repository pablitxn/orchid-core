using MediatR;
using Core.Application.UseCases.Dashboard.Common;

namespace Core.Application.UseCases.Dashboard.GetSecurityMetrics;

public sealed record GetSecurityMetricsQuery(Guid UserId) : IRequest<SecurityMetricsDto>;