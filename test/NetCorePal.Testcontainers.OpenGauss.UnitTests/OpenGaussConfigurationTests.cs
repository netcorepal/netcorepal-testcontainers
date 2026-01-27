namespace Testcontainers.OpenGauss.UnitTests;

public sealed class OpenGaussConfigurationTests
{
    [Fact]
    public void Configuration_Should_Combine_Values()
    {
        var oldValue = new OpenGaussConfiguration(database: "db1", username: "u1", password: "p1", dbCompatibility: "PG");
        var newValue = new OpenGaussConfiguration(database: "db2", dbCompatibility: "A");

        var merged = new OpenGaussConfiguration(oldValue, newValue);

        Assert.Equal("db2", merged.Database);
        Assert.Equal("u1", merged.Username);
        Assert.Equal("p1", merged.Password);
        Assert.Equal("A", merged.DbCompatibility);
    }
}
