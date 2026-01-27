using System.Text.RegularExpressions;
using Domain.Exceptions;

namespace Application.UseCases.OpenApiPlugin.Common;

/// <summary>
/// Validator for OpenAPI plugin commands.
/// </summary>
public static partial class OpenApiPluginValidator
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;
    private const int MaxSystemNameLength = 50;
    private const int MinTimeoutSeconds = 1;
    private const int MaxTimeoutSeconds = 120;

    [GeneratedRegex(@"^[a-z][a-z0-9_]*$")]
    private static partial Regex SystemNameRegex();

    public static void ValidateCreate(
        string name,
        string systemName,
        string baseUrl,
        string? openApiSpecUrl,
        int? timeoutSeconds,
        string? description)
    {
        ValidateName(name);
        ValidateSystemName(systemName);
        ValidateBaseUrl(baseUrl);
        ValidateOpenApiSpecUrl(openApiSpecUrl);
        ValidateTimeoutSeconds(timeoutSeconds);
        ValidateDescription(description);
    }

    public static void ValidateUpdate(
        string? name,
        string? baseUrl,
        string? openApiSpecUrl,
        int? timeoutSeconds,
        string? description)
    {
        if (name is not null)
            ValidateName(name);

        if (baseUrl is not null)
            ValidateBaseUrl(baseUrl);

        if (openApiSpecUrl is not null)
            ValidateOpenApiSpecUrl(openApiSpecUrl);

        ValidateTimeoutSeconds(timeoutSeconds);

        if (description is not null)
            ValidateDescription(description);
    }

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Name is required.", nameof(name));

        if (name.Length > MaxNameLength)
            throw new ValidationException(
                $"Name cannot exceed {MaxNameLength} characters.",
                nameof(name));
    }

    public static void ValidateSystemName(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            throw new ValidationException("SystemName is required.", nameof(systemName));

        if (systemName.Length > MaxSystemNameLength)
            throw new ValidationException(
                $"SystemName cannot exceed {MaxSystemNameLength} characters.",
                nameof(systemName));

        if (!SystemNameRegex().IsMatch(systemName))
            throw new ValidationException(
                "SystemName must be lowercase, start with a letter, and contain only letters, numbers, and underscores.",
                nameof(systemName));
    }

    public static void ValidateBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ValidationException("BaseUrl is required.", nameof(baseUrl));

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            throw new ValidationException("BaseUrl must be a valid absolute URL.", nameof(baseUrl));

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationException(
                "BaseUrl must use HTTP or HTTPS scheme.",
                nameof(baseUrl));
    }

    public static void ValidateOpenApiSpecUrl(string? openApiSpecUrl)
    {
        if (string.IsNullOrEmpty(openApiSpecUrl))
            return;

        if (!openApiSpecUrl.StartsWith('/'))
            throw new ValidationException(
                "OpenApiSpecUrl must be a relative path starting with '/'.",
                nameof(openApiSpecUrl));
    }

    public static void ValidateTimeoutSeconds(int? timeoutSeconds)
    {
        if (timeoutSeconds is null)
            return;

        if (timeoutSeconds < MinTimeoutSeconds || timeoutSeconds > MaxTimeoutSeconds)
            throw new ValidationException(
                $"TimeoutSeconds must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds}.",
                nameof(timeoutSeconds));
    }

    public static void ValidateDescription(string? description)
    {
        if (description?.Length > MaxDescriptionLength)
            throw new ValidationException(
                $"Description cannot exceed {MaxDescriptionLength} characters.",
                nameof(description));
    }
}
