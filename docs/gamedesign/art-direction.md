# Art direction

Art direction for *Six Chambers* (see [concepts.md](concepts.md)). Status: draft, before concept art.

## Format

- Pixel art on a strict grid. Base resolution **640x360**, integer scaling with nearest filtering.
- Every pixel is the same size: no mixed pixel sizes, no anti-aliasing, no gradients.
- 1-pixel dark outline on gameplay objects (parts, tools), light from the top left.
- The counter strip (top ~128 px) is drawn in front view. The bench (bottom ~232 px) is strict top-down.
- Scale reference: the assembled revolver is about 120 px long, a screw is 5-7 px.

## Color logic

The palette is split by meaning, not only by material:

- **The modern world is cool:** night-city blue from the shop window, white LED light, gray plastic.
- **The antique objects are warm:** brass, walnut, blued steel with warm highlights, yellowed paper.
- **The cutting mat is green** with a lighter grid. It has the strongest contrast on screen, so that every
  part reads against it.

The eye goes to the warm objects first, and those are what the player works with.

Palette swaps carry variety: grip material, customer skin and clothes, and time of day in the shop window
(evening, night with rain, morning).

## Palette (draft)

29 colors. Trim to about 24 after the first concept sprites: merge steps that never appear next to each other.

| Group | Colors |
| --- | --- |
| Ink | `#0e0f17` |
| Modern, cool | `#1a2233` `#2a3a52` `#44607f` `#7a9cbc` `#c4d8e8` `#f2f7fa` |
| Plastic | `#5e636e` `#9aa0aa` |
| Cutting mat | `#173a30` `#245c48` `#3f8466` `#5fa585` (grid) |
| Brass | `#6b4a1f` `#a8782b` `#d9ab45` `#f5dc8a` |
| Walnut | `#3f2419` `#6e3f2a` `#9c6340` |
| Blued steel | `#23262f` `#444a57` (highlights reuse `#7a9cbc` and `#f5dc8a`) |
| Rust | `#8e3b1f` `#c45f2a` |
| Paper | `#e2d3b0` `#fff3d6` |
| Skin | `#8a5a44` `#c98e6b` `#f0c7a0` |

## Concept art prompts

For external image generators. These images are mood references only: in the Compo every asset is drawn by
hand during the event. Generators mix pixel sizes, so check a result by downscaling it to 640x360 with
nearest filtering and reducing it to the palette above.

Put the style block before every prompt so the screens look like one game.

**Style block:**

```text
Pixel art game screen, strict pixel grid at 640x360 resolution upscaled with nearest neighbor, every pixel the same size, no anti-aliasing, no blur, no mixed pixel sizes, no gradients, limited palette of about 24 colors, clean readable shapes, cozy quiet mood. Setting: a small present-day city shop of a restorer of antique 19th-century revolvers. Color logic: the modern world is cool (night-city blue, white LED light, gray plastic), the antique objects are warm (brass, walnut wood, blued steel with warm highlights, yellowed paper). No visible text or letters, no logos, no blood, no violence.
```

**Main screen (counter and bench):**

```text
Single game screen split into two zones by the front edge of a wooden counter, like Papers, Please. Upper third: front view, a customer visible from the chest up behind the counter, a modern young person in a hoodie holding a smartphone, looking at the bench with quiet curiosity. Behind them a large shop window onto an evening modern street: rain, glowing apartment towers, a bus stop light, car headlights. Lower two thirds: strict top-down orthographic view of the workbench, no perspective, a green self-healing cutting mat with a fine grid filling most of the space and having the strongest contrast on screen. On the mat an antique single-action revolver partly disassembled in exploded view: cylinder with six chambers, base pin, screws, mainspring, hammer, walnut grip panels, some parts rusty, one half-cleaned with a shiny streak, a rolled yellowed note sticking out of one chamber. Along the bottom edge a tool strip: screwdriver, brass brush, brass oil can, rag, magnifier. A smartphone lies at the right edge of the mat. The customer and street are cool-toned, the antique parts are warm and saturated.
```

**Sprite sheet (scale check):**

```text
Pixel art sprite sheet on a flat dark green background, top-down view, each item separate with space around it: antique single-action revolver assembled (about 120 pixels long), cylinder front view, cylinder side view, base pin, hammer, trigger, mainspring, three screws, two walnut grip panels (one with carved initials and three notches), a rolled paper note, one brass cartridge. Same strict pixel grid, same limited palette, 1-pixel dark outline on every item, light from the top left.
```

**Customer busts (modular check):**

```text
Pixel art character sheet, front view, five modern customers from the chest up, about 48x64 pixels each, all built on the same base body shape with different hair, clothes and skin tones: a young person in a hoodie, an elderly woman in a cardigan, a man in a work jacket, a teenager with headphones, a woman in a raincoat. Each customer in three expressions: neutral, surprised, thankful. Flat dark background, same strict pixel grid and limited palette.
```
