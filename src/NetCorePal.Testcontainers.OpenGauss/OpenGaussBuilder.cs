using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Docker.DotNet.Models;

namespace Testcontainers.OpenGauss;

/// <inheritdoc cref="ContainerBuilder{TBuilderEntity, TContainerEntity, TConfigurationEntity}" />
public sealed class OpenGaussBuilder : ContainerBuilder<OpenGaussBuilder, OpenGaussContainer, OpenGaussConfiguration>
{
    public const string OpenGaussImage = "opengauss/opengauss:latest";

    public const ushort OpenGaussPort = 5432;

    public const string DefaultDatabase = "postgres";

    public const string DefaultUsername = "gaussdb";

    public const string DefaultPassword = "Test@123";

    public OpenGaussBuilder()
        : this(OpenGaussImage)
    {
    }

    public OpenGaussBuilder(string image)
        : this(new DockerImage(image))
    {
    }

    public OpenGaussBuilder(IImage image)
        : this(new OpenGaussConfiguration())
    {
        DockerResourceConfiguration = Init().WithImage(image).DockerResourceConfiguration;
    }

    private OpenGaussBuilder(OpenGaussConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    protected override OpenGaussConfiguration DockerResourceConfiguration { get; }

    public OpenGaussBuilder WithDatabase(string database)
    {
        return Merge(DockerResourceConfiguration, new OpenGaussConfiguration(database: database));
    }

    public OpenGaussBuilder WithUsername(string username)
    {
        return Merge(DockerResourceConfiguration, new OpenGaussConfiguration(username: username));
    }

    public OpenGaussBuilder WithPassword(string password)
    {
        return Merge(DockerResourceConfiguration, new OpenGaussConfiguration(password: password))
            .WithEnvironment("GS_PASSWORD", password)
            .WithEnvironment("PGPASSWORD", password);
    }

    public override OpenGaussContainer Build()
    {
        Validate();

        // By default, the base builder waits until the container is running. For OpenGauss, a more advanced waiting strategy is necessary.
        // If the user does not provide a custom waiting strategy, append the default OpenGauss waiting strategy.
        var openGaussBuilder = DockerResourceConfiguration.WaitStrategies.Count() > 1
            ? this
            : WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(OpenGaussPort)
                    .AddCustomWaitStrategy(
                        new WaitUntil(DockerResourceConfiguration),
                        waitStrategy => waitStrategy.WithTimeout(TimeSpan.FromMinutes(2))));

        return new OpenGaussContainer(openGaussBuilder.DockerResourceConfiguration);
    }

    protected override OpenGaussBuilder Init()
    {
        return base.Init()
            .WithPortBinding(OpenGaussPort, true)
            .WithPrivileged(true)
            .WithDatabase(DefaultDatabase)
            .WithUsername(DefaultUsername)
            .WithPassword(DefaultPassword);
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

    protected override OpenGaussBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new OpenGaussConfiguration(resourceConfiguration));
    }

    protected override OpenGaussBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new OpenGaussConfiguration(resourceConfiguration));
    }

    protected override OpenGaussBuilder Merge(OpenGaussConfiguration oldValue, OpenGaussConfiguration newValue)
    {
        return new OpenGaussBuilder(new OpenGaussConfiguration(oldValue, newValue));
    }

    private sealed class WaitUntil : IWaitUntil
    {
        private readonly IList<string> _command;

        public WaitUntil(OpenGaussConfiguration configuration)
        {
            var password = EscapeBashSingleQuotes(configuration.Password ?? string.Empty);

            // Use bash to keep the invocation simple and allow quoting.
            _command = new List<string>
            {
                "bash",
                "-lc",
                // Always probe using the default admin user/database. Custom username/database can be provisioned after the container has started.
                $"source /home/omm/.bashrc >/dev/null 2>&1 || true; printf '%s\\n' '{password}' | gsql -h 127.0.0.1 -p {OpenGaussPort} -U {DefaultUsername} -d {DefaultDatabase} -c 'SELECT 1;'"
            };
        }

        private static string EscapeBashSingleQuotes(string value)
        {
            return value.Replace("'", "'\"'\"'");
        }

        public async Task<bool> UntilAsync(IContainer container)
        {
            var execResult = await container.ExecAsync(_command).ConfigureAwait(false);
            return 0L.Equals(execResult.ExitCode);
        }
    }
}
