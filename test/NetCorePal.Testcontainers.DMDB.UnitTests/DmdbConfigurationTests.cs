namespace Testcontainers.DMDB.UnitTests;

public sealed class DmdbConfigurationTests
{
    [Fact]
    public void Configuration_Should_Combine_Values()
    {
        var oldValue = new DmdbConfiguration(database: "db1", username: "u1", password: "p1", dbaPassword: "dba1");
        var newValue = new DmdbConfiguration(database: "db2");

        var merged = new DmdbConfiguration(oldValue, newValue);

        Assert.Equal("db2", merged.Database);
        Assert.Equal("u1", merged.Username);
        Assert.Equal("p1", merged.Password);
        Assert.Equal("dba1", merged.DbaPassword);
    }
}
