using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sentry;

namespace WebApi.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Get correlation ID from context items (set by CorrelationIdMiddleware)
            var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString()
                                ?? context.TraceIdentifier;

            // Only capture unexpected 5xx errors to Sentry (not expected 4xx client errors)
            if (!IsExpectedClientError(ex))
            {
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("correlation_id", correlationId);
                    scope.SetExtra("request_path", context.Request.Path.ToString());
                    scope.SetExtra("request_method", context.Request.Method);
                });
                SentrySdk.CaptureException(ex);
            }

            _logger.LogError(ex, "An unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);
            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";

        var problemDetails = exception switch
        {
            InvalidOperationException invalidOp when invalidOp.Message.Contains("Insufficient credits") =>
                CreateProblemDetails(
                    HttpStatusCode.PaymentRequired,
                    "Insufficient Credits",
                    "You don't have enough credits to complete this action.",
                    "INSUFFICIENT_CREDITS"),

            InvalidOperationException invalidOp when invalidOp.Message.Contains("already purchased") =>
                CreateProblemDetails(
                    HttpStatusCode.Conflict,
                    "Already Purchased",
                    "You have already purchased this item.",
                    "ALREADY_PURCHASED"),

            KeyNotFoundException _ =>
                CreateProblemDetails(
                    HttpStatusCode.NotFound,
                    "Resource Not Found",
                    "The requested resource was not found.",
                    "NOT_FOUND"),

            UnauthorizedAccessException _ =>
                CreateProblemDetails(
                    HttpStatusCode.Forbidden,
                    "Forbidden",
                    "You are not authorized to perform this action.",
                    "FORBIDDEN"),

            ArgumentException argEx =>
                CreateProblemDetails(
                    HttpStatusCode.BadRequest,
                    "Invalid Request",
                    argEx.Message,
                    "BAD_REQUEST"),

            _ => CreateProblemDetails(
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An error occurred while processing your request.",
                "INTERNAL_ERROR")
        };

        // Add correlation ID for distributed tracing
        problemDetails.Extensions["correlationId"] = correlationId;

        // Add stack trace in development
        if (_environment.IsDevelopment() && exception.StackTrace != null)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        context.Response.StatusCode = problemDetails.Status ?? 500;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static ProblemDetails CreateProblemDetails(
        HttpStatusCode statusCode,
        string title,
        string detail,
        string errorCode)
    {
        return new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Extensions = { ["errorCode"] = errorCode }
        };
    }

    private static bool IsExpectedClientError(Exception ex) =>
        ex is KeyNotFoundException
            or UnauthorizedAccessException
            or ArgumentException
            || (ex is InvalidOperationException invalidOp &&
                (invalidOp.Message.Contains("Insufficient credits") ||
                 invalidOp.Message.Contains("already purchased")));
}