using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Docker.DotNet.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace Testcontainers.KingbaseES;

/// <inheritdoc cref="ContainerBuilder{TBuilderEntity, TContainerEntity, TConfigurationEntity}" />
public sealed class KingbaseESBuilder : ContainerBuilder<KingbaseESBuilder, KingbaseESContainer, KingbaseESConfiguration>
{
    public const string KingbaseESImage = "apecloud/kingbase:v008r006c009b0014-unit";

    public const ushort KingbaseESPort = 54321;

    // The image initializes the lower-case "test" database by default.
    public const string DefaultDatabase = "test";

    public const string DefaultUsername = "system";

    // The image's entrypoint script defaults to db_password_base64="MTIzNDU2NzhhYgo=" -> "12345678ab".
    public const string DefaultPassword = "12345678ab";

    private const string DefaultAllNodeIp = "localhost";

    private const string DefaultReplicaCount = "1";

    private const string DefaultTrustedIp = "127.0.0.1";

    // The image's entrypoint script expects a StatefulSet-like hostname ending with "-0" for a single-node init.
    private const string DefaultHostname = "kingbase-0";

    public KingbaseESBuilder()
        : this(KingbaseESImage)
    {
    }

    public KingbaseESBuilder(string image)
        : this(new DockerImage(image))
    {
    }

    public KingbaseESBuilder(IImage image)
        : this(new KingbaseESConfiguration())
    {
        DockerResourceConfiguration = Init().WithImage(image).DockerResourceConfiguration;
    }

    private KingbaseESBuilder(KingbaseESConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    protected override KingbaseESConfiguration DockerResourceConfiguration { get; }

    public KingbaseESBuilder WithDatabase(string database)
    {
        return Merge(DockerResourceConfiguration, new KingbaseESConfiguration(database: database));
    }

    public KingbaseESBuilder WithUsername(string username)
    {
        return Merge(DockerResourceConfiguration, new KingbaseESConfiguration(username: username));
    }

    public KingbaseESBuilder WithPassword(string password)
    {
        return Merge(DockerResourceConfiguration, new KingbaseESConfiguration(password: password))
            // The image entrypoint expects DB_PASSWORD to be base64.
            .WithEnvironment("DB_PASSWORD", Base64Encode(password));
    }

    public override KingbaseESContainer Build()
    {
        Validate();

        // By default, the base builder waits until the container is running. For KingbaseES, a more advanced waiting strategy is necessary.
        // If the user does not provide a custom waiting strategy, append the default KingbaseES waiting strategy.
        var kingbaseESBuilder = DockerResourceConfiguration.WaitStrategies.Count() > 1
            ? this
            : WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(KingbaseESPort)
                    .UntilMessageIsLogged("database system is ready to accept connections"));

        return new KingbaseESContainer(kingbaseESBuilder.DockerResourceConfiguration);
    }

    protected override KingbaseESBuilder Init()
    {
        var builder = base.Init()
            .WithPortBinding(KingbaseESPort, true)
            .WithHostname(DefaultHostname)
            .WithEnvironment("ALL_NODE_IP", DefaultAllNodeIp)
            .WithEnvironment("REPLICA_COUNT", DefaultReplicaCount)
            .WithEnvironment("TRUST_IP", DefaultTrustedIp)
            // Ensure the entrypoint script has a suitable HOSTNAME env var.
            // Note: systemd images may clear HOSTNAME from the environment at runtime; the wait strategy uses `hostname`.
            .WithEnvironment("HOSTNAME", DefaultHostname)
            // The entrypoint script expects DB_PASSWORD to be base64.
            .WithEnvironment("DB_PASSWORD", Base64Encode(DefaultPassword))
            .WithPrivileged(true)
            .WithDatabase(DefaultDatabase)
            .WithUsername(DefaultUsername)
            .WithPassword(DefaultPassword);

        // systemd-based images commonly require extra host config to boot reliably on Linux CI runners.
        // Keep this Linux-only to avoid breaking macOS/Windows Docker environments.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            builder = builder
                .WithBindMount("/sys/fs/cgroup", "/sys/fs/cgroup", AccessMode.ReadWrite)
                .WithTmpfsMount("/run")
                .WithTmpfsMount("/run/lock")
                .WithEnvironment("container", "docker");
        }

        return builder;
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
    }

    protected override KingbaseESBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new KingbaseESConfiguration(resourceConfiguration));
    }

    protected override KingbaseESBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new KingbaseESConfiguration(resourceConfiguration));
    }

    protected override KingbaseESBuilder Merge(KingbaseESConfiguration oldValue, KingbaseESConfiguration newValue)
    {
        return new KingbaseESBuilder(new KingbaseESConfiguration(oldValue, newValue));
    }

    private static string Base64Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }
}
