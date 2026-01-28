using MediatR;

namespace Application.UseCases.Plugin.ListPluginCategories;

public sealed record ListPluginCategoriesQuery : IRequest<ListPluginCategoriesDto>;
