using System.Security.Claims;
using AgentStudio.Service.Application;
using AgentStudio.Service.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Elasticsearch;

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
    builder.Services.AddHealthChecks();

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
                        return Task.CompletedTask;
                    }

                    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                    var hasBearerToken = !string.IsNullOrWhiteSpace(authHeader) &&
                                         authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                                         authHeader.Length > "Bearer ".Length;

                    var allowGuestFallback = builder.Configuration.GetValue("Auth:AllowGuestFallback", true);
                    if (!hasBearerToken && allowGuestFallback)
                    {
                        var guestClaims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "guest-user"),
                            new Claim("sub", "guest-user"),
                            new Claim("preferred_username", "Admin"),
                            new Claim(ClaimTypes.Name, "Admin"),
                            new Claim("scope", "agent-flow-write")
                        };
                        var identity = new ClaimsIdentity(guestClaims, JwtBearerDefaults.AuthenticationScheme);
                        context.Principal = new ClaimsPrincipal(identity);
                        context.Success();
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
            if (db.Database.IsRelational())
            {
                var creator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>(db.Database);
                try
                {
                    creator.CreateTables();
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
                {
                    // Tables already exist
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "CreateTables check: {Message}", ex.Message);
                }
            }

            WorkflowSeeder.SeedSampleWorkflowsAsync(db).GetAwaiter().GetResult();
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

    app.MapHealthChecks("/health");
    app.MapGet("/metrics", () => Results.Text(AgentStudioTelemetry.GetPrometheusMetricsText(), "text/plain; version=0.0.4; charset=utf-8"));
    app.MapGet("/api/telemetry", () => Results.Ok(AgentStudioTelemetry.GetSnapshot()));

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
