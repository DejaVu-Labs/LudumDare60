# Art direction

Art direction for *Six Chambers* (see [concepts.md](concepts.md)). Status: draft; the palette comes from the first concept art.

## Format

- Pixel art on a strict grid. Base resolution **640x360**, integer scaling with nearest filtering.
- Every pixel is the same size: no mixed pixel sizes, no anti-aliasing, no gradients.
- 1-pixel dark outline on gameplay objects (parts, tools), light from the top left.
- The counter strip (top ~128 px) is drawn in front view. The bench (bottom ~232 px) is strict top-down.
- Scale reference: the assembled revolver is about 240 px long, a screw is 10-14 px, a customer bust is about
  96x128 px.
- At 640x360 a sprite has four times the pixels it had in the 320x180 draft. Block in large flat shapes first and
  spend detail where the player looks: the parts on the mat and the finds.

## Color logic

The palette is split by meaning, not only by material:

- **The modern world is cool:** night-city blue from the shop window and the rain, the glow of a phone screen.
- **The antique objects are warm:** brass, walnut, steel gone warm-gray with rust, yellowed paper.
- **The cutting mat is teal** with a lighter grid. It has the strongest contrast on screen, so that every
  part reads against it.

The eye goes to the warm objects first, and those are what the player works with.

Palette swaps carry variety: grip material, customer skin and clothes, and time of day in the shop window
(evening, night with rain, morning).

## Palette

32 colors, taken from the main screen concept: `art/palette/six-chambers.gpl` (Aseprite: Load Palette) and
`six-chambers.png` (one pixel per color), in the order below. The 640x360 underlays in `art/reference/` come as they
are and reduced to the palette (nearest color in OKLab, no dithering).

| Group | Colors |
| --- | --- |
| Ink | `#0d0a0a` `#0c171e` (cool) |
| Steel, warm gray | `#2c201b` `#453a30` `#5f5247` `#b3a99b` |
| Walnut, rust, copper (dark to light) | `#1e0d09` `#2b120b` `#402219` `#4c291e` `#622a14` `#6c3721` `#823d1e` `#944f2d` `#a1603b` `#b37149` `#d2834a` |
| Brass, paper, warm light | `#e2ad5f` `#fbe099` `#faebbc` |
| Cutting mat | `#054440` `#0b4f45` `#165a4a` `#306d59` (grid) `#4b6e43` (lit by the lamp) |
| Modern, cool | `#0d222c` `#16303f` `#243a5a` `#2e4a70` `#3c608c` `#5479a1` `#8da9cc` |

- There is no separate skin or plastic ramp: skin uses the warm ramp (`#b37149` to `#e2ad5f`), plastic the cool one.
- The steel grays cover few pixels of a background, but they are the main colors of the revolver parts. Do not merge
  them away.
- Keep 32 colors for now. Trim (towards 24) only after the first sprites, by merging steps that never appear next to
  each other.

## Backgrounds

Two layers in `src/game/LudumDare60.Game.Browser/wwwroot/assets/backgrounds/`, 640x360 each, reduced to the palette;
the sources and their 640x360 versions are in `art/reference/` (`bench-empty-*`, `street-*`):

- `street.png`: the rainy street outside, opaque. Drawn first, raised by 125 px (`LudumDare60Game.StreetOffset`) so
  the embankment with its lamps, railing and reflections fills the window.
- `bench.png`: the room with the bench and the mat, drawn over the street. The window panes and the door glass are
  transparent (alpha 0 or 255, nothing in between).

## Customer sheets

One PNG per customer look in `src/game/LudumDare60.Game.Browser/wwwroot/assets/customers/<look>.png` (the look is
`customer.look` in `orders.json`), listed in `AssetPaths.CustomerSheets`; the source goes in `art/sprites/`. Three
busts from the chest up, 72x96 px each, side by side (216x96), standing on the bottom edge. The game draws them 1.4
times larger, with the bottom middle on the far edge of the bench (y = 140):

