var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "https://localhost", "https://localhost:443", "http://localhost", "http://localhost:80", "http://localhost:5173", "http://localhost:4173", "http://localhost:5000", "http://localhost:5002" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("GatewayCorsPolicy");

app.MapHealthChecks("/health");
app.MapGet("/alive", () => Results.Ok(new
{
    status = "Healthy",
    service = "AgentStudio.Gateway",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/", () => Results.Redirect("/portal/"));

app.MapReverseProxy();

app.Run();

public partial class Program { }
