using Docker.DotNet.Models;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Testcontainers.DMDB;

/// <inheritdoc cref="ContainerConfiguration" />
public sealed class DmdbConfiguration : ContainerConfiguration
{
    public DmdbConfiguration(
        string? database = null,
        string? username = null,
        string? password = null,
        string? dbaPassword = null)
    {
        Database = database;
        Username = username;
        Password = password;
        DbaPassword = dbaPassword;
    }

    public DmdbConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    public DmdbConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    public DmdbConfiguration(DmdbConfiguration resourceConfiguration)
        : this(new DmdbConfiguration(), resourceConfiguration)
    {
    }

    public DmdbConfiguration(DmdbConfiguration oldValue, DmdbConfiguration newValue)
        : base(oldValue, newValue)
    {
        Database = BuildConfiguration.Combine(oldValue.Database, newValue.Database);
        Username = BuildConfiguration.Combine(oldValue.Username, newValue.Username);
        Password = BuildConfiguration.Combine(oldValue.Password, newValue.Password);
        DbaPassword = BuildConfiguration.Combine(oldValue.DbaPassword, newValue.DbaPassword);
    }

    public string? Database { get; }

    public string? Username { get; }

    public string? Password { get; }

    public string? DbaPassword { get; }
}
