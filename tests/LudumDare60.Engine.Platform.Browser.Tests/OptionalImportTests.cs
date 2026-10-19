namespace LudumDare60.Engine.Platform.Browser.Tests;

/// <remarks>A failed import surfaces as a <c>JSException</c>, which exists only in the browser; these tests run outside
/// it, so they cover the import that works and show that other exceptions are not swallowed.</remarks>
public sealed class OptionalImportTests
{
    [Fact]
    public async Task TryAsync_ImportWorks_ReturnsTrue()
        => Assert.True(await OptionalImport.TryAsync(() => Task.CompletedTask));

    [Fact]
    public async Task TryAsync_BugOutsideJs_StillThrows()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => OptionalImport.TryAsync(() => throw new InvalidOperationException("bug")));
}
