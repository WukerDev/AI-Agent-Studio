using AgentStudio.Service.Infrastructure;
using AgentStudio.Service.Protos;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentStudio.Service.Tests.Infrastructure;

public class AgentStudioTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly TestcontainersPostgreSqlFixture dbFixture = new();

    public bool IsUsingTestcontainers => this.dbFixture.IsDockerAvailable;

    public async Task InitializeAsync()
    {
        await this.dbFixture.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await this.dbFixture.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testConfig = new Dictionary<string, string?>();

            if (this.dbFixture.IsDockerAvailable && !string.IsNullOrEmpty(this.dbFixture.ConnectionString))
            {
                testConfig["DatabaseProvider"] = "Npgsql";
                testConfig["ConnectionStrings:DefaultConnection"] = this.dbFixture.ConnectionString;
            }
            else
            {
                testConfig["DatabaseProvider"] = "InMemory";
                testConfig["InMemoryDbName"] = "TestDb_" + Guid.NewGuid();
            }

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.AuthenticationScheme,
                _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        }

        return host;
    }

    public AgentFlowService.AgentFlowServiceClient CreateGrpcClient()
    {
        var client = this.CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client
        });

        return new AgentFlowService.AgentFlowServiceClient(channel);
    }

    public HubConnection CreateSignalRConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(this.Server.BaseAddress, "/events"), options =>
            {
                options.HttpMessageHandlerFactory = _ => this.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>("test-token");
            })
            .Build();
    }
}
