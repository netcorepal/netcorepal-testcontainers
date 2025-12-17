namespace Testcontainers.KingbaseES.UnitTests;

public sealed class KingbaseESConfigurationTests
{
    [Fact]
    public void Configuration_Should_Combine_Values()
    {
        var oldValue = new KingbaseESConfiguration(database: "db1", username: "u1", password: "p1");
        var newValue = new KingbaseESConfiguration(database: "db2");

        var merged = new KingbaseESConfiguration(oldValue, newValue);

        Assert.Equal("db2", merged.Database);
        Assert.Equal("u1", merged.Username);
        Assert.Equal("p1", merged.Password);
    }
}
