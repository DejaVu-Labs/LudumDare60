using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Browser;
using LudumDare60.Game;

var platform = new BrowserPlatform();
var game = new LudumDare60Game
{
    HasDebugKeys = PageAddress.HasQueryParameter("debug"),
    Analytics = platform.Analytics,
};
await new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight)
    .RunAsync(game, platform);
