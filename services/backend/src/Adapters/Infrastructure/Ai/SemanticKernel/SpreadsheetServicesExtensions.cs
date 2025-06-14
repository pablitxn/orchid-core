using Application.Interfaces.Spreadsheet;
using Infrastructure.Ai.SemanticKernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Ai.SemanticKernel;

/// <summary>
/// Extension methods for registering spreadsheet services
/// </summary>
public static class SpreadsheetServicesExtensions
{
    /// <summary>
    /// Adds spreadsheet analysis and execution services to the DI container
    /// </summary>
    public static IServiceCollection AddSpreadsheetServices(this IServiceCollection services)
    {
        // Register core services
        services.AddScoped<ISpreadsheetAnalysisService, SpreadsheetAnalysisService>();
        services.AddScoped<ISandboxManagementService, SandboxManagementService>();
        services.AddScoped<IFormulaExecutionService, FormulaExecutionService>();

        return services;
    }
}