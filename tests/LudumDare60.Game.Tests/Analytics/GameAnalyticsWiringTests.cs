using LudumDare60.Engine;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;
using LudumDare60.Game.Analytics;
using LudumDare60.Game.Customers;

namespace LudumDare60.Game.Tests.Analytics;

/// <summary>
/// <see cref="LudumDare60Game.Analytics"/> receives the shop's events while the engine runs the game.
/// </summary>
public sealed class GameAnalyticsWiringTests
{
    private const double Step = 1.0 / 60;

    private readonly ManualPlatform _platform = new() { Assets = new ShippedAssets() };
    private readonly RecordingAnalytics _recorded = new();

    [Fact]
    public void Analytics_ByDefault_IsNull()
        => Assert.Same(NullAnalytics.Instance, new LudumDare60Game().Analytics);

    [Fact]
    public void Frame_ShopOpens_SendsReadyOnceAndThenTheShopsEvents()
    {
        var game = new LudumDare60Game { Analytics = _recorded, WaitsForPlayer = false };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, _platform);

        _platform.Frame(Step);
        _platform.Frame(Step);
        var opened = _recorded.Events.ToList();
        for (var i = 0; i < 300 && game.Shop!.Phase != ShopPhase.Offering; i++)
        {
            _platform.Frame(Step);
        }

        var ready = Assert.Single(opened);
        Assert.Equal(ShopAnalytics.ReadyEvent, ready.Id);
        Assert.Equal(ShopPhase.Offering, game.Shop!.Phase);
        Assert.Single(_recorded.Events);
    }

    [Fact]
    public void Frame_TitleScreen_SendsReadyBeforeAnyPress()
    {
        var game = new LudumDare60Game { Analytics = _recorded };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, _platform);

        _platform.Frame(Step);
        _platform.Frame(Step);

        Assert.True(game.IsOnTitle);
        Assert.Equal(ShopAnalytics.ReadyEvent, Assert.Single(_recorded.Events).Id);
    }

    [Fact]
    public void Platform_WithoutAnalytics_DropsEvents()
        => Assert.Same(NullAnalytics.Instance, ((IPlatform)_platform).Analytics);

    private sealed class ShippedAssets : IAssetSource
    {
        // Reads finish at once, so a file requested in Initialize is loaded in the first frame.
        public Task<byte[]> ReadAsync(string path)
            => Task.FromResult(Read(path));

        private static byte[] Read(string path) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", path));
    }
}
