namespace Testcontainers.DMDB;

public sealed record DmdbContainerOptions
{
    public const int DefaultPort = 5236;

    public string Image { get; init; } = "cnxc/dm8:20250423-kylin";

    public string Host { get; init; } = "localhost";

    public string Username { get; init; } = "testdb";

    public string Password { get; init; } = "TestDm123";

    public string DbaPassword { get; init; } = "SYSDBA_abc123";

    public string Database { get; init; } = "testdb";

    public bool Privileged { get; init; } = true;

    public int TimeoutSeconds { get; init; } = 30;
}
