using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Browser;
using LudumDare60.Samples.SoundBoard;

await new GameEngine(SoundBoardGame.VirtualWidth, SoundBoardGame.VirtualHeight)
    .RunAsync(new SoundBoardGame(), new BrowserPlatform());
