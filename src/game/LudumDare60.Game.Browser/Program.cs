using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Browser;
using LudumDare60.Game;

await new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight)
    .RunAsync(new LudumDare60Game(), new BrowserPlatform());
