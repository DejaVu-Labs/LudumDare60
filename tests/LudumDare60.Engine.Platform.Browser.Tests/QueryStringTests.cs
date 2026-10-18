namespace LudumDare60.Engine.Platform.Browser.Tests;

public sealed class QueryStringTests
{
    [Theory]
    [InlineData("?debug")]
    [InlineData("debug")]
    [InlineData("?debug=")]
    [InlineData("?debug=1")]
    [InlineData("?lang=en&debug")]
    [InlineData("?debug&lang=en")]
    [InlineData("?lang=en&debug=true&v=3")]
    [InlineData("?DEBUG")]
    public void Has_ParameterPresent_ReturnsTrue(string query) => Assert.True(QueryString.Has(query, "debug"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("?debugger")]
    [InlineData("?lang=debug")]
    [InlineData("?nodebug&x")]
    [InlineData("?&&")]
    public void Has_ParameterAbsent_ReturnsFalse(string? query) => Assert.False(QueryString.Has(query, "debug"));
}
