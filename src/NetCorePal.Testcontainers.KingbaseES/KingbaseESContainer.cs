using DotNet.Testcontainers.Containers;

namespace Testcontainers.KingbaseES;

/// <inheritdoc cref="DockerContainer" />
public sealed class KingbaseESContainer : DockerContainer, IDatabaseContainer
{
    private readonly KingbaseESConfiguration _configuration;

    public KingbaseESContainer(KingbaseESConfiguration configuration)
        : base(configuration)
    {
        _configuration = configuration;
    }

    public string GetConnectionString()
    {
        var properties = new Dictionary<string, string>();
        properties.Add("Host", Hostname);
        properties.Add("Port", GetMappedPublicPort(KingbaseESBuilder.KingbaseESPort).ToString());
        properties.Add("Database", _configuration.Database ?? KingbaseESBuilder.DefaultDatabase);
        properties.Add("Username", _configuration.Username ?? KingbaseESBuilder.DefaultUsername);
        properties.Add("Password", _configuration.Password ?? KingbaseESBuilder.DefaultPassword);
        properties.Add("Timeout", "30");
        return string.Join(";", properties.Select(property => string.Join("=", property.Key, property.Value)));
    }
}
