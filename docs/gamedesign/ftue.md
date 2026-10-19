# First-time user experience

How a new player gets from the itch.io page into the [core loop](core-loop.md) of *Six Chambers*. Status: draft,
written on 2026-10-18.

## Session length

The audience is Ludum Dare participants rating other entries (see
[What the audience rewards](concepts.md#what-the-audience-rewards)). They play dozens of games in a row, mostly in
the browser, usually for 2-10 minutes each, and they decide in the first 30 seconds whether a game is worth their
time. Many close the tab as soon as they have "got it". So:

- **The first minute sells the game.** By then the player has made a part click into place and has seen a find.
- **The first order is a complete experience.** Someone who leaves after 2 minutes can still rate the game fairly.
- **The whole game fits in about 11 minutes**, so a rater who likes it reaches the ending instead of quitting
  halfway. Finishing is what earns the Mood and Overall votes.

### Targets

Measured from the click on "Run game" on itch.io, on a broadband connection:

| Moment | Target |
| --- | --- |
| Something drawn on screen (poster frame) | < 1 s |
| Game ready, first input accepted | < 5 s |
| First part snaps | < 15 s after the first click in the game |
| First find | < 60 s |
| First order done | < 2 min |
| Ending | about 11 min, 15 min at most |

The game reports these moments to GameAnalytics, so the targets can be checked against real players (see
[analytics.md](analytics.md)).

If playtests run long, cut steps from orders before cutting orders: five short orders read as a richer game than
three long ones.

## Where players drop off, and the answer to each

| Risk | Answer |
| --- | --- |
| Black canvas while WASM boots | The host page shows a static poster of the shop (the title screen image) at once; the live scene replaces it without a jump |
| A menu before any play | No menu. The title screen *is* the shop; one click opens it |
| Audio stays silent (browsers need a gesture) | That same first click unlocks audio, so the door bell is the first sound |
| "What do I do?" | Only one part glows at the start; the cursor shows the verb; the hint ladder below |
| A gesture the player cannot guess (circular unscrew) | It is not in order 1. When it comes, the ghost hand shows it, and any circular motion works |
| Pixel hunting for small parts | Every movable part has a hit area of at least 24x24 px, larger than its sprite |
| "A gun game?" | The first thing in the game is a person, a shop and rain; the gun is a tool to open, never to fire at anything |
| Stuck with no way forward | There is no fail state; a part that does not fit says why (its dirt or crack flashes) |
| Long story at the start | No intro. The story starts with the first find and grows only through objects and messages |

## Principles

1. **The first order is the tutorial.** No separate tutorial level, no text boxes. Order 1 has only two moving
   parts and uses five verbs, each once.
2. **One new thing at a time.** One verb per order (see the [order table](core-loop.md#orders)). A new verb
   always appears on a part that is the only one glowing.
3. **Show, then hint, then demonstrate.** The glow is always on; stronger hints come only when the player stalls.
4. **Every touch answers.** Even a wrong drag gets a sound and a wiggle, so the player learns the rules by
   poking.
5. **No text that teaches.** Allowed text: the title, the writing on finds, phone messages, the ending and
   credits. Symptoms are icons.

## The first two minutes

Order 1: young customer in a hoodie, jammed cylinder, a rolled note in one chamber.

| Time | Screen | Player | Feedback | Teaches |
| --- | --- | --- | --- | --- |
| 0 s | Poster: the shop at night, rain on the window, title *Six Chambers*, small loading bar | Waits | Bar fills | - |
| ~4 s | Live scene, same picture. The door sign reads CLOSED and pulses; label "Open shop" on hover | Clicks the sign | Sign flips to OPEN, rain ambience fades in, door bell | Clicking things on screen does things |
| ~6 s | Customer walks in and holds the revolver out; the customer glows | Clicks the customer | The revolver flies onto the mat: thud, light screen shake, symptom bubble: cylinder with a lock | This is a request; what glows can be clicked |
| ~8 s | The **hammer** glows; nothing else does | Pulls its spur back | Click into the half-cock notch; the hammer stays back | **Half cock** first, as a gunsmith would |
| ~11 s | The **base pin** glows. Cursor over it: grab hand with an axis arrow | Drags the pin out | Friction, rising pitch, click; pin rolls onto the mat | **Pull** along a line |
| ~15 s | The **cylinder** glows | Drags it out sideways | Slide, click; customer leans in | Removing a part unblocks the next one |
| ~20 s | A rolled note sticks out of one chamber and glows | Clicks it | Paper unrolls over the bench: a few handwritten lines. Customer: surprised face | **Finds**: the hook of the game |
| ~35 s | Note flies into the finds box. Cylinder shows rust; the wire brush beside it glows (a press on the cylinder flashes it) | Takes the brush, rubs | Rust erased pixel by pixel, a rasp that follows the strokes; at 90 % glint and chime | **Scrub** |
| ~50 s | Faint outline of the cylinder in the frame | Drags it back | Snap. Then the pin outline; snap | **Reassemble** in reverse |
| ~60 s | The hammer glows again | Pulls it on to full cock, clicks the trigger | Cylinder turns one chamber, dry-fire click. Gun shines | **Test** |
| ~70 s | Customer holds out a hand | Drags the gun up to the counter | Thankful face, phone photo of the note, door bell | **Hand back** |
| ~80 s | Phone buzzes on the bench | Clicks it (optional) | A short message from the customer | Messages carry today's story |
| ~90 s | Next customer comes in, or at once if the counter bell is rung | - | - | The loop repeats |

Order 1 is about 90 seconds for a player who never stalls, and under 2 minutes with a few hints.

## Hint ladder

Runs in every order. The timer resets on any progress (a part changes state, or dirt is removed).

| After | Hint |
| --- | --- |
| 0 s | Available parts glow softly. Hover shows the verb cursor |
| 6 s without progress | The next part glows brighter and wiggles once |
| 12 s without progress | A ghost hand performs the gesture on that part once (the path, the direction) |
| Wrong action | Dragging a blocked part: it shakes, the part blocking it flashes. A dirty or broken part at its slot: it bounces back, the dirt or crack flashes |

From order 3 on the delays double (12 s and 24 s): the player knows the controls by then.

## Affordances

- **Glow** marks what can move now. Nothing else on the bench animates, so the glow always means "you can touch
  this".
- **Cursor per verb:** grab hand (pull, lift, hand back), circular arrows (unscrew), oil can (oil), pointing finger
  (click a find or the phone). Scrubbing needs no cursor: the player holds the brush itself.
- **Outline at the slot** marks where a removed part goes back.
- **Sound pitch** rises as a part nears its snap point, so the player feels when to let go.
- **Customer face** reacts to progress: leans in when parts come off, surprised at a find, thankful at the end.

## Controls

- Mouse, left button only. Touch works through the same pointer path.
- Every gesture is forgiving: drags project onto the part's path, so a sloppy drag still moves the part; unscrew
  accepts circular motion in either direction.
- One small mute icon in a corner; the itch.io embed adds its own fullscreen button. No pause (there is nothing
  to pause), no settings screen.
- No save. The game is short; a reload starts over.

## The ending

All five finds are laid out on the mat in the order they were found, the last phone message arrives, the credits
roll over the shop at morning light, and a "Play again" button resets to order 1. The finds layout is also the
screenshot for the itch.io and LD pages.

## The page before the game

The itch.io page and the LD entry are part of the first impression:

- **Cover image and first screenshot:** a find on the mat next to a half-cleaned part, with the customer at the
  counter. Not the gun alone (see the [topic risk](concepts.md#risks-and-cuts)).
- **One-line pitch:** "Restore antique revolvers and find what they have been hiding for 140 years."
- **Controls line:** "Mouse only. About 10 minutes. Sound on."
- Embed size 1280x720 (640x360 at x2, see [build-and-deploy.md](../tech/build-and-deploy.md)).

## Playtesting the FTUE

Watch two or three people who have not seen the game. Explain nothing and do not help. Note:

- the time to the first snap, the first find and the end of order 1;
- every moment they stall for more than 5 seconds, and what they tried;
- whether they open the phone message;
- whether they say what the game is about after order 1.

Fix the stalls first: a stall in the first minute costs more ratings than anything later in the game.

## Engine and host work this needs

Tracked in [docs/backlog.md](../backlog.md#ftue-support):

- Poster frame and loading bar in the host page, replaced by the first rendered frame.
- Cursor sprites per verb (the game draws the cursor; the platform hides the system cursor over the canvas).
- Idle timer and hint state in the order logic, tested without a browser.
