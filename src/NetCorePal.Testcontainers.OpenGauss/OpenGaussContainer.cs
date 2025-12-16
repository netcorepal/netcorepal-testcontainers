using DotNet.Testcontainers.Containers;

namespace Testcontainers.OpenGauss;

/// <inheritdoc cref="DockerContainer" />
public sealed class OpenGaussContainer : DockerContainer, IDatabaseContainer
{
    private readonly OpenGaussConfiguration _configuration;

    public OpenGaussContainer(OpenGaussConfiguration configuration)
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
        var database = _configuration.Database ?? OpenGaussBuilder.DefaultDatabase;
        var username = _configuration.Username ?? OpenGaussBuilder.DefaultUsername;
        var password = _configuration.Password ?? OpenGaussBuilder.DefaultPassword;

        var requiresProvisioning = !string.Equals(database, OpenGaussBuilder.DefaultDatabase, StringComparison.Ordinal)
            || !string.Equals(username, OpenGaussBuilder.DefaultUsername, StringComparison.Ordinal);

        if (!requiresProvisioning)
        {
            return;
        }

        var adminPassword = EscapeBashSingleQuotes(password);

        // Ensure gsql has the right PATH/LD_LIBRARY_PATH.
        var prefix = "source /home/omm/.bashrc >/dev/null 2>&1 || true; ";
        var connect = $"printf '%s\\n' '{adminPassword}' | gsql -h 127.0.0.1 -p {OpenGaussBuilder.OpenGaussPort} -U {OpenGaussBuilder.DefaultUsername} -d {OpenGaussBuilder.DefaultDatabase}";
        var connectQuery = $"{connect} -t -A";

        if (!string.Equals(username, OpenGaussBuilder.DefaultUsername, StringComparison.Ordinal))
        {
            var escapedUserId = EscapeSqlIdentifier(username);
            var escapedUserLiteral = EscapeSqlLiteral(username);
            var escapedPasswordLiteral = EscapeSqlLiteral(password);

            // CREATE ROLE is transactional; use a DO block to avoid failing if the role already exists.
            var createOrUpdateRoleSql =
                $"DO $$ BEGIN "
                + $"IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{escapedUserLiteral}') THEN "
                + $"CREATE ROLE \"{escapedUserId}\" WITH LOGIN PASSWORD '{escapedPasswordLiteral}'; "
                + $"ELSE "
                + $"ALTER ROLE \"{escapedUserId}\" WITH LOGIN PASSWORD '{escapedPasswordLiteral}'; "
                + $"END IF; "
                + $"END $$;";

            var createOrUpdateRoleSqlForBash = EscapeBashSingleQuotes(createOrUpdateRoleSql);

            var roleCommand = new List<string>
            {
                "bash",
                "-lc",
                $"{prefix}{connect} -c '{createOrUpdateRoleSqlForBash}'"
            };

            var roleResult = await ExecAsync(roleCommand).ConfigureAwait(false);
            if (!0L.Equals(roleResult.ExitCode))
            {
                throw new InvalidOperationException("Failed to provision OpenGauss role.");
            }
        }

        if (!string.Equals(database, OpenGaussBuilder.DefaultDatabase, StringComparison.Ordinal))
        {
            var escapedDbId = EscapeSqlIdentifier(database);
            var escapedDbLiteral = EscapeSqlLiteral(database);

            var owner = string.Equals(username, OpenGaussBuilder.DefaultUsername, StringComparison.Ordinal)
                ? OpenGaussBuilder.DefaultUsername
                : username;

            var escapedOwnerId = EscapeSqlIdentifier(owner);
            var existsCheckSql = $"SELECT 1 FROM pg_database WHERE datname = '{escapedDbLiteral}';";
            var createDbSql = $"CREATE DATABASE \"{escapedDbId}\" OWNER \"{escapedOwnerId}\";";

            var existsCheckSqlForBash = EscapeBashSingleQuotes(existsCheckSql);
            var createDbSqlForBash = EscapeBashSingleQuotes(createDbSql);

            // CREATE DATABASE is not transactional; probe first and only create when missing.
            var createDbCommand = new List<string>
            {
                "bash",
                "-lc",
                $"{prefix}{connectQuery} -c '{existsCheckSqlForBash}' | grep -qx '1' || {connect} -c '{createDbSqlForBash}'"
            };

            var dbResult = await ExecAsync(createDbCommand).ConfigureAwait(false);
            if (!0L.Equals(dbResult.ExitCode))
            {
                throw new InvalidOperationException("Failed to provision OpenGauss database.");
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
        properties.Add("Port", GetMappedPublicPort(OpenGaussBuilder.OpenGaussPort).ToString());
        properties.Add("Database", _configuration.Database ?? OpenGaussBuilder.DefaultDatabase);
        properties.Add("Username", _configuration.Username ?? OpenGaussBuilder.DefaultUsername);
        properties.Add("Password", _configuration.Password ?? OpenGaussBuilder.DefaultPassword);
        return string.Join(";", properties.Select(property => string.Join("=", property.Key, property.Value)));
    }
}
