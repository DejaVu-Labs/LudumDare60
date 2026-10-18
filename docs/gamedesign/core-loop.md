# Core loop

The core loop of *Six Chambers* (see [concepts.md](concepts.md)). Status: draft, written on 2026-10-18. How a new
player learns this loop is in [ftue.md](ftue.md).

## Three nested loops

| Loop | Length | What the player does | What keeps them going |
| --- | --- | --- | --- |
| **Touch** | 1-3 s | Grab a lit part, drag it along its path against resistance, feel it snap | The click and sound at the end (pillar 1) |
| **Order** | 1.5-3 min | Take one customer's revolver apart, find what it hides, fix it, put it back, hand it over | "What is inside this one?" and the customer's reaction |
| **Shop** | about 11 min | Serve five customers, collect their finds, read the phone messages | The finds start to rhyme; the last order explains how they connect |

Each loop pays off on its own. A rater who stops after the first order has still seen the whole promise: a
tactile repair, a find, a reaction.

```text
TOUCH (seconds)        hover -> part lights up -> grab -> drag along path -> resistance -> SNAP + sound
                                                                                              |
                                                                    next part lights up  <----+

ORDER (minutes)        customer arrives -> symptom icon -> take apart -> FIND -> clean / repair
                         ^                                                          |
                         |                       reaction <- hand back <- test <- reassemble
                         |                          |
                         +--- bell, next customer <-+-- phone message (a bit more of the story)

SHOP (session)         order 1 -> order 2 -> order 3 -> order 4 -> order 5 -> ending: all finds on the mat
```

## The order loop, step by step

1. **Arrive.** The door bell rings, the customer walks to the counter and puts the revolver on the mat with a
   thud. A speech bubble shows the symptom as an icon (a cylinder with a lock, a broken spring, a rust stain).
   No text.
2. **Take apart.** First the hammer goes to half cock, as on a real single-action revolver: until it clicks into
   the notch nothing moves, and a press on a part flashes the hammer. Then only the parts this order needs can move.
   Parts that can move right now glow; the rest are blocked by something on top of them. Taking a part off unblocks
   the next one.
3. **Find.** One part per order hides a find (a note in a chamber, a photo under a grip panel). It pops out when
   that part comes off; clicking it opens it full size over the bench. The customer reacts (surprised face). The
   find then goes into the **finds box** on the left of the bench, which stays on screen for the whole game.
4. **Clean and repair.** Dirty parts must be scrubbed; a broken part must be swapped for a new one from the
   drawer. A dirty or broken part *does not fit*: dragged to its slot, it bounces back and its dirt or crack
   flashes. This is the only "wrong move" in the game.
5. **Reassemble.** Removed parts show a faint outline at their slot. Parts go back in reverse order; a part
   whose blockers are not back yet does not snap.
6. **Test.** Cock the hammer (the cylinder turns one chamber with a ratchet click) and click the trigger (dry
   fire). The gun is now *done*: it gets a short shine and the customer holds out a hand.
7. **Hand back.** Drag the gun up onto the counter. The customer shows the thankful face, takes a photo of the
   find with their phone, takes the gun and leaves; the door bell rings. The find stays in the finds box, so the
   collection grows on screen.
8. **Message.** A few seconds later the phone on the bench buzzes: a short message from a customer, today's
   side of the story. Reading it is optional. The next customer arrives on their own after the message, or at
   once if the player rings the **counter bell**.

The player controls the pace. Nothing in the loop has a timer, and nothing in it can fail; the only friction is
"this part does not fit yet".

## What is built

