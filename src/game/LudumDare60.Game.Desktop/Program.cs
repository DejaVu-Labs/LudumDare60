using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Desktop;
using LudumDare60.Game;

// Twice the virtual screen: pixel art stays sharp at an integer scale.
var platform = new DesktopPlatform("LudumDare60", LudumDare60Game.VirtualWidth * 2, LudumDare60Game.VirtualHeight * 2);
var game = new LudumDare60Game
{
    HasDebugKeys = args.Contains("--debug", StringComparer.Ordinal),
};
await new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight)
    .RunAsync(game, platform);
