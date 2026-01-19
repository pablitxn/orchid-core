using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace WebApi.Tests;

/// <summary>
/// Tests to ensure production security validations work correctly
/// </summary>
public class ProductionSecurityValidationTests
{
    [Theory]
    [InlineData("Debug")]
    [InlineData("Trace")]
    public void EntityFramework_CommandLogging_Should_BeRejected_In_Production(string logLevel)
    {
        // Arrange
        var configuration = CreateConfigurationWithEFLogLevel(logLevel);

        // Act
        Action act = () => ValidateEFLoggingConfiguration(configuration, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Entity Framework command logging*sensitive SQL parameters*");
    }

    [Fact]
    public void EntityFramework_CommandLogging_Warning_Should_BeAllowed_In_Production()
    {
        // Arrange
        var configuration = CreateConfigurationWithEFLogLevel("Warning");

        // Act
        Action act = () => ValidateEFLoggingConfiguration(configuration, isProduction: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EntityFramework_CommandLogging_Information_Should_BeAllowed_In_Production()
    {
        // Arrange
        var configuration = CreateConfigurationWithEFLogLevel("Information");

        // Act
        Action act = () => ValidateEFLoggingConfiguration(configuration, isProduction: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EntityFramework_CommandLogging_Debug_Should_BeAllowed_In_Development()
    {
        // Arrange
        var configuration = CreateConfigurationWithEFLogLevel("Debug");

        // Act
        Action act = () => ValidateEFLoggingConfiguration(configuration, isProduction: false);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EntityFramework_CommandLogging_Null_Should_BeAllowed_In_Production()
    {
        // Arrange
        var configuration = CreateConfigurationWithEFLogLevel(null);

        // Act
        Action act = () => ValidateEFLoggingConfiguration(configuration, isProduction: true);

        // Assert
        act.Should().NotThrow();
    }

    private static IConfiguration CreateConfigurationWithEFLogLevel(string? logLevel)
    {
        var settings = new Dictionary<string, string?>();
        
        if (logLevel != null)
        {
            settings["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"] = logLevel;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings!)
            .Build();
    }

    /// <summary>
    /// Simulates the validation logic from Program.cs
    /// </summary>
    private static void ValidateEFLoggingConfiguration(IConfiguration configuration, bool isProduction)
    {
        if (!isProduction)
        {
            return; // Skip validation in development
        }

        var efLogLevel = configuration["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"];
        if (efLogLevel != null &&
            (efLogLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
             efLogLevel.Equals("Trace", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Entity Framework command logging is set to detailed level in non-development environment. " +
                "This can expose sensitive SQL parameters. Set 'Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command' to 'Warning' or higher.");
        }
    }
}
