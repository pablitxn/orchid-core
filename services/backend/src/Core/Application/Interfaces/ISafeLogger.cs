namespace Application.Interfaces;

/// <summary>
/// Logger that automatically redacts sensitive information for GDPR compliance.
/// </summary>
public interface ISafeLogger
{
    /// <summary>
    /// Logs a message with automatic redaction of sensitive data.
    /// </summary>
    void LogSafe(LogLevel level, string message, params object[] args);

    /// <summary>
    /// Logs an error with automatic redaction of sensitive data.
    /// </summary>
    void LogSafeError(Exception exception, string message, params object[] args);

    /// <summary>
    /// Redacts sensitive information from a string.
    /// </summary>
    string RedactSensitiveData(string input);

    /// <summary>
    /// Redacts numeric values by rounding or masking.
    /// </summary>
    object RedactNumericValue(object value);
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}