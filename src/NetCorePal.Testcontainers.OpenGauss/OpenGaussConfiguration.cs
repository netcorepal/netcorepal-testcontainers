using Docker.DotNet.Models;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Testcontainers.OpenGauss;

/// <inheritdoc cref="ContainerConfiguration" />
public sealed class OpenGaussConfiguration : ContainerConfiguration
{
    public OpenGaussConfiguration(
        string? database = null,
        string? username = null,
        string? password = null)
    {
        Database = database;
        Username = username;
        Password = password;
    }

    public OpenGaussConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    public OpenGaussConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    public OpenGaussConfiguration(OpenGaussConfiguration resourceConfiguration)
        : this(new OpenGaussConfiguration(), resourceConfiguration)
    {
    }

    public OpenGaussConfiguration(OpenGaussConfiguration oldValue, OpenGaussConfiguration newValue)
        : base(oldValue, newValue)
    {
        Database = BuildConfiguration.Combine(oldValue.Database, newValue.Database);
        Username = BuildConfiguration.Combine(oldValue.Username, newValue.Username);
        Password = BuildConfiguration.Combine(oldValue.Password, newValue.Password);
    }

    public string? Database { get; }

    public string? Username { get; }

    public string? Password { get; }
}
