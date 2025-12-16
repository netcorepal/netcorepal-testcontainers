namespace Testcontainers.OpenGauss;

public sealed record OpenGaussContainerOptions
{
    public const int DefaultPort = 5432;

    public string Image { get; init; } = "opengauss/opengauss:latest";

    public string Host { get; init; } = "localhost";

    public string Username { get; init; } = "gaussdb";

    public string Password { get; init; } = "Test@123";

    public string Database { get; init; } = "postgres";

    public bool Privileged { get; init; } = true;

    public int TimeoutSeconds { get; init; } = 30;
}
