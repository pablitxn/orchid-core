using MediatR;

namespace Application.UseCases.Plugin.ListFeaturedPlugins;

public sealed record ListFeaturedPluginsQuery(int Limit = 10) : IRequest<ListFeaturedPluginsDto>;
