using DotNet.Testcontainers.Containers;
using System.Text;

namespace Testcontainers.KingbaseES;

/// <inheritdoc cref="DockerContainer" />
public sealed class KingbaseESContainer : DockerContainer, IDatabaseContainer
{
    private readonly KingbaseESConfiguration _configuration;

    public KingbaseESContainer(KingbaseESConfiguration configuration)
        : base(configuration)
    {
        _configuration = configuration;
    }

    public new async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task IContainer.StartAsync(CancellationToken cancellationToken)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    

    public string GetConnectionString()
    {
        var properties = new Dictionary<string, string>();
        properties.Add("Host", Hostname);
        properties.Add("Port", GetMappedPublicPort(KingbaseESBuilder.KingbaseESPort).ToString());
        properties.Add("Database", _configuration.Database ?? KingbaseESBuilder.DefaultDatabase);
        properties.Add("Username", _configuration.Username ?? KingbaseESBuilder.DefaultUsername);
        properties.Add("Password", _configuration.Password ?? KingbaseESBuilder.DefaultPassword);
        properties.Add("Timeout", "30");
        return string.Join(";", properties.Select(property => string.Join("=", property.Key, property.Value)));
    }

    private string GetEffectiveDatabase()
        => _configuration.Database ?? KingbaseESBuilder.DefaultDatabase;

    private string GetEffectiveUsername()
        => _configuration.Username ?? KingbaseESBuilder.DefaultUsername;

    private string GetEffectivePassword()
        => _configuration.Password ?? KingbaseESBuilder.DefaultPassword;

    private static string EscapeBashSingleQuotes(string value)
        => value.Replace("'", "'\"'\"'");

    private static string EscapeSqlIdentifier(string value)
        => value.Replace("\"", "\"\"");

    private static string EscapeSqlLiteral(string value)
        => value.Replace("'", "''");

