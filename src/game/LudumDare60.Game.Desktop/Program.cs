using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Desktop;
using LudumDare60.Game;

// --renderer opengl draws with OpenGL 3.3 instead of Vulkan.
if (!DesktopArguments.TryGetRenderer(args, out var renderer, out var error))
{
    await Console.Error.WriteLineAsync(error);
    return 2;
}

// Twice the virtual screen: pixel art stays sharp at an integer scale.
var platform = new DesktopPlatform("LudumDare60", LudumDare60Game.VirtualWidth * 2, LudumDare60Game.VirtualHeight * 2, renderer);
var game = new LudumDare60Game
{
    HasDebugKeys = args.Contains("--debug", StringComparer.Ordinal),
    Analytics = platform.Analytics,
};
await new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight)
    .RunAsync(game, platform);
return 0;
