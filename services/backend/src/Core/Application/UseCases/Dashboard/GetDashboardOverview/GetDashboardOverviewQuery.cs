using MediatR;
using Core.Application.UseCases.Dashboard.Common;

namespace Core.Application.UseCases.Dashboard.GetDashboardOverview;

public sealed record GetDashboardOverviewQuery(Guid UserId) : IRequest<DashboardOverviewDto>;