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
            .WithEnvironment("DB_PASSWORD", password);
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
                    .AddCustomWaitStrategy(
                        new WaitUntilSqlReady(GetEffectiveUsername(), GetEffectivePassword()),
                        waitStrategy => waitStrategy.WithTimeout(TimeSpan.FromMinutes(10))));

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

    private static string EscapeBashSingleQuotes(string value)
    {
        return value.Replace("'", "'\"'\"'");
    }

    private sealed class WaitUntilStarted : IWaitUntil
    {
        private bool _entrypointExecuted;
        private int _retryCount;

        public async Task<bool> UntilAsync(IContainer container)
        {
            // Ensure sshd is available before running the image's entrypoint.
            // The script requires SSH connectivity to ALL_NODE_IP (localhost/127.0.0.1).
            // On some Linux/amd64 environments, sshd is not started by default under systemd-without-dbus.
            // We proactively generate host keys and start sshd in background to unblock initialization.
            var prepareSshCommand = new List<string>
            {
                "sh",
                "-lc",
                // If sshd is not running, generate host keys (idempotent) and start sshd in background.
                // Note: avoid `disown` (bash-only) for broader shell compatibility.
                "pgrep -x sshd >/dev/null 2>&1 || { (command -v ssh-keygen >/dev/null 2>&1 && ssh-keygen -A || /usr/bin/ssh-keygen -A || true); (/usr/sbin/sshd -D -E /tmp/sshd.log >/dev/null 2>&1 &) || true; sleep 1; }"
            };

            try
            {
                _ = await container.ExecAsync(prepareSshCommand).ConfigureAwait(false);
            }
            catch
            {
                // Ignore SSH preparation failures; entrypoint may still succeed in other environments.
            }

            var statusCheckCommand = new List<string>
            {
                "sh",
                "-lc",
                "runuser -u kingbase -- /home/kingbase/cluster/bin/sys_ctl status -D /home/kingbase/cluster/data >/dev/null 2>&1"
            };

            // Fast path: DB already running.
            var statusResult = await container.ExecAsync(statusCheckCommand).ConfigureAwait(false);
            if (0L.Equals(statusResult.ExitCode))
            {
                return true;
            }

            // Execute the image entrypoint at least once to initialize/start the DB.
            // If the first attempt failed (common when SSH isn't ready), allow a few retries.
            var shouldRunEntrypoint = !_entrypointExecuted || _retryCount < 3;
            if (shouldRunEntrypoint)
            {
                var entrypointCommand = new List<string>
                {
                    "sh",
                    "-lc",
                    "HOSTNAME=$(hostname) /home/kingbase/cluster/bin/docker-entrypoint.sh >/tmp/kingbase-entrypoint.log 2>&1; echo $? > /tmp/kingbase-entrypoint.exitcode"
                };

                _entrypointExecuted = true;

                _ = await container.ExecAsync(entrypointCommand).ConfigureAwait(false);

                // Give the DB a moment to come up after entrypoint.
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }

            _retryCount++;

            // Final check: DB running now?
            statusResult = await container.ExecAsync(statusCheckCommand).ConfigureAwait(false);
            return 0L.Equals(statusResult.ExitCode);
        }
    }

    private sealed class WaitUntilSqlReady : IWaitUntil
    {
        private readonly string _username;
        private readonly string _password;
        private int _retryCount = 0;

        public WaitUntilSqlReady(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public async Task<bool> UntilAsync(IContainer container)
        {
            var adminUser = DefaultUsername;
            var adminPassword = _password;
            var maintenanceDatabase = DefaultDatabase;

            var pgpassLine = $"127.0.0.1:{KingbaseESPort}:*:{adminUser}:{adminPassword}";
            var pgpassLineForBash = EscapeBashSingleQuotes(pgpassLine);

            var prefix =
                $"printf '%s\\n' '{pgpassLineForBash}' > /home/kingbase/.pgpass "
                + "&& chown kingbase:kingbase /home/kingbase/.pgpass "
                + "&& chmod 600 /home/kingbase/.pgpass; ";

            var connectQuery =
                "runuser -u kingbase -- env PGPASSFILE=/home/kingbase/.pgpass "
                + $"/home/kingbase/cluster/bin/ksql -w -h 127.0.0.1 -p {KingbaseESPort} -U {adminUser} -d {maintenanceDatabase} -t -A";

            var sql = "SELECT 1;";
            var sqlForBash = EscapeBashSingleQuotes(sql);

            var command = new List<string>
            {
                "sh",
                "-lc",
                // Ensure select 1 returns '1' exactly.
                $"{prefix}{connectQuery} -c '{sqlForBash}' | grep -qx '1'"
            };

            var result = await container.ExecAsync(command).ConfigureAwait(false);
            if (0L.Equals(result.ExitCode))
            {
                // Reset retry count on success
                _retryCount = 0;
                return true;
            }

            // Add a small delay before retrying to avoid hammering the container
            // This is especially important in early initialization stages
            _retryCount++;
            if (_retryCount < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }

            return false;
        }
    }
}
