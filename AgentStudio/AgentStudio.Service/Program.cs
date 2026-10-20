using AgentStudio.Service.Application;
using AgentStudio.Service.Infrastructure;
using AgentStudio.Service.Model;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

try
{
    var elasticUri = configuration["Elasticsearch:Uri"];
    var loggerConfig = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .WriteTo.Console();

    if (!string.IsNullOrEmpty(elasticUri))
    {
        loggerConfig.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
        {
            AutoRegisterTemplate = true,
            IndexFormat = configuration["Elasticsearch:IndexFormat"] ?? "agentstudio-logs-{0:yyyy.MM.dd}"
        });
    }

    Log.Logger = loggerConfig.CreateLogger();
}
catch
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();
}

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
    builder.Services.AddSingleton<IUserIdProvider, KeycloakUserIdProvider>();
    
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Keycloak:Authority"];
            options.Audience = builder.Configuration["Keycloak:Audience"];
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateLifetime = true
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/events"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAgentFlowScope", policy =>
        {
            policy.RequireAuthenticatedUser();
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
    builder.Services.AddScoped<IMcpClientService, McpClientService>();
    builder.Services.AddScoped<IAgentEngine, AgentEngine>();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Npgsql";
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection");

        if (dbProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(connStr))
        {
            options.UseInMemoryDatabase(builder.Configuration["InMemoryDbName"] ?? "AgentStudio_InMemoryDb");
        }
        else
        {
            options.UseNpgsql(connStr);
        }
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            db.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not ensure database is created at startup (PostgreSQL may be offline)");
        }
    }

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

public partial class Program { }
