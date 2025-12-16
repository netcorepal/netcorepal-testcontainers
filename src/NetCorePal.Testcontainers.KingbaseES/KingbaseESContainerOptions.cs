namespace Testcontainers.KingbaseES;

public sealed record KingbaseESContainerOptions
{
    public const int DefaultPort = 54321;

    public string Image { get; init; } = "apecloud/kingbase:v008r006c009b0014-unit";

    public string Host { get; init; } = "localhost";

    public string Username { get; init; } = "system";

    public string Password { get; init; } = "Test@123";

    public string Database { get; init; } = "TEST";

    public bool Privileged { get; init; } = true;

    public int TimeoutSeconds { get; init; } = 30;
}
