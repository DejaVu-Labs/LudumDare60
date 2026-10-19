using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game;

/// <summary>
/// The parts sheet: the revolver from its sprites (<c>art/sprites/revolver.png</c>, embedded in the assembly, see
/// <see cref="EmbeddedSheet.Revolver"/>), a Nagant in profile, muzzle to the left, and the parts that move on their own: the
/// body without them (barrel, frame with the cylinder window, trigger guard, grip frame), the base pin (head on the
/// left), the cylinder in two positions of its turn, the hammer and the trigger, the walnut grip panel and a screw (the
/// grip screw and the hammer screw). The oil can and the brush come from <c>art/sprites/tools.png</c>
/// (<see cref="EmbeddedSheet.Tools"/>). Next to them the pieces still generated in code: the mainspring whole and
/// broken, and the parts tray. Every piece has a glow outline. Colors come from the palette
/// (docs/gamedesign/art-direction.md).
/// </summary>
internal static class PlaceholderParts
{
    public const int Width = 256;
    public const int Height = 395;

    /// <summary>
    /// The revolver without the parts that move: the cylinder window, the slot of the hammer and the gap in front of the
    /// ejector housing where the base pin's head shows are transparent, and the grip frame under the panel is bare
    /// steel.
    /// </summary>
    public static readonly IntRect Body = new(0, 0, BodyWidth, BodyHeight);

    /// <summary>
    /// One pixel around the outside of <see cref="Body"/>: the whole revolver glows when it is ready to hand back.
    /// </summary>
    public static readonly IntRect BodyGlow = Around(Body, 0, BodyHeight);

    public static readonly IntRect Pin = new(0, RowA, PinWidth, PinHeight);

    /// <summary>
    /// One pixel around <see cref="Pin"/>, so it is drawn one pixel up and left of the pin.
    /// </summary>
    public static readonly IntRect PinGlow = Around(Pin, PinWidth, RowA);

