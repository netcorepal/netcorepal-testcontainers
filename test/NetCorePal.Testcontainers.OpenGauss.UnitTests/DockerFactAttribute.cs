namespace Testcontainers.OpenGauss.UnitTests;

using Xunit;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        var skipDockerTests = string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS"), "0", StringComparison.Ordinal);

        if (skipDockerTests)
        {
            Skip = "Docker integration tests are disabled. Unset SKIP_DOCKER_TESTS or set it to 0 to enable.";
        }
    }
}
