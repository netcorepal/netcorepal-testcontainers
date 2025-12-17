namespace Testcontainers.KingbaseES.UnitTests;

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
    public void ConnectionStringContainsExpectedProperties()
    {
        var connectionString = _fixture.GetConnectionString();
        
        Assert.Contains("Host=", connectionString);
        Assert.Contains("Port=", connectionString);
        Assert.Contains("Database=", connectionString);
        Assert.Contains("Username=", connectionString);
        Assert.Contains("Password=", connectionString);
        Assert.Contains("Timeout=", connectionString);
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
                throw new InvalidOperationException("Docker integration tests are disabled. Unset SKIP_DOCKER_TESTS or set it to 0 to enable.");
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

    public sealed class KingbaseESCustomWaitStrategyFixture : KingbaseESDefaultFixture
    {
        protected override KingbaseESBuilder Configure(KingbaseESBuilder builder)
        {
            // Demonstrates how consumers can override the default wait strategy.
            // Note: this image does not start the database automatically; a custom wait strategy must still
            // trigger the image-provided docker-entrypoint.sh to initialize/start the DB.
            return builder.WithWaitStrategy(
                Wait.ForUnixContainer().AddCustomWaitStrategy(
                    new WaitUntil(),
                    waitStrategy => waitStrategy.WithTimeout(TimeSpan.FromMinutes(5))));
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            if (Container is null)
            {
                return;
            }

            // Validate the DB is actually reachable via the EF Core provider.
            await using var dbContext = new ProbeDbContext(Container.GetConnectionString());
            var canConnect = await dbContext.Database.CanConnectAsync();
            if (!canConnect)
            {
                throw new InvalidOperationException("Failed to connect to KingbaseES using DotNetCore.EntityFrameworkCore.KingbaseES.");
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

        private sealed class WaitUntil : IWaitUntil
        {
            private static readonly IList<string> Command = new List<string>
            {
                "bash",
                "-lc",
                "HOSTNAME=$(hostname) /home/kingbase/cluster/bin/docker-entrypoint.sh >/dev/null 2>&1 && runuser -u kingbase -- /home/kingbase/cluster/bin/sys_ctl status -D /home/kingbase/cluster/data >/dev/null 2>&1"
            };

            public async Task<bool> UntilAsync(IContainer container)
            {
                var execResult = await container.ExecAsync(Command).ConfigureAwait(false);
                return 0L.Equals(execResult.ExitCode);
            }
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

    public sealed class KingbaseESCustomWaitStrategyConfiguration(KingbaseESCustomWaitStrategyFixture fixture)
        : KingbaseESContainerTest(fixture), IClassFixture<KingbaseESCustomWaitStrategyFixture>;

    public sealed class KingbaseESImageTagConfiguration(KingbaseESImageTagFixture fixture)
        : KingbaseESContainerTest(fixture), IClassFixture<KingbaseESImageTagFixture>;

    public sealed class KingbaseESDifferentDatabaseAndUsernameConfiguration(KingbaseESDifferentDatabaseAndUsernameFixture fixture)
        : KingbaseESContainerTest(fixture), IClassFixture<KingbaseESDifferentDatabaseAndUsernameFixture>;
}
