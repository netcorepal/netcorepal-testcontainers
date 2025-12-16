namespace Testcontainers.KingbaseES.UnitTests;

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        var isDockerEnabled = !string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal)
                              && !string.Equals(Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS"), "0", StringComparison.Ordinal);

        if (!isDockerEnabled)
        {
            Skip = "Docker integration tests are disabled. Unset SKIP_DOCKER_TESTS or set it to 0 to enable.";
        }
    }
}
