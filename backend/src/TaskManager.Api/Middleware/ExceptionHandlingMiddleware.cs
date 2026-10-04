using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Common;

namespace TaskManager.Api.Middleware;

/// <summary>
/// Turns every exception into one consistent RFC 7807 JSON body
/// (type, title, status, detail, traceId, errors?) so the client has a single error shape to handle.
/// </summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nothing to send back.
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(ex, "Exception after response started; cannot write error body.");
                throw;
            }

            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        ProblemDetails problem = ex switch
        {
            ValidationException ve => ValidationProblem(ve),
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Resource not found", ex.Message),
            InvalidCredentialsException => Problem(StatusCodes.Status401Unauthorized, "Authentication failed", ex.Message),
            _ => Problem(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                env.IsDevelopment() ? ex.Message : "Something went wrong. Please try again later.")
        };

        if (problem.Status >= 500)
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("Handled {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        await ProblemResponse.WriteAsync(context, problem);
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ValidationProblemDetails ValidationProblem(ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred."
        };
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