`Customers/Shop` runs the loop on the orders of `orders.json`, one customer at a time. After a moment the door bell
rings and the customer walks from the door to the counter; the revolver drops onto the mat with a thud, and a
bubble shows the symptom icon. The hammer glows first (`Bench/ActionTest`): the player drags its spur back (it turns
around its pivot) until it clicks into the half-cock notch, where it stays; a hammer let go before the notch falls
back down, and one in the notch cannot be pushed lower. Until then every part stays put, and a press on one gives a
dull tick and flashes the hammer. Once every part of the order has been out, is back in its seat, and every dirty
part is clean, the parts stay put and the test begins: the hammer glows again, the player pulls it on to full cock
(the cylinder turns one chamber), then the trigger glows and a click on it drops the hammer with a dry-fire click.
The trigger does nothing below full cock: a dull tick, and the hammer flashes. Then the revolver shines, the customer holds out a hand, and the player drags the whole revolver
up to it. The customer smiles, takes it, walks out with the door bell, and the next order comes in. At the first
order the bench cannot play yet the shop stays empty. Finds, the phone message and the counter bell are not built
yet (see [backlog](../backlog.md#shop-loop-follow-ups)).

## Rules

- **An order unlocks only the parts it needs.** Every other part of the revolver is fixed for that order. This
  keeps the bench readable and the content small: each order is a short script, not a full simulation.
- **Blocking is the puzzle.** Each part lists the parts that must come off before it can move. Disassembly
  order follows from that; reassembly is the same graph backwards (`PartAssembly`): a blocker goes back in only
  once everything it blocks is back. A part halfway out still blocks. Pressing a blocked part does not move it: a
  dull tick plays and the parts that hold it flash for a moment, so the player sees what comes first.
- **A drag feels like this** (`PartDrag`, tuned with `DragFeel`): a seated part sticks until the pointer pulls it
  about 8 px, and meanwhile only strains a quarter of the pull towards the pointer. Loose, it trails the pointer with
  friction and a speed limit. Within 3 px of an end, moving towards it, it snaps in with a click and leaves the
  player's hand; let go within 3 px of an end, it settles there; let go elsewhere, it stays. Snapping out (free) and in
  (seated) click at different pitches, and coming loose gives a soft tick.
- **A part is in one of five states:** blocked, available (glows), held (being dragged), off (on the mat),
  back (in its slot).
- **Parts out of the gun lie anywhere.** A part pulled to the end of its rail clicks out and stays in the hand; it can
  be carried anywhere on the work surface and put down there. Brought back close to its rail it sticks to it
  (`FreeMove` per part: the cylinder anywhere along its way out of the window, about 10 px off; the base pin only tip
  first, at the mouth of the bore, within about 4 px), with a soft click, and can be pushed in from there; pulled
  off to the side near the end of the rail it comes loose again. A part put down on the revolver lies on top of it.
- **Dirty parts:** each has a dirt layer and a clean threshold (about 90 % of its pixels). A dirty part can be moved
  like any other, but it does not fit: brought to its rail it does not stick, a dull tick plays and the part and the
  wire brush flash. A press on the brush picks it up; while the button stays down its bristles follow the pointer
  and erase dirt wherever they pass over a part out of the gun. The scrub sound rasps louder and a little higher the
  faster the brush moves over the metal, and falls silent when it stops or leaves the part; bare steel sounds softer
  than rust. Let go, the brush lies where it was put down, with a clack. Past the threshold the rest of the dirt
  fades out with a glint and a chime, so the player never hunts for the last pixel, and the part fits again. A dirty
  part still in the gun is dragged as usual. A part brought to its rail while another part keeps it out (the base pin
  while the cylinder is still out) does not stick either, and what holds it flashes.
- **Broken parts:** the drawer at the right of the tool strip pulses while a broken part is off. Dragging the
  new part from the drawer onto the broken one swaps them.
- **One find per order** (two at most), attached to a part. The find is part of the order, not a random drop.
- **One new verb per order.** Orders 1-4 each teach one verb and reuse the earlier ones; order 5 adds nothing
  new and combines everything.

## Orders

| # | Customer (today) | Symptom | Parts that move | New verb | Find (1870s-1900s) | Target time |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Young person in a hoodie | Cylinder jammed (rust) | base pin, cylinder | pull, scrub, test, hand back | Rolled note in a chamber | 1.5 min |
| 2 | Elderly woman in a cardigan | Grip loose and grimy | grip screw, grip panels | unscrew | Photo under a grip panel | 2 min |
| 3 | Man in a work jacket | Hammer will not stay cocked (broken spring) | grip screw, grip panels, mainspring | replace from the drawer | Initials carved inside the frame | 2 min |
| 4 | Teenager with headphones | Action stiff, heavy rust | base pin, cylinder, hammer screw, hammer | oil | One unspent cartridge in a chamber | 2.5 min |
| 5 | Woman in a raincoat | "Make it work again" (everything) | all of the above | none | The find that joins both timelines | 2.5 min |

Ending: about 30 s. All five finds are laid out on the mat, one last phone message, credits, "Play again".
Total: about 11 minutes (see [Session length](ftue.md#session-length)).

The orders are data: `src/game/LudumDare60.Game.Browser/wwwroot/assets/data/orders.json`, one entry per order in
the order customers come in. An order names its customer's look, the symptom icon, the parts it unlocks (by id:
`base-pin`, `cylinder`, `grip-screw`, `grip-panels`, `mainspring`, `hammer-screw`, `hammer`), its dirty parts (rust or
grime, with an optional clean threshold), broken and oiled parts, the find (its id, the part it hides in, and its
handwriting if it has any), the phone message and a target time. Which part blocks which is a fact of the revolver,
not of an order, so it lives in code (`Orders/RevolverParts`); so do the parts' places on the bench until the real
art exists (`Bench/BenchLayout`). Loading checks every rule at once and stops the game naming each problem: unknown
properties or parts, a part whose blocker the order keeps fixed, dirty, broken, oiled or find parts the order does
not unlock. The find texts and messages in the file are drafts.

The order logic (blocking graph, part states, dirt, the brush) lives in `LudumDare60.Game` (`Bench/OrderBench`) and is
tested without a browser.

## What feeds each loop

| Loop | Input from the player | Feedback | Reward |
| --- | --- | --- | --- |
| Touch | Drag along a path | Resistance, part position, sound pitch rising toward the snap | Snap, click, small screen shake, the next part lights up |
| Order | A sequence of touches | Customer leans in, symptom bubble changes, gun shines when done | The find, the customer's face, the phone message |
| Shop | Five orders | Finds box filling up, messages that reference earlier customers | The ending that explains the finds |

## Cuts

In the order the concept already sets ([Risks and cuts](concepts.md#risks-and-cuts)), and what each cut does to
the loop:

| Cut | Effect on the loop |
| --- | --- |
| Oil | Order 4 teaches nothing new; it becomes a harder scrub order |
| Parts drawer | Order 3 becomes "the spring was only dirty"; repair is cleaning only |
| Phone messages | The shop loop loses today's side of the story; finds alone carry it |
| Ending | The game ends after order 5 on the finds box and credits |
| Orders 4-5 | Three orders, about 6 minutes. Still a complete game if order 3 carries the joining find |
