using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Browser;
using LudumDare60.Game;

var game = new LudumDare60Game { HasDebugKeys = PageAddress.HasQueryParameter("debug") };
await new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight)
    .RunAsync(game, new BrowserPlatform());
