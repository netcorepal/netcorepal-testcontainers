namespace Testcontainers.DMDB.UnitTests;

using System.Data;
using System.Data.Common;
using Dm;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Images;

public abstract class DmdbContainerTest
{
    private readonly DmdbDefaultFixture _fixture;

    protected DmdbContainerTest(DmdbDefaultFixture fixture)
    {
        _fixture = fixture;
    }

    [DockerFact]
    public void ConnectionStringContainsExpectedProperties()
    {
        var connectionString = _fixture.GetConnectionString();
        
        Assert.Contains("Host=", connectionString);
        Assert.Contains("Port=", connectionString);
        Assert.Contains("Database=", connectionString);
        Assert.Contains("Username=", connectionString);
        Assert.Contains("Password=", connectionString);
        Assert.Contains("DBAPassword=", connectionString);
        Assert.Contains("Timeout=", connectionString);
    }

    [DockerFact]
    public async Task ConnectionStateReturnsOpen()
    {
        await using DbConnection connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        Assert.Equal(ConnectionState.Open, connection.State);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        Assert.Equal(1, Convert.ToInt32(result));
    }

    public class DmdbDefaultFixture : IAsyncLifetime
    {
        private readonly DmdbBuilder _builder;

        public DmdbDefaultFixture()
        {
            _builder = Configure(new DmdbBuilder());
        }

        public DmdbContainer? Container { get; private set; }

        protected static bool IsDockerEnabled
            => !string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal)
               && !string.Equals(Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS"), "0", StringComparison.Ordinal);

        public virtual DbProviderFactory DbProviderFactory => DmClientFactory.Instance;

        protected virtual DmdbBuilder Configure(DmdbBuilder builder)
        {
            return builder;
        }

        public async Task InitializeAsync()
        {
            if (!IsDockerEnabled)
            {
                return;
            }

            Container = _builder.Build();
            await Container.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (Container is null)
            {
                return;
            }

            await Container.DisposeAsync();
        }

        public string GetConnectionString()
        {
            if (Container is null)
            {
                throw new InvalidOperationException("Docker integration tests are disabled. Unset SKIP_DOCKER_TESTS or set it to 0 to enable.");
            }

            return Container.GetConnectionString();
        }

        public DbConnection CreateConnection()
        {
            if (Container is null)
            {
                throw new InvalidOperationException("Docker integration tests are disabled. Unset SKIP_DOCKER_TESTS or set it to 0 to enable.");
            }

            var connection = DbProviderFactory.CreateConnection();
            if (connection is null)
            {
                throw new InvalidOperationException($"{nameof(DbProviderFactory)} did not create a connection.");
            }

            connection.ConnectionString = Container.GetConnectionString();
            return connection;
        }
    }

    public sealed class DmdbDifferentPasswordFixture : DmdbDefaultFixture
    {
        protected override DmdbBuilder Configure(DmdbBuilder builder)
        {
            return builder.WithPassword("TestDm456").WithDbaPassword("TestDm456");
        }
    }

    public sealed class DmdbCustomWaitStrategyFixture : DmdbDefaultFixture
    {
        protected override DmdbBuilder Configure(DmdbBuilder builder)
        {
            // Demonstrates how consumers can override the default wait strategy.
            return builder.WithWaitStrategy(
                Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(DmdbBuilder.DmdbPort));
        }
    }

    public sealed class DmdbImageTagFixture : DmdbDefaultFixture
    {
        protected override DmdbBuilder Configure(DmdbBuilder builder)
        {
            // Demonstrates how consumers can pin a specific image tag.
            // Defaults to the library's default image to keep CI/dev stable.
            var image = Environment.GetEnvironmentVariable("DMDB_IMAGE") ?? DmdbBuilder.DmdbImage;
            return builder.WithImage(new DockerImage(image));
        }
    }

    public sealed class DmdbDifferentDatabaseFixture : DmdbDefaultFixture
    {
        protected override DmdbBuilder Configure(DmdbBuilder builder)
        {
            return builder
                .WithDatabase("mydb");
        }
    }

    public sealed class DmdbDefaultConfiguration(DmdbDefaultFixture fixture)
        : DmdbContainerTest(fixture), IClassFixture<DmdbDefaultFixture>;

    public sealed class DmdbDifferentPasswordConfiguration(DmdbDifferentPasswordFixture fixture)
        : DmdbContainerTest(fixture), IClassFixture<DmdbDifferentPasswordFixture>;

    public sealed class DmdbCustomWaitStrategyConfiguration(DmdbCustomWaitStrategyFixture fixture)
        : DmdbContainerTest(fixture), IClassFixture<DmdbCustomWaitStrategyFixture>;

    public sealed class DmdbImageTagConfiguration(DmdbImageTagFixture fixture)
        : DmdbContainerTest(fixture), IClassFixture<DmdbImageTagFixture>;

    public sealed class DmdbDifferentDatabaseConfiguration(DmdbDifferentDatabaseFixture fixture)
        : DmdbContainerTest(fixture), IClassFixture<DmdbDifferentDatabaseFixture>;
}
