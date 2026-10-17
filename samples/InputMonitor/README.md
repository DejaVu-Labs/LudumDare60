# Input monitor

A browser sample that shows the engine's raw input as text: held keys, pointers and their buttons, wheel and
locked-pointer motion, gamepads, focus and surface size, plus the last events with their fields. Use it to check
what a browser, a device or a keyboard layout actually sends.

| Project | Contents |
| --- | --- |
| `LudumDare60.Samples.InputMonitor` | `InputTracker` folds `RawInputEvent`s into state and writes a UTF-8 report without allocating. `InputMonitorGame : IGame` rewrites the report only in frames that bring events. Plain `net10.0`, tested in `tests/LudumDare60.Samples.InputMonitor.Tests`. |
| `LudumDare60.Samples.InputMonitor.Browser` | Entry point and page. `wwwroot/monitor.js` shows the report in a `<pre>` over the canvas and locks the pointer on double-click. |

```bash
dotnet run --project samples/InputMonitor/LudumDare60.Samples.InputMonitor.Browser
```

`InputTracker` shows the rules every input consumer follows; see
[docs/tech/architecture.md](../../docs/tech/architecture.md#input). `FocusLost` and `Overflow` release every key and
button. Each pointer button has its own down and up event. Kinds a consumer does not know are ignored.
