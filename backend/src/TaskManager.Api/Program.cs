using TaskManager.Api.Extensions;
using TaskManager.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPersistence(builder.Configuration)
    .AddApplicationServices()
    .AddApiControllers()
    .AddJwtAuthentication(builder.Configuration)
    .AddSwaggerWithJwt();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger stays on in every environment for this assessment so reviewers can try the API
// from the Docker stack. A production deployment would gate it.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based tests.
public partial class Program;