    private async Task ProvisionAsync(CancellationToken cancellationToken)
    {
        var database = GetEffectiveDatabase();
        var username = GetEffectiveUsername();
        var password = GetEffectivePassword();

        var requiresProvisioning = !string.Equals(database, KingbaseESBuilder.DefaultDatabase, StringComparison.Ordinal)
            || !string.Equals(username, KingbaseESBuilder.DefaultUsername, StringComparison.Ordinal)
            || !string.Equals(password, KingbaseESBuilder.DefaultPassword, StringComparison.Ordinal);

        if (!requiresProvisioning)
        {
            return;
        }

        await EnsureSqlReadyAsync(password, cancellationToken).ConfigureAwait(false);

        var adminUser = KingbaseESBuilder.DefaultUsername;
        var adminPassword = password;
        var maintenanceDatabase = KingbaseESBuilder.DefaultDatabase;

        var pgpassLine = $"127.0.0.1:{KingbaseESBuilder.KingbaseESPort}:*:{adminUser}:{adminPassword}";
        var pgpassLineForBash = EscapeBashSingleQuotes(pgpassLine);

        var prefix =
            $"printf '%s\\n' '{pgpassLineForBash}' > /home/kingbase/.pgpass "
            + "&& chown kingbase:kingbase /home/kingbase/.pgpass "
            + "&& chmod 600 /home/kingbase/.pgpass; ";

        var connect =
            "runuser -u kingbase -- env PGPASSFILE=/home/kingbase/.pgpass "
            + $"/home/kingbase/cluster/bin/ksql -w -h 127.0.0.1 -p {KingbaseESBuilder.KingbaseESPort} -U {adminUser} -d {maintenanceDatabase}";
        var connectQuery = $"{connect} -t -A";

        if (!string.Equals(username, KingbaseESBuilder.DefaultUsername, StringComparison.Ordinal))
        {
            var escapedUserId = EscapeSqlIdentifier(username);
            var escapedUserLiteral = EscapeSqlLiteral(username);
            var escapedPasswordLiteral = EscapeSqlLiteral(password);

            var existsUserSql = $"SELECT 1 FROM sys_user WHERE usename = '{escapedUserLiteral}';";
            var createUserSql = $"CREATE USER \"{escapedUserId}\" WITH PASSWORD '{escapedPasswordLiteral}';";
            var alterUserSql = $"ALTER USER \"{escapedUserId}\" WITH PASSWORD '{escapedPasswordLiteral}';";

            var existsUserSqlForBash = EscapeBashSingleQuotes(existsUserSql);
            var createUserSqlForBash = EscapeBashSingleQuotes(createUserSql);
            var alterUserSqlForBash = EscapeBashSingleQuotes(alterUserSql);

            var roleCommand = new List<string>
            {
                "bash",
                "-lc",
                $"{prefix}{connectQuery} -c '{existsUserSqlForBash}' | grep -qx '1' || {connect} -c '{createUserSqlForBash}'; {connect} -c '{alterUserSqlForBash}'"
            };

            var roleResult = await ExecAsync(roleCommand, cancellationToken).ConfigureAwait(false);
            if (!0L.Equals(roleResult.ExitCode))
            {
                throw new InvalidOperationException("Failed to provision KingbaseES user.");
            }
        }

        if (!string.Equals(database, KingbaseESBuilder.DefaultDatabase, StringComparison.Ordinal))
        {
            var escapedDbId = EscapeSqlIdentifier(database);
            var escapedDbLiteral = EscapeSqlLiteral(database);

            var owner = string.Equals(username, KingbaseESBuilder.DefaultUsername, StringComparison.Ordinal)
                ? KingbaseESBuilder.DefaultUsername
                : username;

            var escapedOwnerId = EscapeSqlIdentifier(owner);
            var existsDbSql = $"SELECT 1 FROM pg_database WHERE datname = '{escapedDbLiteral}';";
            var createDbSql = $"CREATE DATABASE \"{escapedDbId}\" OWNER \"{escapedOwnerId}\";";

            var existsDbSqlForBash = EscapeBashSingleQuotes(existsDbSql);
            var createDbSqlForBash = EscapeBashSingleQuotes(createDbSql);

            var dbCommand = new List<string>
            {
                "bash",
                "-lc",
                $"{prefix}{connectQuery} -c '{existsDbSqlForBash}' | grep -qx '1' || {connect} -c '{createDbSqlForBash}'"
            };

            var dbResult = await ExecAsync(dbCommand, cancellationToken).ConfigureAwait(false);
            if (!0L.Equals(dbResult.ExitCode))
            {
                throw new InvalidOperationException("Failed to provision KingbaseES database.");
            }
        }
    }

    private async Task EnsureSqlReadyAsync(string password, CancellationToken cancellationToken)
    {
        var adminUser = KingbaseESBuilder.DefaultUsername;
        var maintenanceDatabase = KingbaseESBuilder.DefaultDatabase;

        var pgpassLine = $"127.0.0.1:{KingbaseESBuilder.KingbaseESPort}:*:{adminUser}:{password}";
        var pgpassLineForBash = EscapeBashSingleQuotes(pgpassLine);

        var prefix =
            $"printf '%s\\n' '{pgpassLineForBash}' > /home/kingbase/.pgpass "
            + "&& chown kingbase:kingbase /home/kingbase/.pgpass "
            + "&& chmod 600 /home/kingbase/.pgpass; ";

        var connectQuery =
            "runuser -u kingbase -- env PGPASSFILE=/home/kingbase/.pgpass "
            + $"/home/kingbase/cluster/bin/ksql -w -h 127.0.0.1 -p {KingbaseESBuilder.KingbaseESPort} -U {adminUser} -d {maintenanceDatabase} -t -A";

        var command = new List<string>
        {
            "bash",
            "-lc",
            $"{prefix}{connectQuery} -c 'SELECT 1;' | grep -qx '1'"
        };

        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ExecAsync(command, cancellationToken).ConfigureAwait(false);
            if (0L.Equals(result.ExitCode))
            {
                return;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new InvalidOperationException("Timed out waiting for KingbaseES SQL to become ready.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
    }
}
