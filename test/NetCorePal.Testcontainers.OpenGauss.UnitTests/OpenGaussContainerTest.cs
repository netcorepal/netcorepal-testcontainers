namespace Testcontainers.OpenGauss.UnitTests;

using System.Data;
using System.Data.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Images;
using Npgsql;

public abstract class OpenGaussContainerTest
{
    private readonly OpenGaussDefaultFixture _fixture;

    protected OpenGaussContainerTest(OpenGaussDefaultFixture fixture)
    {
        _fixture = fixture;
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

    public class OpenGaussDefaultFixture : IAsyncLifetime
    {
        private readonly OpenGaussBuilder _builder;

        public OpenGaussDefaultFixture()
        {
            _builder = Configure(new OpenGaussBuilder());
        }

        public OpenGaussContainer? Container { get; private set; }

        protected static bool IsDockerEnabled
            => !string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal)
               && !string.Equals(Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS"), "0", StringComparison.Ordinal);

        public virtual DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;

        protected virtual OpenGaussBuilder Configure(OpenGaussBuilder builder)
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

            connection.ConnectionString = Container.GetConnectionString() + ";No Reset On Close=true;";
            return connection;
        }
    }

    public sealed class OpenGaussDifferentPasswordFixture : OpenGaussDefaultFixture
    {
        protected override OpenGaussBuilder Configure(OpenGaussBuilder builder)
        {
            return builder.WithPassword("Test@456");
        }
    }

    public sealed class OpenGaussCustomWaitStrategyFixture : OpenGaussDefaultFixture
    {
        protected override OpenGaussBuilder Configure(OpenGaussBuilder builder)
        {
            // Demonstrates how consumers can override the default wait strategy.
            // We wait from the host side using Npgsql until SELECT 1 succeeds.
            return builder.WithWaitStrategy(
                Wait.ForUnixContainer().AddCustomWaitStrategy(
                    new WaitUntilDatabaseIsAvailable(),
                    waitStrategy => waitStrategy.WithTimeout(TimeSpan.FromMinutes(2))));
        }

        private sealed class WaitUntilDatabaseIsAvailable : IWaitUntil
        {
            public async Task<bool> UntilAsync(DotNet.Testcontainers.Containers.IContainer container)
            {
                try
                {
                    var connectionString = string.Join(";",
                        $"Host={container.Hostname}",
                        $"Port={container.GetMappedPublicPort(OpenGaussBuilder.OpenGaussPort)}",
                        $"Database={OpenGaussBuilder.DefaultDatabase}",
                        $"Username={OpenGaussBuilder.DefaultUsername}",
                        $"Password={OpenGaussBuilder.DefaultPassword}");

                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync().ConfigureAwait(false);

                    await using var command = new NpgsqlCommand("SELECT 1;", connection);
                    _ = await command.ExecuteScalarAsync().ConfigureAwait(false);

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    public sealed class OpenGaussImageTagFixture : OpenGaussDefaultFixture
    {
        protected override OpenGaussBuilder Configure(OpenGaussBuilder builder)
        {
            // Demonstrates how consumers can pin a specific image tag.
            // Defaults to the library's default image to keep CI/dev stable.
            var image = Environment.GetEnvironmentVariable("OPENGAUSS_IMAGE") ?? OpenGaussBuilder.OpenGaussImage;
            return builder.WithImage(new DockerImage(image));
        }
    }

    public sealed class OpenGaussDifferentDatabaseAndUsernameFixture : OpenGaussDefaultFixture
    {
        protected override OpenGaussBuilder Configure(OpenGaussBuilder builder)
        {
            return builder
                .WithDatabase("testdb")
                .WithUsername("testuser");
        }
    }

    public sealed class OpenGaussDefaultConfiguration(OpenGaussDefaultFixture fixture)
        : OpenGaussContainerTest(fixture), IClassFixture<OpenGaussDefaultFixture>;

    public sealed class OpenGaussDifferentPasswordConfiguration(OpenGaussDifferentPasswordFixture fixture)
        : OpenGaussContainerTest(fixture), IClassFixture<OpenGaussDifferentPasswordFixture>;

    public sealed class OpenGaussCustomWaitStrategyConfiguration(OpenGaussCustomWaitStrategyFixture fixture)
        : OpenGaussContainerTest(fixture), IClassFixture<OpenGaussCustomWaitStrategyFixture>;

    public sealed class OpenGaussImageTagConfiguration(OpenGaussImageTagFixture fixture)
        : OpenGaussContainerTest(fixture), IClassFixture<OpenGaussImageTagFixture>;

    public sealed class OpenGaussDifferentDatabaseAndUsernameConfiguration(OpenGaussDifferentDatabaseAndUsernameFixture fixture)
        : OpenGaussContainerTest(fixture), IClassFixture<OpenGaussDifferentDatabaseAndUsernameFixture>;
}
