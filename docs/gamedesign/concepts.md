# Concepts

Theme: **Old Tech**. Concept, written on 2026-10-17.

**Chosen direction: *Six Chambers*** (working title), a cozy pixel-art game about a modern-day restorer who
takes apart, cleans and repairs antique revolvers that families bring in.

## Time and tech budget

- We aim for the **Compo** (48 h, solo, every asset made during the event) and fall back to the Jam (72 h)
  if the core loop is not playable in time. At 2026-10-17 09:30 UTC the Compo countdown showed about
  **1 day 12 hours** left.
- The engine has a frame loop and nothing else: no renderer, no input, no audio. Every hour spent on
  engine work comes out of game time.
- Renderer: **WebGL2 only for the Compo**, behind the backend-neutral renderer interface. WebGPU comes later
  with the same command stream and instance format. See [Rendering](#rendering).

## What the audience rewards

Data pulled from the ldjam.com API for LD56 to LD59 (top 8 Overall per category, plus Innovation, Theme and
Mood leaders for LD58 and LD59).

**Who rates.** Only participants rate. In LD59, 1637 games were published (1208 Jam, 367 Compo) and 1133 got
20+ ratings. Raters are developers who play dozens or hundreds of entries in 2-10 minute sessions, mostly in
the browser. They reward a game that explains itself in the first 30 seconds and leaves one memorable idea.

**Patterns among recent winners:**

1. **One mechanic, explored deeply.** *An Angler's Light* (LD57 Compo #1): your light attracts prey and
   reveals you. *Depth of Field* (LD57 Compo #7): the only control is camera focus.
2. **The theme read as a pun or a physical property, not a setting.** *Depth of Field*, *Deep Fry*,
   *Puppeteer* (depth as perspective), *Call Hector* (LD58 Theme #1). Theme and Innovation leaders reinterpret
   the word; Overall leaders still stay close to it.
3. **Diegetic interfaces and fake hardware.** *My Keyboard is Full of Ants!* (LD56 Compo #1, the real keyboard
   is the playfield), *Call Hector*, *Windows Explorer 2* (LD58 Innovation #3), *KingdomOS*, *Unstable98.exe*
   (LD49). The audience likes being tricked by the interface itself.
4. **Incrementals keep winning.** *Idle Badger* (LD58 Compo #1, also Theme #1 and Mood #1), *Signal Garden*
   (LD59 Jam #1), *Maginot* (LD56 Compo #2). Short, satisfying, number-go-up, done in 15 minutes.
5. **Lonely operator at a console.** *Pyre Lookout* (LD59 Jam #7, Mood #1), *SETI@dream*, *Cosmic Relay*,
   *SIGNALITE*. A desk, a machine, a stream of requests: strong Mood scores.
6. **Cute protagonist plus humor.** Ducks, hamsters, badgers, moles, rats, worms. Almost every top-8 list has
   one.
7. **Polish over size.** Small 2D games with a strict palette, juicy feedback and instant restart beat larger
   unfinished ones.

**Saturation risk for this theme** (from the first hours of LD60 posts): CRT shaders as decoration, fake retro
desktops, steampunk in brown and gray, "the platformer changes console generation", defragmentation puzzles,
ancient alien ruins, the Antikythera mechanism. LD36 already had "Ancient Technology". A retro skin alone will
not stand out: the old-tech property must *be* the mechanic.

## Six Chambers

> A small restoration shop in a modern city. People bring in the antique revolvers their families kept for
> generations, and while you take each one apart you find what it has been hiding for 140 years.

### Fantasy and pillars

The player is a careful restorer of antiques. Nobody fires a shot in this game: it is about people, their
families and the objects they kept.

1. **Every touch answers.** Each action has resistance, a click into place and a sound. Feel comes from code
   and feedback, not from detailed art.
2. **Calm, not pressure.** No timers and no failure. A wrong move is a part that does not fit yet.
3. **Objects tell the story.** No dialogue trees. What you find inside the gun, the customer's reaction and a
   few phone messages are enough (the *Unpacking* approach).
4. **Old inside the new.** The modern world around the bench is cool, flat and disposable. The revolver is
   warm, heavy and repairable. This contrast is how the theme reads.

### Setting

Present day. The shop is a small storefront in a city: a counter, a shop window onto the street, a workbench
right behind the counter. Modern things are few and iconic (a smartphone, a laptop glow, a takeaway coffee, an
LED ring magnifier, the city outside the window) so that the antique on the mat stays the focus. Customers are
modern people, mostly descendants of the original owners.

### Screen layout

One screen, two projections, split by the front edge of the counter (the *Papers, Please* layout):

| Zone | Share of 640x360 | View | Contents |
| --- | --- | --- | --- |
| Counter | top ~128 px | front view | Customer from the chest up, shop window with the street behind |
| Bench | bottom ~232 px | strict top-down, no perspective | Green self-healing cutting mat with a grid, revolver parts, tool strip along the bottom edge, phone at the right edge |

The bench never uses perspective: constrained drags and part layout stay simple. If the exploded gun does not
fit, the customer steps back to the window while you work and the counter strip shrinks.

### Core loop

Summary below; the full loop, rules and order list are in [core-loop.md](core-loop.md), and how a new player
learns it is in [ftue.md](ftue.md).

1. A customer comes to the counter and puts down a revolver. Their request is shown as an icon of the symptom
   (jammed cylinder, broken spring, rust), not as text.
2. **Inspect.** Look the gun over with the magnifier, find dirt, rust and the broken part.
3. **Take it apart.** Unscrew screws, pull the base pin, remove the cylinder, lift the grip panels. Hidden
   objects appear here, and the customer reacts to them.
4. **Clean.** Scrub rust and dirt off each part, oil the moving parts.
5. **Repair.** Swap the broken part (spring, pin, hand) for a new one from the drawer.
6. **Reassemble.** Parts only go back in the right order. A part that cannot fit yet stays on the mat.
7. **Test and return.** Cock the hammer, turn the cylinder, dry-fire click. The customer takes the gun and the
   find. Later a message on the phone tells a bit more.

### Verbs

All interactions are **constrained drags**: a part moves along one predefined path (a line, a circle or an
arc) with resistance, a snap point and a sound at the end. No physics engine.

| Verb | Input | Feedback |
| --- | --- | --- |
| Unscrew | circular mouse motion around the screw | squeak per turn, screw rises, pops out |
| Pull out | drag along the part's axis | friction, then a click when free |
| Swing / lift | drag along an arc | hinge creak, click at the stop |
| Scrub | rub the mouse over a part | rust pixels erased, brush loop sound, clean metal glint |
| Oil | click a joint | drop sound, part moves smoother afterwards |
| Replace | drag a new part from the drawer | metal clink, snap into the slot |
| Test | drag the hammer back, click the trigger | ratchet per chamber, dry-fire click |

Parts that can move right now get a subtle highlight, so the player always knows what comes off next.

### Customers and story

**Two timelines.** The finds inside the guns come from the 1870s-1900s: a rolled note in a chamber, one unspent
cartridge, initials carved in the grip, notches on the grip edge, a dried flower, a photo under a grip panel.
The customers and their phone messages are from today. The finds are never explained; the player connects them
across orders. The last order joins the two timelines (for example, all the guns turn out to belong to one
family, or the last one is the restorer's own).

**Variety from one gun model:** dirt and rust amount, grip material (palette swap), one fault and one or two
finds per order.

**Customer art is modular:** one base bust (about 96x128 px) with swappable head, hair and clothing, palette
swaps for skin and clothes, three expressions (neutral, surprised at a find, thankful) and a two-frame blink.

**MVP content:** one revolver model, 4-5 customers, one fault and one find each, a short ending.

### Art direction

Pixel art at 640x360 on a strict grid. Palette, color logic and concept art prompts live in
[art-direction.md](art-direction.md).

### Audio

About eight sounds, recorded at home with a phone (Compo rules allow self-made recordings): click, cylinder
ratchet, screw squeak, metal clink, brush loop, oil drop, spring, cock and dry-fire click. Pens, a stapler,
keys, scissors and a lighter cover all of them. Ambience: rain on the shop window, distant traffic, a phone
buzz for messages.

### Rendering

Pixel art drives the renderer:

- **Low-resolution target.** The scene is drawn into a 640x360 render target and scaled to the canvas by an
  integer factor with nearest filtering (x2 = 1280x720, x3 = 1920x1080), letterboxed if needed.
  The math lives in `PixelViewport` (see [architecture.md](../tech/architecture.md#screen)).
- **Sprite batcher.** One texture per file: a PNG or Aseprite file that holds an animation is one sprite sheet,
  and a frame is a source rectangle. No atlas packing. Each sprite is one instanced quad; the instances go into a
  preallocated array, are uploaded with one `bufferSubData` and drawn with one instanced draw call per run of
  sprites with the same texture. See [architecture.md](../tech/architecture.md#rendering).
- **Palette in the art, not in the shader.** Assets are plain RGBA. Palette swaps (grip materials, customer
  clothes and skin, time of day) are separate sprites or a per-sprite tint, not indexed colors.
- **Dirt layers.** Dirt or rust is its own RGBA sprite over the part, at the part's size, so cleaning works pixel
  by pixel and the art can be drawn as a layer. The brush erases its pixels with a little dither, and the CPU
  counts the dirt left exactly (`Cleaning/DirtLayer`). Changed rows are re-uploaded with `texSubImage2D`
  (`Graphics.UpdateTexture`), one range per frame. A mask sampled by the sprite shader would have needed a bigger
  instance format; the layer needs none.
- **WebGL2 only for the Compo**, behind the backend-neutral renderer interface. WebGPU follows later with the
  same command stream and instance format.

### Plan (36 h, ~28 working hours)

| Block | Hours | Done when |
| --- | --- | --- |
| Platform | ~4 | WebGL2 sprite batcher and 640x360 target, mouse input, sound playback, first Release publish |
| Disassembly | ~5 | Parts move on paths with resistance and snap; ordered disassembly and reassembly |
| Cleaning | ~3 | Dirt mask scrubbing, oiling |
| **Decision point** | ~12 h from start | One gun can be taken apart, cleaned and put back. If not: switch to the Jam |
| Customers | ~6 | Modular busts with three expressions; 4-5 orders with fault, find and reaction |
| Gun and shop art | ~4 | Revolver parts, cutting mat, tool strip, counter and shop window |
| Polish | ~3 | Shake, clicks, phone messages, title screen, ending |
| Submission | ~2 | Final publish, itch.io upload, LD page, screenshots |

Release AOT takes several minutes, so publish a Release build at the end of the first block, not only before
the deadline.

The art hours were estimated for 320x180. At 640x360 every sprite has four times the pixels: re-estimate the art
blocks once the first revolver part is drawn, and cut early rather than late.

### Risks and cuts

- **Feel.** If dragging does not feel good, nothing else matters. Tune it before any content.
- **Bench space.** 640x232 px should fit an exploded revolver. Check the layout with real sprites early.
- **Customer art cost.** About 6 h for 4-5 customers, estimated at 48x64 px; at 96x128 px expect more. Fallback:
  all customers as silhouettes against the shop window on one shared figure, different only in outline and color.
- **Modern clutter.** More than five or six modern objects on screen kill the contrast.
- **Topic.** A game about guns can look violent at first glance. Screenshots and the page must show antiques,
  the bench, the customers and the finds.
- **Cut first:** oiling, the parts drawer (repair by cleaning only), phone messages, the ending, the second
  grip material.

## Sources

- ldjam.com API (`api.ldjam.com/vx`): LD56-LD59 results feeds, LD58/LD59 event stats, LD60 event posts.
- [The winners of Ludum Dare, and what to learn from them](https://ratking.de/blog/2014/01/31/the-winners-of-ludum-dare/)
- [A peek inside some of the top games from Ludum Dare 49](https://github.blog/open-source/gaming/a-peek-inside-some-of-the-top-games-from-ludum-dare-49/)