1. **Presenting:** bringing the revolver in, and waiting with a hand out to take it back.
2. **Waiting:** while the repair goes on, on the phone. A customer whose waiting frame shows them elsewhere waits
   there instead of the counter (`Customers/CustomerSeats`): `robe` sits on the bench under the revolver painting,
   bottom middle at (156, 141), drawn 0.9 times larger since that bench stands at the back wall; `dog-walker` walks
   the dog on the pavement right behind the window, bottom middle at (370, 150), 1.1 times larger, the feet and most
   of the dog hidden below the sill. A customer outside is drawn between the street and the room, so the window
   frame and the wall cover them. The symptom bubble moves and scales with them and stays over the room. They switch
   places at once, without a walk.
3. **Pleased:** with the revolver back, until they are out of the door.

Sheets so far: `hoodie`, `robe` and `dog-walker`. Looks without a sheet are drawn as code placeholders. A sheet made
from a concept may leave out palette colors the character does not have (`robe` and `dog-walker` use no cool or mat
colors, so the concept's blue rim light turns gray instead of blue). A frame may be drawn at another scale than its
neighbors: the `dog-walker` waiting frame holds the whole figure and the dog, the other two frames a bust.

## Concept art prompts

For external image generators. These images are mood references only: in the Compo every asset is drawn by
hand during the event. Generators mix pixel sizes, so check a result by downscaling it to 640x360 with
nearest filtering and reducing it to the palette above.

Put the style block before every prompt so the screens look like one game.

**Style block:**

```text
Pixel art game screen, strict pixel grid at 640x360 resolution upscaled with nearest neighbor, every pixel the same size, no anti-aliasing, no blur, no mixed pixel sizes, no gradients, limited palette of about 32 colors, clean readable shapes, cozy quiet mood. Setting: a small present-day city shop of a restorer of antique 19th-century revolvers. Color logic: the modern world is cool (night-city blue, white LED light, gray plastic), the antique objects are warm (brass, walnut wood, blued steel with warm highlights, yellowed paper). No visible text or letters, no logos, no blood, no violence.
```

**Main screen (counter and bench):**

```text
Single game screen split into two zones by the front edge of a wooden counter, like Papers, Please. Upper third: front view, a customer visible from the chest up behind the counter, a modern young person in a hoodie holding a smartphone, looking at the bench with quiet curiosity. Behind them a large shop window onto an evening modern street: rain, glowing apartment towers, a bus stop light, car headlights. Lower two thirds: strict top-down orthographic view of the workbench, no perspective, a green self-healing cutting mat with a fine grid filling most of the space and having the strongest contrast on screen. On the mat an antique single-action revolver partly disassembled in exploded view: cylinder with six chambers, base pin, screws, mainspring, hammer, walnut grip panels, some parts rusty, one half-cleaned with a shiny streak, a rolled yellowed note sticking out of one chamber. Along the bottom edge a tool strip: screwdriver, brass brush, brass oil can, rag, magnifier. A smartphone lies at the right edge of the mat. The customer and street are cool-toned, the antique parts are warm and saturated.
```

**Sprite sheet (scale check):**

```text
Pixel art sprite sheet on a flat dark green background, top-down view, each item separate with space around it: antique single-action revolver assembled (about 240 pixels long), cylinder front view, cylinder side view, base pin, hammer, trigger, mainspring, three screws, two walnut grip panels (one with carved initials and three notches), a rolled paper note, one brass cartridge. Same strict pixel grid, same limited palette, 1-pixel dark outline on every item, light from the top left.
```

**Customer busts (modular check):**

```text
Pixel art character sheet, front view, five modern customers from the chest up, about 96x128 pixels each, all built on the same base body shape with different hair, clothes and skin tones: a young person in a hoodie, an old man in a robe, a woman walking a dog, a teenager with headphones, a woman in a raincoat. Each customer in three expressions: neutral, surprised, thankful. Flat dark background, same strict pixel grid and limited palette.
```
