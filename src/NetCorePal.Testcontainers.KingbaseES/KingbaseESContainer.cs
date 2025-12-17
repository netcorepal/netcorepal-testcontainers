using DotNet.Testcontainers.Containers;

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

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync().ConfigureAwait(false);
    }

    private async Task ProvisionAsync()
    {
        var database = _configuration.Database ?? KingbaseESBuilder.DefaultDatabase;
        var username = _configuration.Username ?? KingbaseESBuilder.DefaultUsername;
        var password = _configuration.Password ?? KingbaseESBuilder.DefaultPassword;

        var requiresProvisioning = !string.Equals(database, KingbaseESBuilder.DefaultDatabase, StringComparison.Ordinal)
            || !string.Equals(username, KingbaseESBuilder.DefaultUsername, StringComparison.Ordinal)
            || !string.Equals(password, KingbaseESBuilder.DefaultPassword, StringComparison.Ordinal);

        if (!requiresProvisioning)
        {
            return;
        }

        // Always provision using the built-in admin user.
        var adminUser = KingbaseESBuilder.DefaultUsername;
        var adminPassword = password;

        var pgpassLine = $"127.0.0.1:{KingbaseESBuilder.KingbaseESPort}:*:{adminUser}:{adminPassword}";
        var pgpassLineForBash = EscapeBashSingleQuotes(pgpassLine);

        var prefix =
            $"printf '%s\\n' '{pgpassLineForBash}' > /home/kingbase/.pgpass "
            + "&& chown kingbase:kingbase /home/kingbase/.pgpass "
            + "&& chmod 600 /home/kingbase/.pgpass; ";

        var connect =
            // Use -w to avoid interactive password prompts in non-TTY exec sessions.
            // Set PGPASSFILE explicitly to ensure the client picks up the credentials.
            $"runuser -u kingbase -- env PGPASSFILE=/home/kingbase/.pgpass /home/kingbase/cluster/bin/ksql -w -h 127.0.0.1 -p {KingbaseESBuilder.KingbaseESPort} -U {adminUser} -d kingbase";
        var connectQuery = $"{connect} -t -A";

        if (!string.Equals(username, KingbaseESBuilder.DefaultUsername, StringComparison.Ordinal))
        {
            var escapedUserId = EscapeSqlIdentifier(username);
            var escapedUserLiteral = EscapeSqlLiteral(username);
            var escapedPasswordLiteral = EscapeSqlLiteral(password);

            // Kingbase exposes users via sys_user (used by the image entrypoint as well).
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

            var roleResult = await ExecAsync(roleCommand).ConfigureAwait(false);
            if (!0L.Equals(roleResult.ExitCode))
            {
                throw new InvalidOperationException("Failed to provision KingbaseES role.");
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

            var dbResult = await ExecAsync(dbCommand).ConfigureAwait(false);
            if (!0L.Equals(dbResult.ExitCode))
            {
                throw new InvalidOperationException("Failed to provision KingbaseES database.");
            }
        }
    }

    private static string EscapeBashSingleQuotes(string value)
    {
        return value.Replace("'", "'\"'\"'");
    }

    private static string EscapeSqlIdentifier(string value)
    {
        return value.Replace("\"", "\"\"");
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''");
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
}
