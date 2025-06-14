using Application.Interfaces.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Application.UseCases.Spreadsheet.Compression;

/// <summary>
/// Extension methods for registering spreadsheet compression services.
/// </summary>
public static class SpreadsheetCompressionServiceCollectionExtensions
{
    /// <summary>
    /// Registers all spreadsheet compression services.
    /// </summary>
    public static IServiceCollection AddSpreadsheetCompression(this IServiceCollection services)
    {
        // Core compression services
        services.AddSingleton<IInvertedIndexTranslator, InvertedIndexTranslator>();
        services.AddSingleton<IFormatAwareAggregator, FormatAwareAggregator>();
        
        // Type recognizers (avoid duplicate registrations)
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, DateTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, PercentageTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, CurrencyTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, ScientificTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, TimeTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, FractionTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, AccountingTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, BooleanTypeRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeRecognizer, NumberTypeRecognizer>());
        
        // Pipeline builder factory
        services.AddScoped<ISpreadsheetPipelineBuilder>(provider => 
            new SpreadsheetPipelineBuilder(provider));
        
        return services;
    }
    
    /// <summary>
    /// Registers spreadsheet compression services with custom type recognizers.
    /// </summary>
    public static IServiceCollection AddSpreadsheetCompression<TCustomRecognizer>(
        this IServiceCollection services) 
        where TCustomRecognizer : class, ITypeRecognizer
    {
        services.AddSpreadsheetCompression();
        services.AddSingleton<ITypeRecognizer, TCustomRecognizer>();
        
        return services;
    }
}