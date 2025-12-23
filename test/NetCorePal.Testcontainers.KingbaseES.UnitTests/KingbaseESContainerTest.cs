namespace Testcontainers.KingbaseES.UnitTests;

using System.Data;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;

public abstract class KingbaseESContainerTest
{
    private readonly KingbaseESDefaultFixture _fixture;

    protected KingbaseESContainerTest(KingbaseESDefaultFixture fixture)
    {
        _fixture = fixture;
    }

    [DockerFact]
    public async Task ConnectionStringContainsExpectedProperties()
    {
        var connectionString = _fixture.GetConnectionString();

        Assert.Contains("Host=", connectionString);
        Assert.Contains("Port=", connectionString);
        Assert.Contains("Database=", connectionString);
        Assert.Contains("Username=", connectionString);
        Assert.Contains("Password=", connectionString);
        Assert.Contains("Timeout=", connectionString);

        await VerifyKingbaseEsIsReachableAsync(connectionString);
    }

    private static async Task VerifyKingbaseEsIsReachableAsync(string connectionString)
    {
        await using var dbContext = new ProbeDbContext(connectionString);

        var canConnect = await dbContext.Database.CanConnectAsync();
        if (!canConnect)
        {
            throw new InvalidOperationException(
                "Failed to connect to KingbaseES using DotNetCore.EntityFrameworkCore.KingbaseES.");
        }

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        var result = await command.ExecuteScalarAsync();
        if (result is null || Convert.ToInt32(result) != 1)
        {
            throw new InvalidOperationException("Connected to KingbaseES but 'SELECT 1' did not return 1.");
        }
    }

    private sealed class ProbeDbContext : DbContext
    {
        private readonly string _connectionString;

        public ProbeDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseKdbndp(_connectionString);
    }

    public class KingbaseESDefaultFixture : IAsyncLifetime
    {
        private readonly KingbaseESBuilder _builder;

        public KingbaseESDefaultFixture()
        {
            _builder = Configure(new KingbaseESBuilder());
        }

        public KingbaseESContainer? Container { get; private set; }

        protected static bool IsDockerEnabled
            => !string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal)
               && !string.Equals(Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS"), "0", StringComparison.Ordinal);

        protected virtual KingbaseESBuilder Configure(KingbaseESBuilder builder)
        {
            return builder;
        }

        public virtual async Task InitializeAsync()
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
                throw new InvalidOperationException(
                    "Docker integration tests are disabled. Unset SKIP_DOCKER_TESTS or set it to 0 to enable.");
            }

            return Container.GetConnectionString();
        }
    }

    public sealed class KingbaseESDifferentPasswordFixture : KingbaseESDefaultFixture
    {
        protected override KingbaseESBuilder Configure(KingbaseESBuilder builder)
        {
            return builder.WithPassword("Test@456");
        }
    }

    public sealed class KingbaseESImageTagFixture : KingbaseESDefaultFixture
    {
        protected override KingbaseESBuilder Configure(KingbaseESBuilder builder)
        {
            // Demonstrates how consumers can pin a specific image tag.
            // Defaults to the library's default image to keep CI/dev stable.
            var image = Environment.GetEnvironmentVariable("KINGBASEES_IMAGE") ?? KingbaseESBuilder.KingbaseESImage;
            return builder.WithImage(new DockerImage(image));
        }
    }

    public sealed class KingbaseESDifferentDatabaseAndUsernameFixture : KingbaseESDefaultFixture
    {
        protected override KingbaseESBuilder Configure(KingbaseESBuilder builder)
        {
            return builder
                .WithDatabase("mydb")
                .WithUsername("myuser");
        }
    }

    public sealed class KingbaseESDefaultConfiguration(KingbaseESDefaultFixture fixture)
        : KingbaseESContainerTest(fixture), IClassFixture<KingbaseESDefaultFixture>;

    public sealed class KingbaseESDifferentPasswordConfiguration(KingbaseESDifferentPasswordFixture fixture)
        : KingbaseESContainerTest(fixture), IClassFixture<KingbaseESDifferentPasswordFixture>;

    public sealed class KingbaseESImageTagConfiguration(KingbaseESImageTagFixture fixture)
        : KingbaseESContainerTest(fixture), IClassFixture<KingbaseESImageTagFixture>;

    public sealed class KingbaseESDifferentDatabaseAndUsernameConfiguration(
        KingbaseESDifferentDatabaseAndUsernameFixture fixture)
        : KingbaseESContainerTest(fixture), IClassFixture<KingbaseESDifferentDatabaseAndUsernameFixture>;
}