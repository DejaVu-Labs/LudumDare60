using LudumDare60.Engine;
using LudumDare60.Engine.Platform.Browser;
using LudumDare60.Samples.InputMonitor;
using LudumDare60.Samples.InputMonitor.Browser;

await MonitorInterop.ImportAsync();
await new GameEngine().RunAsync(new InputMonitorGame(new MonitorTextDisplay()), new BrowserPlatform());
