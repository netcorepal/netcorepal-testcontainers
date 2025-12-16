using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Docker.DotNet.Models;
using Dm;

namespace Testcontainers.DMDB;

/// <inheritdoc cref="ContainerBuilder{TBuilderEntity, TContainerEntity, TConfigurationEntity}" />
public sealed class DmdbBuilder : ContainerBuilder<DmdbBuilder, DmdbContainer, DmdbConfiguration>
{
    public const string DmdbImage = "cnxc/dm8:20250423-kylin";

    public const ushort DmdbPort = 5236;

    public const string DefaultDatabase = "testdb";

    public const string DefaultUsername = "testdb";

    public const string DefaultPassword = "TestDm123";

    public const string DefaultDbaPassword = "SYSDBA_abc123";

    public DmdbBuilder()
        : this(DmdbImage)
    {
    }

    public DmdbBuilder(string image)
        : this(new DockerImage(image))
    {
    }

    public DmdbBuilder(IImage image)
        : this(new DmdbConfiguration())
    {
        DockerResourceConfiguration = Init().WithImage(image).DockerResourceConfiguration;
    }

    private DmdbBuilder(DmdbConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    protected override DmdbConfiguration DockerResourceConfiguration { get; }

    public DmdbBuilder WithDatabase(string database)
    {
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(database: database));
    }

    public DmdbBuilder WithUsername(string username)
    {
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(username: username));
    }

    public DmdbBuilder WithPassword(string password)
    {
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(password: password));
    }

    public DmdbBuilder WithDbaPassword(string dbaPassword)
    {
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(dbaPassword: dbaPassword));
    }

    public override DmdbContainer Build()
    {
        Validate();

        // By default, the base builder waits until the container is running. For DMDB, a more advanced waiting strategy is necessary.
        // If the user does not provide a custom waiting strategy, append the default DMDB waiting strategy.
        var dmdbBuilder = DockerResourceConfiguration.WaitStrategies.Count() > 1
            ? this
            : WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(DmdbPort)
                    .AddCustomWaitStrategy(
                        new WaitUntil(DockerResourceConfiguration),
                        waitStrategy => waitStrategy.WithTimeout(TimeSpan.FromMinutes(2))));

        return new DmdbContainer(dmdbBuilder.DockerResourceConfiguration);
    }

    protected override DmdbBuilder Init()
    {
        return base.Init()
            .WithPortBinding(DmdbPort, true)
            .WithPrivileged(true)
            .WithDatabase(DefaultDatabase)
            .WithUsername(DefaultUsername)
            .WithPassword(DefaultPassword)
            .WithDbaPassword(DefaultDbaPassword);
    }

    protected override void Validate()
    {
        base.Validate();

        _ = Guard.Argument(DockerResourceConfiguration.Username, nameof(DockerResourceConfiguration.Username))
            .NotNull()
            .NotEmpty();

        _ = Guard.Argument(DockerResourceConfiguration.Password, nameof(DockerResourceConfiguration.Password))
            .NotNull()
            .NotEmpty();

        _ = Guard.Argument(DockerResourceConfiguration.DbaPassword, nameof(DockerResourceConfiguration.DbaPassword))
            .NotNull()
            .NotEmpty();
    }

    protected override DmdbBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(resourceConfiguration));
    }

    protected override DmdbBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(resourceConfiguration));
    }

    protected override DmdbBuilder Merge(DmdbConfiguration oldValue, DmdbConfiguration newValue)
    {
        return new DmdbBuilder(new DmdbConfiguration(oldValue, newValue));
    }

    private sealed class WaitUntil : IWaitUntil
    {
        private readonly DmdbConfiguration _configuration;

        public WaitUntil(DmdbConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> UntilAsync(IContainer container)
        {
            try
            {
                var username = _configuration.Username ?? DefaultUsername;
                var password = _configuration.Password ?? DefaultPassword;
                var dbaPassword = _configuration.DbaPassword ?? DefaultDbaPassword;
                var port = container.GetMappedPublicPort(DmdbPort);
                
                // Build connection string manually to match the format in the reference implementation
                var connectionString = $"Host={container.Hostname};Port={port};Username={username};Password={password};DBAPassword={dbaPassword};Timeout=5;";

                await using var connection = new DmConnection(connectionString);
                await connection.OpenAsync().ConfigureAwait(false);

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
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
