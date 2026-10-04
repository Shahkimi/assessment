using TaskManager.Api.Extensions;
using TaskManager.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPersistence(builder.Configuration)
    .AddApplicationServices()
    .AddApiControllers();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based tests.
public partial class Program;
