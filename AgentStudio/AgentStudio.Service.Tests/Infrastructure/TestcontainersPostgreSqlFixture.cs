using Testcontainers.PostgreSql;
using Xunit;

namespace AgentStudio.Service.Tests.Infrastructure;

public class TestcontainersPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;

    public bool IsDockerAvailable { get; private set; }
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            this.container = new PostgreSqlBuilder()
                .WithImage("postgres:15-alpine")
                .WithDatabase("agentstudio_tests")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await this.container.StartAsync(cts.Token);
            this.ConnectionString = this.container.GetConnectionString();
            this.IsDockerAvailable = true;
        }
        catch (Exception)
        {
            this.IsDockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (this.container != null)
        {
            try
            {
                await this.container.DisposeAsync();
            }
            catch
            {
            }
        }
    }
}
