using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Repositories;
using TaskManager.Api.Services;

namespace TaskManager.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set the ConnectionStrings__Default environment variable.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

        services.AddScoped<ITaskRepository, TaskRepository>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITaskService, TaskService>();
        services.AddValidatorsFromAssemblyContaining<Program>();
        return services;
    }

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

        // Malformed JSON, unknown enum text or bad date formats fail before our code runs.
        // Reshape those 400s so they look like every other error (and hide internal type names).
        services.Configure<ApiBehaviorOptions>(o =>
            o.InvalidModelStateResponseFactory = ctx =>
            {
                var parameterNames = ctx.ActionDescriptor.Parameters
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var errors = new Dictionary<string, string[]>();
                foreach (var (key, entry) in ctx.ModelState)
                {
                    if (entry.Errors.Count == 0) continue;

                    // "request: The request field is required." is a binder artefact, not a field error.
                    if (parameterNames.Contains(key)) continue;

                    var field = FieldName(key);
                    errors[field] = entry.Errors.Select(e => FriendlyMessage(field, e.ErrorMessage)).Distinct().ToArray();
                }

                if (errors.Count == 0)
                    errors["body"] = ["A JSON request body is required."];

                var problem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed",
                    Detail = "The request body is missing or malformed."
                };
                problem.Type = "https://httpstatuses.io/400";
                problem.Instance = ctx.HttpContext.Request.Path;
                problem.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

                return new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
            });

        return services;
    }

    // System.Text.Json model errors are keyed like "$.priority"; whole-body errors as "" or "$".
    private static string FieldName(string key)
    {
        var name = key.TrimStart('$', '.');
        return name.Length == 0 ? "body" : name;
    }

    private static string FriendlyMessage(string field, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "The value is invalid.";
        if (field == "body") return "Request body is not valid JSON.";
        if (message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase))
            return $"'{field}' has an invalid value.";
        return message;
    }
}
