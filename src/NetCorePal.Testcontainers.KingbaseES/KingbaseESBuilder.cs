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

    // The image's entrypoint ultimately uses the plaintext password "12345678ab" by default.
    // Note: the image may store/propagate a base64 form internally, but the env var DB_PASSWORD is expected to be plaintext.
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
            // The image entrypoint expects DB_PASSWORD to be plaintext.
            .WithEnvironment("DB_PASSWORD", RuntimeInformation.OSArchitecture == Architecture.Arm64 ? Base64Encode(password) : password);
    }

    public override KingbaseESContainer Build()
    {
        Validate();

        // By default, the base builder waits until the container is running.
        // This image uses systemd as entrypoint and does NOT automatically start the database.
        // We need a multi-step wait strategy:
        // 1) Trigger/ensure docker-entrypoint.sh has started the DB.
        // 2) Verify the DB is reachable via the image-provided SQL client.
        // Provisioning (custom db/user/password) is applied from KingbaseESContainer.StartAsync() after the container is started.
        var kingbaseESBuilder = DockerResourceConfiguration.WaitStrategies.Count() > 1
            ? this
            : WithWaitStrategy(
                Wait.ForUnixContainer()
                    .AddCustomWaitStrategy(
                        new WaitUntilStarted(),
                        waitStrategy => waitStrategy.WithTimeout(TimeSpan.FromMinutes(10)))
                    .UntilExternalTcpPortIsAvailable(KingbaseESPort));
        return new KingbaseESContainer(kingbaseESBuilder.DockerResourceConfiguration);
    }

    private string GetEffectiveDatabase()
        => DockerResourceConfiguration.Database ?? DefaultDatabase;

    private string GetEffectiveUsername()
        => DockerResourceConfiguration.Username ?? DefaultUsername;

    private string GetEffectivePassword()
        => DockerResourceConfiguration.Password ?? DefaultPassword;

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
            // The entrypoint script expects DB_PASSWORD to be plaintext.
            .WithEnvironment("DB_PASSWORD", DefaultPassword)
            .WithPrivileged(true)
            .WithDatabase(DefaultDatabase)
            .WithUsername(DefaultUsername)
            .WithPassword(DefaultPassword);

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

    private static string EscapeBashSingleQuotes(string value)
    {
        return value.Replace("'", "'\"'\"'");
    }

    private static string Base64Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private sealed class WaitUntilStarted : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            if (RuntimeInformation.OSArchitecture != Architecture.Arm64)
            {
                var prepareSshCommand = new List<string>
                {
                    "sh",
                    "-lc",
                    "pgrep -x sshd >/dev/null 2>&1 || { (command -v ssh-keygen >/dev/null 2>&1 && ssh-keygen -A || /usr/bin/ssh-keygen -A || true); (/usr/sbin/sshd -D -E /tmp/sshd.log >/dev/null 2>&1 &) || true; sleep 1; }"
                };

                try
                {
                    var r = await container.ExecAsync(prepareSshCommand).ConfigureAwait(false);
                    Console.WriteLine(r.Stdout);
                }
                catch
                {
                    throw;
                }
            }


            var entrypointCommand = new List<string>
            {
                "sh",
                "-lc",
                "HOSTNAME=$(hostname) /home/kingbase/cluster/bin/docker-entrypoint.sh"
            };
            try
            {
                var r = await container.ExecAsync(entrypointCommand).ConfigureAwait(false);
                Console.WriteLine(r.Stdout);
            }
            catch
            {
                throw;
            }
            // Give the DB a moment to come up after entrypoint.
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            return true;
        }
    }
}