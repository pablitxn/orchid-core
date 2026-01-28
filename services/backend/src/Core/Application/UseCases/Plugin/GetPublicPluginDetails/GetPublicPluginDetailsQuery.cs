using MediatR;

namespace Application.UseCases.Plugin.GetPublicPluginDetails;

public sealed record GetPublicPluginDetailsQuery(Guid PluginId) : IRequest<PublicPluginDetailsDto?>;
