using Docker.DotNet.Models;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Testcontainers.KingbaseES;

/// <inheritdoc cref="ContainerConfiguration" />
public sealed class KingbaseESConfiguration : ContainerConfiguration
{
    public KingbaseESConfiguration(
        string? database = null,
        string? username = null,
        string? password = null)
    {
        Database = database;
        Username = username;
        Password = password;
    }

    public KingbaseESConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    public KingbaseESConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    public KingbaseESConfiguration(KingbaseESConfiguration resourceConfiguration)
        : this(new KingbaseESConfiguration(), resourceConfiguration)
    {
    }

    public KingbaseESConfiguration(KingbaseESConfiguration oldValue, KingbaseESConfiguration newValue)
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
