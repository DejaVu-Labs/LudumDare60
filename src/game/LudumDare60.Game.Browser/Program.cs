using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Browser;
using LudumDare60.Game;

await new GameEngine().RunAsync(new LudumDare60Game(), new BrowserPlatform());
