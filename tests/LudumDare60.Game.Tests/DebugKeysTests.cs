using LudumDare60.Engine;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;
using LudumDare60.Game.Customers;

namespace LudumDare60.Game.Tests;

public sealed class DebugKeysTests
{
    private const double Step = 1.0 / 60;

    private readonly ManualPlatform _platform = new() { Assets = new ShippedAssets() };

    private static RawInputEvent KeyDown(Key key) => new(RawInputEventKind.KeyDown, 0, (ushort)key, 0, 0, 0, 0, 0);

    private static RawInputEvent KeyUp(Key key) => new(RawInputEventKind.KeyUp, 0, (ushort)key, 0, 0, 0, 0, 0);

    private LudumDare60Game Start(bool debug)
    {
        var game = new LudumDare60Game { HasDebugKeys = debug, WaitsForPlayer = false };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, _platform);
        _platform.Frame(Step);
        Assert.NotNull(game.Shop);
        return game;
    }

    private void Press(Key key)
    {
        _platform.Frame(Step, KeyDown(key));
        _platform.Frame(Step, KeyUp(key));
    }

    [Theory]
    [InlineData(Key.Digit1, "hoodie")]
    [InlineData(Key.Digit3, "dog-walker")]
    [InlineData(Key.Digit5, "parcel")]
    [InlineData(Key.Numpad2, "robe")]
    public void DigitKey_WithDebugKeys_ThatOrdersCustomerComesInAtOnce(Key key, string orderId)
    {
        var game = Start(debug: true);

        Press(key);

        Assert.Equal(ShopPhase.Arriving, game.Shop!.Phase);
        Assert.Equal(orderId, game.Bench!.Order.Id);
    }

    [Fact]
    public void Digit0_WithDebugKeys_TheCurrentCustomerComesInAgain()
    {
        var game = Start(debug: true);
        Press(Key.Digit4);
        var bench = game.Bench;
        for (var i = 0; i < 200; i++)
        {
            _platform.Frame(Step);
        }

        Assert.Equal(ShopPhase.Offering, game.Shop!.Phase);

        Press(Key.Digit0);

        Assert.Equal(ShopPhase.Arriving, game.Shop.Phase);
        Assert.Equal(3, game.Shop.OrderIndex);
        Assert.NotSame(bench, game.Bench);
    }

    [Fact]
    public void DigitKey_BeyondTheOrders_DoesNothing()
    {
        var game = Start(debug: true);

        Press(Key.Digit9);

        Assert.Equal(ShopPhase.Waiting, game.Shop!.Phase);
        Assert.Equal(0, game.Shop.OrderIndex);
    }

    [Fact]
    public void EKey_WithDebugKeys_SendsTheCustomerAwayAndClosesTheShop()
    {
        var game = Start(debug: true);
        Press(Key.Digit2);

        Press(Key.E);

        Assert.Equal(ShopPhase.Closed, game.Shop!.Phase);
        Assert.Null(game.Bench);
    }

    [Fact]
    public void EKey_WithoutDebugKeys_DoesNothing()
    {
        var game = Start(debug: false);

        Press(Key.E);

        Assert.Equal(ShopPhase.Waiting, game.Shop!.Phase);
    }

    [Fact]
    public void DigitKey_WithoutDebugKeys_DoesNothing()
    {
        var game = Start(debug: false);

        Press(Key.Digit3);

        Assert.Equal(ShopPhase.Waiting, game.Shop!.Phase);
        Assert.Equal(0, game.Shop.OrderIndex);
    }

    private sealed class ShippedAssets : IAssetSource
    {
        // Reads finish at once, so a file requested in Initialize is loaded in the first frame.
        public Task<byte[]> ReadAsync(string path)
            => Task.FromResult(Read(path));

        private static byte[] Read(string path) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", path));
    }
}
