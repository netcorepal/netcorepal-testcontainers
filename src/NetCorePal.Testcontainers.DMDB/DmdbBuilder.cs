using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Docker.DotNet.Models;

namespace Testcontainers.DMDB;

/// <inheritdoc cref="ContainerBuilder{TBuilderEntity, TContainerEntity, TConfigurationEntity}" />
public sealed class DmdbBuilder : ContainerBuilder<DmdbBuilder, DmdbContainer, DmdbConfiguration>
{
    public const string DmdbImage = "cnxc/dm8:20250423-kylin";

    public const ushort DmdbPort = 5236;

    public const string DefaultDatabase = "testdb";

    public const string DefaultUsername = "SYSDBA";

    public const string DefaultPassword = "SYSDBA_abc123";

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
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(password: password))
            .WithEnvironment("DM_USER_PWD", password);
    }

    public DmdbBuilder WithDbaPassword(string dbaPassword)
    {
        return Merge(DockerResourceConfiguration, new DmdbConfiguration(dbaPassword: dbaPassword))
            .WithEnvironment("SYSDBA_PWD", dbaPassword)
            .WithEnvironment("SYSAUDITOR_PWD", dbaPassword);
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
            .WithEnvironment("DM_USER_PWD", DefaultPassword)
            .WithEnvironment("SYSDBA_PWD", DefaultDbaPassword)
            .WithEnvironment("SYSAUDITOR_PWD", DefaultDbaPassword)
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

        private readonly IList<string> _command;

        public WaitUntil(DmdbConfiguration configuration)
        {
            _configuration = configuration;

            var password = EscapeBashSingleQuotes(configuration.Password ?? string.Empty);
            var dbaPassword = EscapeBashSingleQuotes(configuration.DbaPassword ?? string.Empty);

            // Use bash to keep the invocation simple and allow quoting.
            _command = new List<string>
            {
                "bash",
                "-lc",
                // Always probe using the default user/database. Custom username/database can be provisioned after the container has started.
                // Prefer local in-container probing to avoid host networking edge-cases during startup.
                $"/opt/dmdbms/bin/disql '{DefaultUsername}'/'{password}'@127.0.0.1:{DmdbPort} -e \"SELECT 1;\" >/dev/null 2>&1 || /opt/dmdbms/bin/disql '{DefaultUsername}'/'{password}'@127.0.0.1:{DmdbPort} -e \"SELECT 1\" >/dev/null 2>&1 || /opt/dmdbms/bin/disql sysdba/'{dbaPassword}'@127.0.0.1:{DmdbPort} -e \"SELECT 1;\" >/dev/null 2>&1"
            };
        }

        private static string EscapeBashSingleQuotes(string value)
        {
            return value.Replace("'", "'\"'\"'");
        }

        public async Task<bool> UntilAsync(IContainer container)
        {
            var execResult = await container.ExecAsync(_command).ConfigureAwait(false);
            return 0L.Equals(execResult.ExitCode);
        }
    }
}
