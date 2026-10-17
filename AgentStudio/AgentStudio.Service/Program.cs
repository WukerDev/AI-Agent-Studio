using AgentStudio.Service.Services;
using AgentStudio.Service.Hubs;
using AgentStudio.Service.Interceptors;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System.Security.Claims;
using AgentStudio.Service.Data;
using AgentStudio.Service.Security;
using Microsoft.EntityFrameworkCore;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(configuration["Elasticsearch:Uri"] ?? "http://localhost:9200"))
    {
        AutoRegisterTemplate = true,
        IndexFormat = configuration["Elasticsearch:IndexFormat"] ?? "agentstudio-logs-{0:yyyy.MM.dd}"
    })
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddValidatorsFromAssemblyContaining<ValidationInterceptor>();

    builder.Services.AddGrpc(options =>
    {
        options.Interceptors.Add<ValidationInterceptor>();
    });
    
    builder.Services.AddSignalR();
    
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Keycloak:Authority"];
            options.Audience = builder.Configuration["Keycloak:Audience"];
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAgentFlowScope", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "agent-flow-write");
        });
    });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
        });
    });
    
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserContext, UserContext>();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseCors("AllowAll");

    app.UseRouting();

    app.UseGrpcWeb();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGrpcService<AgentFlowServiceImplementation>()
       .EnableGrpcWeb()
       .RequireAuthorization("RequireAgentFlowScope");

    app.MapHub<EventsHub>("/events")
       .RequireAuthorization();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}