    /// <summary>
    /// The scrubbing brush lying at a slant: the bristles down on the left, the walnut handle up to the right.
    /// </summary>
    public static readonly IntRect Brush = new(0, RowE, BrushWidth, BrushHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect BrushGlow = Around(Brush, BrushWidth, RowE);

    /// <summary>
    /// The trigger, what of it shows below the frame: its top goes on under the frame.
    /// </summary>
    public static readonly IntRect Trigger = new(132, RowA, TriggerWidth, TriggerHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect TriggerGlow = Around(Trigger, 154, RowA);

    public static readonly IntRect Cylinder = new(0, RowB, CylinderWidth, CylinderHeight);

    /// <summary>
    /// The cylinder one chamber further round: its flutes sit lower. Cocking the hammer turns the cylinder between the
    /// two.
    /// </summary>
    public static readonly IntRect CylinderTurned = new(CylinderWidth, RowB, CylinderWidth, CylinderHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect CylinderGlow = Around(Cylinder, 2 * CylinderWidth, RowB);

    /// <summary>
    /// The hammer at rest, spur up and back.
    /// </summary>
    public static readonly IntRect Hammer = new(95, RowB, HammerWidth, HammerHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect HammerGlow = Around(Hammer, 116, RowB);

    /// <summary>
    /// The walnut panel on the side of the grip, which hides the mainspring; its top edge meets the frame.
    /// </summary>
    public static readonly IntRect GripPanel = new(0, RowC, PanelWidth, PanelHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect GripPanelGlow = Around(GripPanel, PanelWidth, RowC);

    /// <summary>
    /// A screw head seen from the side of the gun, with its cross slot: the grip screw and the hammer screw.
    /// </summary>
    public static readonly IntRect Screw = new(139, RowB, ScrewSize, ScrewSize);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect ScrewGlow = Around(Screw, 151, RowB);

    /// <summary>
    /// The flat mainspring, top end under the hammer, bottom end at the foot of the grip.
    /// </summary>
    public static readonly IntRect Mainspring = new(78, RowC, MainspringWidth, MainspringHeight);

    /// <summary>
    /// The mainspring snapped in the middle, the lower half bent away.
    /// </summary>
    public static readonly IntRect MainspringBroken = new(96, RowC, MainspringWidth, MainspringHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect MainspringGlow = Around(Mainspring, 114, RowC);

    /// <summary>
    /// The oil can lying on the mat, its spout up and to the left.
    /// </summary>
    public static readonly IntRect OilCan = new(88, RowE, OilCanWidth, OilCanHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect OilCanGlow = Around(OilCan, 88 + OilCanWidth, RowE);

    /// <summary>
    /// The wooden tray of spare parts, seen from above.
    /// </summary>
    public static readonly IntRect Tray = new(134, RowC, TrayWidth, TrayHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect TrayGlow = Around(Tray, 186, RowC);

    /// <summary>
    /// A solid white block, scaled and tinted into bars (the loading screen). 4 by 4, so filtering at its edges never
    /// reaches the transparent pixels around it.
    /// </summary>
    public static readonly IntRect Pixel = new(242, RowC, 4, 4);

    /// <summary>
    /// "PRESS ANY KEY TO PLAY" in white, three pixels by five per letter, for the title screen.
    /// </summary>
    public static readonly IntRect PressAnyKey = new(0, RowD, (PressAnyKeyText.Length * 4) - 1, 5);

    /// <summary>
    /// "TO BE CONTINUED" in white, in the same letters as <see cref="PressAnyKey"/>, for the ending.
    /// </summary>
    public static readonly IntRect ToBeContinued = new(88, RowD, (ToBeContinuedText.Length * 4) - 1, 5);

    /// <summary>
    /// The middle of the bristles, in brush pixels: the point that scrubs.
    /// </summary>
    public static readonly Vector2 BrushOrigin = new(10, 30);

    /// <summary>
    /// The pin's grab point, the middle of its head, in pin pixels.
    /// </summary>
    public static readonly Vector2 PinOrigin = new(4, 4);

    /// <summary>
    /// The cylinder's grab point, its middle, in cylinder pixels.
    /// </summary>
    public static readonly Vector2 CylinderOrigin = new(CylinderWidth / 2, CylinderHeight / 2);

    /// <summary>
    /// The hammer's pivot, in hammer pixels: low in its foot, which goes into the frame. It turns around it when cocked.
    /// </summary>
    public static readonly Vector2 HammerOrigin = new(5, 20);

    /// <summary>
    /// The tip of the hammer's spur at rest, relative to its pivot: where the player takes it to cock it.
    /// </summary>
    public static readonly Vector2 HammerSpur = new(15, -13);

    /// <summary>
    /// The trigger's pivot, in trigger pixels: its top, where it goes under the frame.
    /// </summary>
    public static readonly Vector2 TriggerOrigin = new(5, 3);

    /// <summary>
    /// The grip panel's grab point, in panel pixels: low on the panel, clear of the grip screw.
    /// </summary>
    public static readonly Vector2 GripPanelOrigin = new(20, 50);

    /// <summary>
    /// The middle of a screw head, in screw pixels: it turns around it.
    /// </summary>
    public static readonly Vector2 ScrewOrigin = new(ScrewSize / 2f, ScrewSize / 2f);

    /// <summary>
    /// The mainspring's grab point, the middle of its length, in mainspring pixels.
    /// </summary>
    public static readonly Vector2 MainspringOrigin = new(9, 20);

    /// <summary>
    /// The tip of the oil can's spout, in oil can pixels: the point that oils.
    /// </summary>
    public static readonly Vector2 OilCanOrigin = new(1, 1);

    /// <summary>
    /// Where the base pin's head sits in <see cref="Body"/> when the pin is in: in front of the ejector housing, under
    /// the barrel.
    /// </summary>
    public static readonly Vector2 PinInBody = new Vector2(41, 18) + PinOrigin;

    /// <summary>
    /// Where the seated cylinder's middle sits in <see cref="Body"/>, in the frame's window.
    /// </summary>
    public static readonly Vector2 CylinderInBody = new Vector2(90, 7) + CylinderOrigin;

    /// <summary>
    /// Where the hammer's pivot sits in <see cref="Body"/>: the back of the frame, behind the cylinder.
    /// </summary>
    public static readonly Vector2 HammerInBody = new Vector2(133, 2) + HammerOrigin;

    /// <summary>
    /// Where the trigger's pivot sits in <see cref="Body"/>: under the frame, inside the guard.
    /// </summary>
    public static readonly Vector2 TriggerInBody = new Vector2(105, 45) + TriggerOrigin;

    /// <summary>
    /// Where the seated grip panel's grab point sits in <see cref="Body"/>.
    /// </summary>
    public static readonly Vector2 GripPanelInBody = new Vector2(PanelLeft, PanelTop) + GripPanelOrigin;

    /// <summary>
    /// Where the grip screw's middle sits in <see cref="Body"/>: through the middle of the grip panel.
    /// </summary>
    public static readonly Vector2 GripScrewInBody = new(166, 73);

    /// <summary>
    /// Where the hammer screw's middle sits in <see cref="Body"/>: on the hammer's pivot.
    /// </summary>
    public static readonly Vector2 HammerScrewInBody = HammerInBody;

    /// <summary>
    /// Where the seated mainspring's grab point sits in <see cref="Body"/>: in the grip, along the backstrap.
    /// </summary>
    public static readonly Vector2 MainspringInBody = new Vector2(MainspringLeft, MainspringTop) + MainspringOrigin;

    /// <summary>
    /// The slant of the backstrap the mainspring lies along: how far right it goes per pixel down.
    /// </summary>
    public const float MainspringSlant = 0.35f;

    private const int BodyWidth = 185;
    private const int BodyHeight = 108;
    private const int RowA = 220;
    private const int RowB = 246;
    private const int RowC = 281;
    private const int RowD = 348;
    private const int RowE = 354;
    private const int PinWidth = 41;
    private const int PinHeight = 8;
    private const int CylinderWidth = 31;
    private const int CylinderHeight = 33;
    private const int BrushWidth = 43;
    private const int BrushHeight = 38;
    private const int HammerWidth = 21;
    private const int HammerHeight = 30;
    private const int TriggerWidth = 22;
    private const int TriggerHeight = 24;
    private const int ScrewSize = 12;
    private const int MainspringWidth = 18;
    private const int MainspringHeight = 40;
    private const int OilCanWidth = 49;
    private const int OilCanHeight = 39;
    private const int TrayWidth = 52;
    private const int TrayHeight = 36;

    // The grip panel's sprite in body pixels: the walnut inside the grip frame, below the frame.
    private const int PanelLeft = 147;
    private const int PanelTop = 43;
    private const int PanelWidth = 38;
    private const int PanelHeight = 65;

    // The mainspring's sprite in body pixels, under the panel near the backstrap.
    private const int MainspringLeft = 163;
    private const int MainspringTop = 49;

    // Where each piece lies in the revolver sheet.
    private static readonly IntRect s_bodySource = new(0, 0, BodyWidth, BodyHeight);
    private static readonly IntRect s_cylinderSource = new(0, 110, CylinderWidth, CylinderHeight);
    private static readonly IntRect s_cylinderTurnedSource = new(32, 110, CylinderWidth, CylinderHeight);
    private static readonly IntRect s_hammerSource = new(64, 110, HammerWidth, HammerHeight);
    private static readonly IntRect s_triggerSource = new(86, 110, TriggerWidth, TriggerHeight);
    private static readonly IntRect s_gripPanelSource = new(109, 110, PanelWidth, PanelHeight);
    private static readonly IntRect s_pinSource = new(148, 110, PinWidth, PinHeight);
    private static readonly IntRect s_screwSource = new(148, 119, ScrewSize, ScrewSize);

    // Where each tool lies in the tools sheet.
    private static readonly IntRect s_oilCanSource = new(0, 0, OilCanWidth, OilCanHeight);
    private static readonly IntRect s_brushSource = new(51, 0, BrushWidth, BrushHeight);

    private const string PressAnyKeyText = "PRESS ANY KEY TO PLAY";
    private const string ToBeContinuedText = "TO BE CONTINUED";

    // Three by five letters, row by row from the top: the ones PressAnyKeyText and ToBeContinuedText need.
    private static readonly Dictionary<char, string[]> s_letters = new()
    {
        ['A'] = [".#.", "#.#", "###", "#.#", "#.#"],
        ['B'] = ["##.", "#.#", "##.", "#.#", "##."],
        ['C'] = [".##", "#..", "#..", "#..", ".##"],
        ['D'] = ["##.", "#.#", "#.#", "#.#", "##."],
        ['E'] = ["###", "#..", "##.", "#..", "###"],
        ['I'] = ["###", ".#.", ".#.", ".#.", "###"],
        ['K'] = ["#.#", "#.#", "##.", "#.#", "#.#"],
        ['L'] = ["#..", "#..", "#..", "#..", "###"],
        ['N'] = ["##.", "#.#", "#.#", "#.#", "#.#"],
        ['O'] = ["###", "#.#", "#.#", "#.#", "###"],
        ['P'] = ["###", "#.#", "###", "#..", "#.."],
        ['R'] = ["##.", "#.#", "##.", "#.#", "#.#"],
        ['S'] = [".##", "#..", ".#.", "..#", "##."],
        ['T'] = ["###", ".#.", ".#.", ".#.", ".#."],
        ['U'] = ["#.#", "#.#", "#.#", "#.#", "###"],
        ['Y'] = ["#.#", "#.#", ".#.", ".#.", ".#."],
        [' '] = ["...", "...", "...", "...", "..."],
    };

    private static readonly Color s_ink = Color.FromRgb(0x0d0a0a);
    private static readonly Color s_steelDark = Color.FromRgb(0x2c201b);
    private static readonly Color s_steelMid = Color.FromRgb(0x5f5247);
    private static readonly Color s_steelLight = Color.FromRgb(0xb3a99b);
    private static readonly Color s_glow = SpriteOutline.Color;
    private static readonly Color s_walnutLight = Color.FromRgb(0xa1603b);
    private static readonly Color s_walnut = Color.FromRgb(0x823d1e);

    // Old oil and dirt, dark to light.
    private static readonly Color[] s_grime =
    [
        Color.FromRgb(0x0d0a0a), Color.FromRgb(0x1e0d09), Color.FromRgb(0x2b120b), Color.FromRgb(0x2c201b), Color.FromRgb(0x453a30),
    ];

    // Rust, dark to light, from the walnut, rust and copper ramp.
    private static readonly Color[] s_rust =
    [
        Color.FromRgb(0x622a14), Color.FromRgb(0x823d1e), Color.FromRgb(0x944f2d), Color.FromRgb(0xa1603b), Color.FromRgb(0xb37149),
    ];

    /// <summary>
    /// How much of a part rust covers, from 0 (all of it) to 1 (none): pixels whose blotch value lies above it.
    /// </summary>
    private const float RustCutoff = 0.3f;

    /// <summary>
    /// Like <see cref="RustCutoff"/>, for grime on the grip panel.
    /// </summary>
    private const float GrimeCutoff = 0.35f;

    /// <summary>
    /// How opaque rust is, out of 255: 80%, so the part shows a little through it.
    /// </summary>
    private const byte RustAlpha = 204;

    public static Image Create()
    {
        var pixels = new byte[Width * Height * Image.BytesPerPixel];
        Fill(pixels, Body, static (x, y) => SheetColor(s_bodySource, x, y));
        Fill(pixels, Pin, static (x, y) => SheetColor(s_pinSource, x, y));
        Fill(pixels, Cylinder, static (x, y) => SheetColor(s_cylinderSource, x, y));
        Fill(pixels, CylinderTurned, static (x, y) => SheetColor(s_cylinderTurnedSource, x, y));
        Fill(pixels, Brush, static (x, y) => SheetColor(EmbeddedSheet.Tools, s_brushSource, x, y));
        Fill(pixels, Hammer, static (x, y) => SheetColor(s_hammerSource, x, y));
        Fill(pixels, Trigger, static (x, y) => SheetColor(s_triggerSource, x, y));
        Fill(pixels, GripPanel, static (x, y) => SheetColor(s_gripPanelSource, x, y));
        Fill(pixels, Screw, static (x, y) => SheetColor(s_screwSource, x, y));
        Fill(pixels, Mainspring, (x, y) => MainspringColor(x, y, broken: false));
        Fill(pixels, MainspringBroken, (x, y) => MainspringColor(x, y, broken: true));
        Fill(pixels, OilCan, static (x, y) => SheetColor(EmbeddedSheet.Tools, s_oilCanSource, x, y));
        Fill(pixels, Tray, TrayColor);
        Fill(pixels, Pixel, static (_, _) => Color.White);
        Fill(pixels, PressAnyKey, static (x, y) => TextColor(PressAnyKeyText, x, y));
        Fill(pixels, ToBeContinued, static (x, y) => TextColor(ToBeContinuedText, x, y));
        FillGlow(pixels, BodyGlow, IsBody);
        FillGlow(pixels, PinGlow, IsPin);
        FillGlow(pixels, CylinderGlow, IsCylinder);
        FillGlow(pixels, BrushGlow, IsBrush);
        FillGlow(pixels, HammerGlow, IsHammer);
        FillGlow(pixels, TriggerGlow, IsTrigger);
        FillGlow(pixels, GripPanelGlow, IsGripPanel);
        FillGlow(pixels, ScrewGlow, IsScrew);
        FillGlow(pixels, MainspringGlow, (x, y) => IsMainspring(x, y, broken: false));
        FillGlow(pixels, OilCanGlow, IsOilCan);
        FillGlow(pixels, TrayGlow, IsTray);
        return new Image(Width, Height, pixels);
    }

    /// <summary>
    /// Rust over the cylinder, for a <see cref="Cleaning.DirtLayer"/>: blotches over most of it, the cylinder's size and
    /// origin. The same pixels every time.
    /// </summary>
    public static Image CreateCylinderRust() => CreateDirt(CylinderWidth, CylinderHeight, IsCylinder, s_rust, RustCutoff, RustAlpha);

    /// <summary>
    /// Rust over the hammer, the hammer's size and origin, like <see cref="CreateCylinderRust"/>.
    /// </summary>
    public static Image CreateHammerRust() => CreateDirt(HammerWidth, HammerHeight, IsHammer, s_rust, RustCutoff, RustAlpha);

    /// <summary>
    /// Old oil and dirt over the grip panel, the panel's size and origin, like <see cref="CreateCylinderRust"/>.
    /// </summary>
    public static Image CreateGripPanelGrime() => CreateDirt(PanelWidth, PanelHeight, IsGripPanel, s_grime, GrimeCutoff, byte.MaxValue);

    /// <summary>
    /// The revolver's opaque pixels with the revolver at <paramref name="at"/>, asked with work surface points: what
    /// hides a part under the frame.
    /// </summary>
    public static GrabShape CreateBodyCover(Vector2 at) => new(BodyWidth, BodyHeight, -at, IsBody, margin: 0);

    /// <summary>
    /// The base pin's pixels, head and rod, and two around them: the rod is thin.
    /// </summary>
    public static GrabShape CreatePinGrab() => new(Pin.Width, Pin.Height, PinOrigin, IsPin, margin: 2);

    /// <summary>
    /// The cylinder's pixels and one around them. It keeps its outline as it turns.
    /// </summary>
    public static GrabShape CreateCylinderGrab() => new(CylinderWidth, CylinderHeight, CylinderOrigin, IsCylinder, margin: 1);

    /// <summary>
    /// The hammer's pixels at rest, hub, neck and spur, and one around them.
    /// </summary>
    public static GrabShape CreateHammerGrab() => new(HammerWidth, HammerHeight, HammerOrigin, IsHammer, margin: 1);

    /// <summary>
    /// The hammer's pixels above the hammer screw and the screw's own grab margin, and one around them: where the thumb
    /// takes the hammer in the gun to cock it. The screw on the pivot stays the screw's.
    /// </summary>
    public static GrabShape CreateCockGrab()
        => new(HammerWidth, HammerHeight, HammerOrigin, static (x, y) => y < HammerOrigin.Y - (ScrewSize / 2) - 3 && IsHammer(x, y), margin: 1);

    /// <summary>
    /// The trigger's pixels below the frame, and one around them: where the player pulls it.
    /// </summary>
    public static GrabShape CreateTriggerGrab() => new(TriggerWidth, TriggerHeight, TriggerOrigin, IsTrigger, margin: 1);

    /// <summary>
    /// A screw head's pixels and two around them: the head is small.
    /// </summary>
    public static GrabShape CreateScrewGrab() => new(ScrewSize, ScrewSize, ScrewOrigin, IsScrew, margin: 2);

    /// <summary>
    /// The grip panel's pixels and one around them: the whole panel takes it, not only its grab point.
    /// </summary>
    public static GrabShape CreateGripPanelGrab() => new(PanelWidth, PanelHeight, GripPanelOrigin, IsGripPanel, margin: 1);

    /// <summary>
    /// The mainspring's pixels, whole or broken, and two around them: the spring is thin, and takes it along its
    /// whole length.
    /// </summary>
    public static GrabShape CreateMainspringGrab()
        => new(MainspringWidth, MainspringHeight, MainspringOrigin, (x, y) => IsMainspring(x, y, broken: false) || IsMainspring(x, y, broken: true), margin: 2);

    /// <summary>
    /// Blotches of <paramref name="ramp"/> over the pixels of a part, the part's size, <paramref name="alpha"/> opaque.
    /// </summary>
    private static Image CreateDirt(int width, int height, Func<int, int, bool> isPart, Color[] ramp, float cutoff, byte alpha)
    {
        var pixels = new byte[width * height * Image.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Coarse blotches of 3x3 pixels, broken up per pixel.
                var value = (Hash(x / 3, y / 3) * 0.6f) + (Hash(x + 100, y + 100) * 0.4f);
                if (!isPart(x, y) || value <= cutoff)
                {
                    continue;
                }

                var shade = Math.Min(ramp.Length - 1, (int)((value - cutoff) / (1 - cutoff) * ramp.Length));
                var i = ((y * width) + x) * Image.BytesPerPixel;
                var color = ramp[shade];
                pixels[i] = color.R;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.B;
                pixels[i + 3] = alpha;
            }
        }

        return new Image(width, height, pixels);
    }

    /// <summary>
    /// A deterministic value in [0, 1) per integer point.
    /// </summary>
    private static float Hash(int x, int y)
    {
        var h = unchecked(((uint)x * 374761393u) + ((uint)y * 668265263u));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        h ^= h >> 16;
        return (h >> 8) / (float)(1 << 24);
    }

    private static IntRect Around(IntRect part, int x, int y) => new(x, y, part.Width + 2, part.Height + 2);

    /// <summary>
    /// Whether part pixel (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="part"/>, a place in the
    /// revolver sheet, is opaque.
    /// </summary>
    private static bool IsOpaque(IntRect part, int x, int y) => IsOpaque(EmbeddedSheet.Revolver, part, x, y);

    private static bool IsOpaque(EmbeddedSheet sheet, IntRect part, int x, int y)
        => (uint)x < (uint)part.Width && (uint)y < (uint)part.Height && sheet.Get(part.X + x, part.Y + y).A != 0;

    private static Color SheetColor(IntRect part, int x, int y) => SheetColor(EmbeddedSheet.Revolver, part, x, y);

    private static Color SheetColor(EmbeddedSheet sheet, IntRect part, int x, int y)
        => (uint)x < (uint)part.Width && (uint)y < (uint)part.Height ? sheet.Get(part.X + x, part.Y + y) : Color.Transparent;

    private static bool IsBody(int x, int y) => IsOpaque(s_bodySource, x, y);

    private static bool IsGripPanel(int x, int y) => IsOpaque(s_gripPanelSource, x, y);

    private static bool IsScrew(int x, int y) => IsOpaque(s_screwSource, x, y);

    /// <summary>
    /// The middle of the mainspring at row <paramref name="y"/>, in mainspring pixels: it slants like the backstrap.
    /// A broken spring has a gap at its middle and its lower half bent away.
    /// </summary>
    private static bool IsMainspring(int x, int y, bool broken)
    {
        if ((uint)x >= MainspringWidth || (uint)y >= MainspringHeight)
        {
            return false;
        }

        if (broken && y is 14 or 15)
        {
            return false;
        }

        var middle = 2 + (y * MainspringSlant) + (broken && y >= 16 ? 1.5f : 0);
        return MathF.Abs(x - middle) <= 1;
    }

    private static Color MainspringColor(int x, int y, bool broken)
    {
        if (!IsMainspring(x, y, broken))
        {
            return Color.Transparent;
        }

        var middle = 2 + (y * MainspringSlant) + (broken && y >= 16 ? 1.5f : 0);
        if (broken && y is 13 or 16)
        {
            // The torn ends.
            return s_rust[2];
        }

        return x < middle ? s_steelLight : s_steelMid;
    }

    private static bool IsTray(int x, int y)
    {
        if ((uint)x >= TrayWidth || (uint)y >= TrayHeight)
        {
            return false;
        }

        // Rounded corners: the corner pixels are cut.
        var cornerX = x is 0 or TrayWidth - 1;
        var cornerY = y is 0 or TrayHeight - 1;
        return !(cornerX && cornerY);
    }

    private static Color TrayColor(int x, int y)
    {
        if (!IsTray(x, y))
        {
            return Color.Transparent;
        }

        // A walnut rim two pixels wide, lit from the top, around a dark felt bottom.
        var rim = x <= 1 || y <= 1 || x >= TrayWidth - 2 || y >= TrayHeight - 2;
        if (rim)
        {
            return y <= 1 ? s_walnutLight : s_walnut;
        }

        return y == 2 ? s_ink : s_steelDark;
    }

    private static bool IsPin(int x, int y) => IsOpaque(s_pinSource, x, y);

    private static bool IsCylinder(int x, int y) => IsOpaque(s_cylinderSource, x, y);

    private static bool IsHammer(int x, int y) => IsOpaque(s_hammerSource, x, y);

    private static bool IsTrigger(int x, int y) => IsOpaque(s_triggerSource, x, y);

    private static bool IsBrush(int x, int y) => IsOpaque(EmbeddedSheet.Tools, s_brushSource, x, y);

    private static bool IsOilCan(int x, int y) => IsOpaque(EmbeddedSheet.Tools, s_oilCanSource, x, y);

    private static Color TextColor(string text, int x, int y)
    {
        // A column of space between letters.
        var column = x % 4;
        return column < 3 && s_letters[text[x / 4]][y][column] == '#' ? Color.White : Color.Transparent;
    }

    private static void Fill(byte[] pixels, IntRect rect, Func<int, int, Color> color)
    {
        for (var y = 0; y < rect.Height; y++)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                Set(pixels, rect.X + x, rect.Y + y, color(x, y));
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="glow"/> with a one pixel outline around a part's pixels, given in part coordinates.
    /// </summary>
    private static void FillGlow(byte[] pixels, IntRect glow, Func<int, int, bool> isPart)
    {
        for (var y = 0; y < glow.Height; y++)
        {
            for (var x = 0; x < glow.Width; x++)
            {
                if (IsOutline(x - 1, y - 1, isPart))
                {
                    Set(pixels, glow.X + x, glow.Y + y, s_glow);
                }
            }
        }
    }

    private static bool IsOutline(int x, int y, Func<int, int, bool> isPart)
    {
        if (isPart(x, y))
        {
            return false;
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (isPart(x + dx, y + dy))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void Set(byte[] pixels, int x, int y, Color color)
    {
        var i = ((y * Width) + x) * Image.BytesPerPixel;
        pixels[i] = color.R;
        pixels[i + 1] = color.G;
        pixels[i + 2] = color.B;
        pixels[i + 3] = color.A;
    }
}
