var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "TaskFlow API");

app.Run();

// Exposed for WebApplicationFactory-based tests.
public partial class Program;
