using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Api.Middleware;

/// <summary>Single place that writes ProblemDetails so middleware, auth events and model binding all match.</summary>
public static class ProblemResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        problem.Type ??= $"https://httpstatuses.io/{problem.Status}";
        problem.Instance ??= context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, problem.GetType(), JsonOptions));
    }
}
