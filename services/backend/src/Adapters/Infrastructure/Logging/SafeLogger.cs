using System.Text.RegularExpressions;
using System.Reflection;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using LogLevel = Application.Interfaces.LogLevel;

namespace Infrastructure.Logging;

/// <summary>
/// Implementation of ISafeLogger that redacts sensitive information for GDPR compliance.
/// </summary>
public sealed class SafeLogger(ILogger logger, SafeLoggerOptions? options = null) : ISafeLogger
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SafeLoggerOptions _options = options ?? new SafeLoggerOptions();

    // Regex patterns for sensitive data
    private static readonly Regex EmailPattern = new(
        @"\b[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhonePattern = new(
        @"\b(?:\+1[-.\s]?)?(?:\(\d{3}\)|\d{3})[-.\s]?\d{3}[-.\s]?\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex CreditCardPattern = new(
        @"\b(?:\d[ -]*?){13,16}\b",
        RegexOptions.Compiled);

    private static readonly Regex SsnPattern = new(
        @"\b(?!000|666|9\d{2})\d{3}-(?!00)\d{2}-(?!0000)\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex IpAddressPattern = new(
        @"\b(?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\b",
        RegexOptions.Compiled);

    public void LogSafe(LogLevel level, string message, params object[] args)
    {
        var redactedMessage = RedactSensitiveData(message);
        var redactedArgs = args.Select(RedactIfSensitive).ToArray();

        var msLevel = ConvertLogLevel(level);
        _logger.Log(msLevel, redactedMessage, redactedArgs);
    }

    public void LogSafeError(Exception exception, string message, params object[] args)
    {
        var redactedMessage = RedactSensitiveData(message);
        var redactedArgs = args.Select(RedactIfSensitive).ToArray();
        var redactedException = RedactException(exception);

        _logger.LogError(redactedException, redactedMessage, redactedArgs);
    }

    public string RedactSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = input;

        // Redact emails
        result = EmailPattern.Replace(result, "***@***.***");

        // Redact phone numbers
        result = PhonePattern.Replace(result, "***-***-****");

        // Redact credit card numbers
        result = CreditCardPattern.Replace(result, "****-****-****-****");

        // Redact SSNs
        result = SsnPattern.Replace(result, "***-**-****");

        // Redact IP addresses
        result = IpAddressPattern.Replace(result, "***.***.***.***");

        // Truncate long text
        if (result.Length > _options.MaxTextLength)
        {
            result = result.Substring(0, _options.MaxTextLength) + "...[TRUNCATED]";
        }

        return result;
    }

    public object RedactNumericValue(object value)
    {
        return value switch
        {
            decimal d => RoundDecimal(d),
            double d => RoundDouble(d),
            float f => RoundFloat(f),
            int i when Math.Abs(i) > _options.NumericThreshold => RoundInt(i),
            long l when Math.Abs(l) > _options.NumericThreshold => RoundLong(l),
            _ => value
        };
    }

    private object RedactIfSensitive(object value)
    {
        return value switch
        {
            string s => RedactSensitiveData(s),
            decimal or double or float or int or long => RedactNumericValue(value),
            Exception ex => RedactException(ex),
            _ => value
        };
    }

    private Exception RedactException(Exception exception)
    {
        if (exception == null)
            return null!;

        // Redact the exception message in-place to preserve type and stack trace
        var redactedMessage = RedactSensitiveData(exception.Message);
        try
        {
            var messageField = typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic)
                                ?? typeof(Exception).GetField("m_message", BindingFlags.Instance | BindingFlags.NonPublic);
            if (messageField != null)
            {
                messageField.SetValue(exception, redactedMessage);
            }
        }
        catch
        {
            // If reflection fails, skip modifying the message
        }

        // Recursively redact inner exceptions
        if (exception is AggregateException aggEx)
        {
            foreach (var inner in aggEx.InnerExceptions)
            {
                RedactException(inner);
            }
        }
        else if (exception.InnerException != null)
        {
            RedactException(exception.InnerException);
        }

        return exception;
    }

    private decimal RoundDecimal(decimal value)
    {
        if (Math.Abs(value) <= (decimal)_options.NumericThreshold)
            return value;

        var magnitude = (int)Math.Floor(Math.Log10((double)Math.Abs(value)));
        var roundTo = Math.Max(0, magnitude - 2);
        return Math.Round(value / (decimal)Math.Pow(10, roundTo)) * (decimal)Math.Pow(10, roundTo);
    }

    private double RoundDouble(double value)
    {
        if (Math.Abs(value) <= _options.NumericThreshold)
            return value;

        var magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var roundTo = Math.Max(0, magnitude - 2);
        return Math.Round(value / Math.Pow(10, roundTo)) * Math.Pow(10, roundTo);
    }

    private float RoundFloat(float value)
    {
        return (float)RoundDouble(value);
    }

    private int RoundInt(int value)
    {
        if (Math.Abs(value) <= _options.NumericThreshold)
            return value;

        var magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var roundTo = Math.Max(0, magnitude - 2);
        var divisor = (int)Math.Pow(10, roundTo);
        return (value / divisor) * divisor;
    }

    private long RoundLong(long value)
    {
        if (Math.Abs(value) <= _options.NumericThreshold)
            return value;

        var magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var roundTo = Math.Max(0, magnitude - 2);
        var divisor = (long)Math.Pow(10, roundTo);
        return (value / divisor) * divisor;
    }

    private static Microsoft.Extensions.Logging.LogLevel ConvertLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }
}

/// <summary>
/// Configuration options for SafeLogger.
/// </summary>
public sealed class SafeLoggerOptions
{
    private int _maxTextLength = 128;
    /// <summary>
    /// Maximum length of text before truncation.
    /// </summary>
    public int MaxTextLength
    {
        get => _maxTextLength;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxTextLength), "MaxTextLength must be positive.");
            _maxTextLength = value;
        }
    }

    private double _numericThreshold = 100;
    /// <summary>
    /// Numeric values below this threshold are not redacted.
    /// </summary>
    public double NumericThreshold
    {
        get => _numericThreshold;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(NumericThreshold), "NumericThreshold must be greater than zero.");
            _numericThreshold = value;
        }
    }
}