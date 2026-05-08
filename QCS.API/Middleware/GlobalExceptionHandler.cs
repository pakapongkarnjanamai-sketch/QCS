using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace QCS.API.Middleware
{
    /// <summary>
    /// Global exception handler that translates unhandled exceptions into RFC 7807
    /// <see cref="ProblemDetails"/> responses.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger,
            IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException
                && (httpContext.RequestAborted.IsCancellationRequested || cancellationToken.IsCancellationRequested))
            {
                // Request was cancelled by the caller; treat as handled without error logging noise.
                return true;
            }

            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}",
                Sanitize(httpContext.Request.Method),
                Sanitize(httpContext.Request.Path.Value));

            var (status, title) = MapException(exception);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = $"https://httpstatuses.io/{status}",
                Instance = httpContext.Request.Path
            };

            if (_environment.IsDevelopment())
            {
                problem.Detail = exception.ToString();
            }

            httpContext.Response.StatusCode = status;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);
            return true;
        }

        private static (int Status, string Title) MapException(Exception exception) => exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            OperationCanceledException => (StatusCodes.Status408RequestTimeout, "Request cancelled"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operation not allowed in current state"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        // Strip CR/LF from user-controlled values before they reach log sinks
        // to prevent log-forging (CWE-117).
        private static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace('\r', '_').Replace('\n', '_');
        }
    }
}
