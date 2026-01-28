using Application.UseCases.Publisher.Common;
using MediatR;

namespace Application.UseCases.Publisher.ListPublisherPlugins;

public record ListPublisherPluginsQuery(Guid UserId) : IRequest<List<PublisherPluginDto>>;
