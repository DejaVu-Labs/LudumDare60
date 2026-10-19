# Product analytics

What the game reports to [GameAnalytics](https://gameanalytics.com), and which design question each event answers.
Status: first version, written on 2026-10-19. The targets come from [ftue.md](ftue.md#targets) and the loops from
[core-loop.md](core-loop.md). How the events get to GameAnalytics, and how to turn it on, is in
[build-and-deploy.md](../tech/build-and-deploy.md#product-analytics).

## Questions

The audience plays dozens of entries in a row and decides in the first 30 seconds (see
[ftue.md](ftue.md#session-length)). So the questions are about the funnel and about where players get stuck, not
about retention or money:

1. **Do players get into the game?** Sessions started, against the shop opened, and how long loading took.
2. **Do they understand the first click?** How long the first customer stands at the counter before the player
   clicks them.
3. **Where in an order do they drop off?** Each step of the repair reached, per order.
4. **Is an order paced as planned?** Seconds to each step, and the whole order against its `targetSeconds`.
5. **What confuses them?** Presses refused because something blocks a part, the hammer still down, a part not ready
   to go back, the trigger before full cock, and pauses long enough that the hint ladder would step in.
6. **How many reach the ending?** Orders completed, and the shop finished.
7. **Does touch play as well as a mouse?** Every question above, split by input device.
8. **Do players find the room's extras?** The music on the phone and the elephant statuette, and when in the session.

## Events

Every id is built by `Analytics/ShopAnalytics` in the game, except the session, the error and the dimension, which
the browser platform adds. `<order>` is the order's `id` from `orders.json` (`hoodie`, `robe`, `dog-walker`,
`clown`, `parcel`). Seconds are rounded to hundredths.

### Session

| Event | Kind | Value | When | Answers |
| --- | --- | --- | --- | --- |
| session start | automatic | - | The page loaded the SDK, early during loading. | 1 |
| `Game:Ready` | design | Seconds from the first frame to the shop open (the loading screen). | Once, when every file is in. | 1 |
| `Game:Started` | design | Seconds the title screen was up, from the shop open to the press. | Once, when the player presses on the title screen. | 1 |
| `Game:Finished` | design | Seconds from the shop open to the shop closed. | Once, when the shop runs out of orders it can play. | 6 |
| error, `Critical` | error | The error's type and message. | Once, when startup or the game stops on an error (the page shows its error text). | 1 |

The runtime download before the first frame is not in `Game:Ready`: Sentry's `load` transaction measures it (see
[Error and load reports](../tech/build-and-deploy.md#error-and-load-reports)). A session without `Game:Ready` is a
player who left during loading or hit an error.

### Order progression

| Event | Kind | Score | When |
| --- | --- | --- | --- |
| `Start:Order:<order>` | progression | - | The player clicks the customer and the revolver flies onto the mat. |
| `Complete:Order:<order>` | progression | Whole seconds from the start. | The player hands the revolver back. |

There is no `Fail`: the game has no fail state. A start without a completion is a player who closed the page during
the order (or a debug key that sent the customer away). GameAnalytics' progression funnel shows the drop-off per order
(question 3, 6) and the score its length (question 4).

### Order steps

Design events `Order:<order>:<step>`, each sent the first time it happens in the order, with the seconds since the
order started. Their counts per order form the funnel inside the order (question 3); their values the pacing (question
4).

| Step | When | FTUE moment |
| --- | --- | --- |
| `Waited` | At the start; the value is the seconds the customer stood at the counter before the click, not since the start. | "Clicking things does things" (question 2) |
| `HalfCock` | The hammer clicks into half cock. | Half cock first |
| `FirstPartOut` | A part reaches the end of its path (a pin pulled out, a screw turned out). | First part snaps, target under 15 s |
| `Cleaned` | The first part scrubbed clean. | Scrub |
| `Oiled` | The first part oiled. | Oil |
| `Replaced` | A broken part swapped for the spare. | Replace |
| `Assembled` | The revolver is back together; the test begins. | Reassemble |
| `Tested` | The hammer fell from full cock: the test is done and the customer holds out a hand. | Test |

The completion follows `Tested` when the player drags the revolver up to the customer (hand back). A step the order
does not need (no oil in order 1) is never sent.

### Order friction

Design events `Order:<order>:<count>`, sent once when the order completes, right after `Complete`. Each is sent even
when it is zero, so GameAnalytics' mean is per completed order (question 5).

| Count | Value |
| --- | --- |
| `Blocked` | Presses on a part that something else still holds in (it shakes, the blocker flashes). |
| `HammerFirst` | Presses on a part while the hammer is down (the hammer flashes). |
| `NotReady` | Times a loose part was brought to its rail while dirty, dry, broken or blocked (it does not stick). |
| `TriggerEarly` | Trigger presses before full cock. |
| `Stalls` | Pauses of 6 s or more without progress: the first rung of the [hint ladder](ftue.md#hint-ladder). |
| `LongestPause` | The longest pause without progress, in seconds. |

Progress is anything that clicks: a part snaps or docks, a part is cleaned, oiled or replaced, the hammer reaches
half or full cock, the trigger fires, the revolver comes together or is handed back. A failed pull that snaps back
counts too: the player is acting, not stuck.

### The room

Design events `Analytics/RoomAnalytics` sends for what the player does outside the repair, each with the seconds
since the player left the title screen (question 8).

| Event | When |
| --- | --- |
| `Phone:Track:<track>` | A click on the phone starts a track; `<track>` is its file name: `blues`, `everything-you-ever-dreamed`, `two-hour-delay`. |
| `Phone:Off` | The click after the last track turns the music off. |
| `Elephant:Trumpet` | The elephant statuette on the shelf trumpets (a press while it still plays sends nothing). |

### Custom dimension

Custom dimension 01 is the kind of pointer the player last pressed with: `mouse`, `touch` or `pen`. Every event
after the first press carries it, so each question above can be split by input device (question 7).

## Reading the results

- **Loading drop-off**: sessions against `Game:Ready` count. Compare with Sentry's `load.completed: false`.
- **First click**: mean of `Order:hoodie:Waited`. Anything over a few seconds means the glowing customer does not read
  as "click me".
- **The first order as the tutorial**: the funnel `Start:Order:hoodie` → `HalfCock` → `FirstPartOut` → `Cleaned` →
  `Assembled` → `Tested` → `Complete`, and the mean value of each step against the
  [first two minutes](ftue.md#the-first-two-minutes).
- **Stuck points**: per order, the mean of `Stalls`, `LongestPause`, `HammerFirst` and `Blocked`. A high
  `HammerFirst` on order 1 means half cock is not discovered; a high `NotReady` means the dirt or crack does not read.
- **Pacing**: the `Complete` score per order against `targetSeconds` in `orders.json`, and `Game:Finished` against the
  11 minutes of the shop loop.
- **Title screen**: `Game:Ready` count against `Game:Started` count is who left on the title; the mean of `Game:Started`
  is how long "PRESS ANY KEY" takes to read.
- **Extras**: sessions with any `Phone:Track:blues` or `Elephant:Trumpet`, and their mean value: found early or late.
  How far players click through the tracks shows whether the music keeps them.

## When analytics or reporting fail

Neither may stop the game. `boot.js` imports `analytics.js` and `reporting.js` only through `startOptional`, so a
content blocker that strips a file named like a tracker, a dropped connection or a start that throws leaves a console
warning and a game without them. The platform's own import of both modules is optional too
(`OptionalImport.TryAsync`), every call into them catches what JS throws, a broken SDK only makes events wait in a
bounded queue, and a blocked or failing Sentry request is dropped.

## Rules for new events

- Every id part is 1 to 64 of `A-Z a-z 0-9 space - _ . ( ) ! ?`, at most five parts. `analytics.js` drops anything
  else with a console warning, and a game test checks the ids of every shipped order.
- Send moments, not frames: an event per step, a count summed into one event at the end of the order.
- Build the ids when the order begins (`ShopAnalytics` does it per customer), so the fixed steps allocate nothing.
- Add the event to this file, with the question it answers.
- No personal data: no free text from the player, no ids of our own. GameAnalytics adds its own anonymous user id.
