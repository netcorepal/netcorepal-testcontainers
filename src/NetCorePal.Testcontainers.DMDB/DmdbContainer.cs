using DotNet.Testcontainers.Containers;

namespace Testcontainers.DMDB;

/// <inheritdoc cref="DockerContainer" />
public sealed class DmdbContainer : DockerContainer, IDatabaseContainer
{
    private readonly DmdbConfiguration _configuration;

    public DmdbContainer(DmdbConfiguration configuration)
        : base(configuration)
    {
        _configuration = configuration;
    }

    public string GetConnectionString()
    {
        var properties = new Dictionary<string, string>();
        properties.Add("Host", Hostname);
        properties.Add("Port", GetMappedPublicPort(DmdbBuilder.DmdbPort).ToString());
        properties.Add("Database", _configuration.Database ?? DmdbBuilder.DefaultDatabase);
        properties.Add("Username", _configuration.Username ?? DmdbBuilder.DefaultUsername);
        properties.Add("Password", _configuration.Password ?? DmdbBuilder.DefaultPassword);
        properties.Add("DBAPassword", _configuration.DbaPassword ?? DmdbBuilder.DefaultDbaPassword);
        properties.Add("Timeout", "30");
        return string.Join(";", properties.Select(property => string.Join("=", property.Key, property.Value)));
    }
}
