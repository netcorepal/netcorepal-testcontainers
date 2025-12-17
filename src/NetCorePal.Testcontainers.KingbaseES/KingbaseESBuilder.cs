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

    private static string EscapeBashSingleQuotes(string value)
    {
        return value.Replace("'", "'\"'\"'");
    }

    private sealed class WaitUntilStarted : IWaitUntil
    {
        private static readonly IList<string> Command = new List<string>
        {
            "bash",
            "-lc",
            // The image's systemd entrypoint does NOT guarantee DB is started when the container is "running".
            // We need to trigger docker-entrypoint.sh (install/init/start) and wait until sys_ctl reports the DB is running.
            // Keep the check idempotent and retryable:
            // - If sys_ctl exists and the DB is running, succeed fast.
            // - Otherwise, run docker-entrypoint.sh once (guarded by a simple lock) and then re-check.
            // - Only mark success when sys_ctl status succeeds.
            "DATA_DIR=/home/kingbase/cluster/data; SYS_CTL=/home/kingbase/cluster/bin/sys_ctl; ENTRYPOINT=/home/kingbase/cluster/bin/docker-entrypoint.sh; "
            + "MARKER=/home/kingbase/.testcontainers-kingbasees.started; LOCKDIR=/tmp/testcontainers-kingbasees-entrypoint.lock; "
            + "if [ -x \"$SYS_CTL\" ] && runuser -u kingbase -- \"$SYS_CTL\" status -D \"$DATA_DIR\" >/dev/null 2>&1; then touch \"$MARKER\"; exit 0; fi; "
            + "if [ ! -f \"$MARKER\" ]; then "
            + "  if mkdir \"$LOCKDIR\" >/dev/null 2>&1; then "
            + "    ( HOSTNAME=$(hostname) \"$ENTRYPOINT\" >/tmp/testcontainers-kingbasees-entrypoint.out 2>&1 || true ); "
            + "    rmdir \"$LOCKDIR\" >/dev/null 2>&1 || true; "
            + "  fi; "
            + "fi; "
            + "[ -x \"$SYS_CTL\" ] && runuser -u kingbase -- \"$SYS_CTL\" status -D \"$DATA_DIR\" >/dev/null 2>&1 && touch \"$MARKER\""
        };

        public async Task<bool> UntilAsync(IContainer container)
        {
            var execResult = await container.ExecAsync(Command).ConfigureAwait(false);
            return 0L.Equals(execResult.ExitCode);
        }
    }

    private sealed class WaitUntilSqlReady : IWaitUntil
    {
        private readonly string _username;
        private readonly string _password;

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
                "bash",
                "-lc",
                // Ensure select 1 returns '1' exactly.
                $"{prefix}{connectQuery} -c '{sqlForBash}' | grep -qx '1'"
            };

            var result = await container.ExecAsync(command).ConfigureAwait(false);
            return 0L.Equals(result.ExitCode);
        }
    }
}
