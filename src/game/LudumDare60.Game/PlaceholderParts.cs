using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game;

/// <summary>
/// The revolver generated in code until the real sprites exist, in one sheet: the whole revolver in profile, muzzle to
/// the left (barrel with its front sight, ejector housing, a color-cased frame with the cylinder window, the grip
/// frame with its straps, brass trigger guard), and the parts that move on their own: the base pin (knurled head on
/// the left), the cylinder in two positions of its turn, the hammer and the trigger, the walnut grip panel, a screw
/// (the grip screw and the hammer screw), the mainspring whole and broken, plus the brush, the oil can and the parts
/// tray, and a glow outline for each. Colors come from the palette (docs/gamedesign/art-direction.md).
/// </summary>
internal static class PlaceholderParts
{
    public const int Width = 256;
    public const int Height = 316;

    /// <summary>
    /// The revolver without the parts that move: the cylinder window and the gap for the base pin's head are
    /// transparent.
    /// </summary>
    public static readonly IntRect Body = new(0, 0, BodyWidth, BodyHeight);

    /// <summary>
    /// One pixel around the outside of <see cref="Body"/>: the whole revolver glows when it is ready to hand back.
    /// </summary>
    public static readonly IntRect BodyGlow = Around(Body, 0, BodyHeight);

    public static readonly IntRect Pin = new(0, RowA, 40, 7);

    /// <summary>
    /// One pixel around <see cref="Pin"/>, so it is drawn one pixel up and left of the pin.
    /// </summary>
    public static readonly IntRect PinGlow = Around(Pin, 40, RowA);

    /// <summary>
    /// The scrubbing brush lying flat: wire bristles on the left, a brass ferrule, a walnut handle to the right.
    /// </summary>
    public static readonly IntRect Brush = new(84, RowA, BrushWidth, 7);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect BrushGlow = Around(Brush, 106, RowA);

    public static readonly IntRect Trigger = new(132, RowA, TriggerWidth, TriggerHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect TriggerGlow = Around(Trigger, 136, RowA);

    public static readonly IntRect Cylinder = new(0, RowB, CylinderWidth, CylinderHeight);

    /// <summary>
    /// The cylinder one chamber further round: its flutes sit lower. Cocking the hammer turns the cylinder between the
    /// two.
    /// </summary>
    public static readonly IntRect CylinderTurned = new(CylinderWidth, RowB, CylinderWidth, CylinderHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect CylinderGlow = Around(Cylinder, 52, RowB);

    /// <summary>
    /// The hammer at rest, spur up and back.
    /// </summary>
    public static readonly IntRect Hammer = new(80, RowB, HammerSize, HammerSize);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect HammerGlow = Around(Hammer, 96, RowB);

    /// <summary>
    /// The walnut panel on the side of the grip, which hides the mainspring; its top edge meets the frame.
    /// </summary>
    public static readonly IntRect GripPanel = new(0, RowC, PanelWidth, PanelHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect GripPanelGlow = Around(GripPanel, PanelWidth, RowC);

    /// <summary>
    /// A screw head seen from the side of the gun, with its slot: the grip screw and the hammer screw.
    /// </summary>
    public static readonly IntRect Screw = new(90, RowC, ScrewSize, ScrewSize);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect ScrewGlow = Around(Screw, 97, RowC);

    /// <summary>
    /// The flat mainspring, top end under the hammer, bottom end at the foot of the grip.
    /// </summary>
    public static readonly IntRect Mainspring = new(106, RowC, MainspringWidth, MainspringHeight);

    /// <summary>
    /// The mainspring snapped in the middle, the lower half bent away.
    /// </summary>
    public static readonly IntRect MainspringBroken = new(134, RowC, MainspringWidth, MainspringHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect MainspringGlow = Around(Mainspring, 162, RowC);

    /// <summary>
    /// The oil can lying on the mat, its spout up and to the left.
    /// </summary>
    public static readonly IntRect OilCan = new(192, RowC, OilCanWidth, OilCanHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect OilCanGlow = Around(OilCan, 208, RowC);

    /// <summary>
    /// The wooden tray of spare parts, seen from above.
    /// </summary>
    public static readonly IntRect Tray = new(0, RowD, TrayWidth, TrayHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect TrayGlow = Around(Tray, TrayWidth, RowD);

    /// <summary>
    /// A solid white block, scaled and tinted into bars (the loading screen). 4 by 4, so filtering at its edges never
    /// reaches the transparent pixels around it.
    /// </summary>
    public static readonly IntRect Pixel = new(110, RowD, 4, 4);

    /// <summary>
    /// "PRESS ANY KEY TO PLAY" in white, three pixels by five per letter, for the title screen.
    /// </summary>
    public static readonly IntRect PressAnyKey = new(118, RowD, (PressAnyKeyText.Length * 4) - 1, 5);

    /// <summary>
    /// The middle of the bristles, in brush pixels: the point that scrubs.
    /// </summary>
    public static readonly Vector2 BrushOrigin = new(3, 3);

    /// <summary>
    /// The pin's grab point, the middle of its head, in pin pixels.
    /// </summary>
    public static readonly Vector2 PinOrigin = new(2, 3);

    /// <summary>
    /// The cylinder's grab point, its middle, in cylinder pixels. It lies on the pin's axis.
    /// </summary>
    public static readonly Vector2 CylinderOrigin = new(CylinderWidth / 2, CylinderHeight / 2);

    /// <summary>
    /// The hammer's pivot, in hammer pixels: it turns around it when cocked.
    /// </summary>
    public static readonly Vector2 HammerOrigin = new(4, 12);

    /// <summary>
    /// The tip of the hammer's spur at rest, relative to its pivot: where the player takes it to cock it.
    /// </summary>
    public static readonly Vector2 HammerSpur = new(10, -11);

    /// <summary>
    /// The trigger's pivot, in trigger pixels: its top.
    /// </summary>
    public static readonly Vector2 TriggerOrigin = new(1, 0);

    /// <summary>
    /// The grip panel's grab point, in panel pixels: low on the panel, clear of the grip screw.
    /// </summary>
    public static readonly Vector2 GripPanelOrigin = new(20, 35);

    /// <summary>
    /// The middle of a screw head, in screw pixels: it turns around it.
    /// </summary>
    public static readonly Vector2 ScrewOrigin = new(ScrewSize / 2f, ScrewSize / 2f);

    /// <summary>
    /// The mainspring's grab point, the middle of its length, in mainspring pixels.
    /// </summary>
    public static readonly Vector2 MainspringOrigin = new(13, 16);

    /// <summary>
    /// The tip of the oil can's spout, in oil can pixels: the point that oils.
    /// </summary>
    public static readonly Vector2 OilCanOrigin = new(1, 1);

    /// <summary>
    /// Where the base pin's head sits in <see cref="Body"/> when the pin is in: in front of the frame, on the
    /// cylinder's axis.
    /// </summary>
    public static readonly Vector2 PinInBody = new(95, 30);

    /// <summary>
    /// Where the seated cylinder's middle sits in <see cref="Body"/>. The window leaves one pixel around it.
    /// </summary>
    public static readonly Vector2 CylinderInBody = new(WindowLeft + 1 + (CylinderWidth / 2), WindowTop + 1 + (CylinderHeight / 2));

    /// <summary>
    /// Where the hammer's pivot sits in <see cref="Body"/>: the top of the frame, behind the cylinder.
    /// </summary>
    public static readonly Vector2 HammerInBody = new(133, 19);

    /// <summary>
    /// Where the trigger's pivot sits in <see cref="Body"/>: under the frame, inside the guard.
    /// </summary>
    public static readonly Vector2 TriggerInBody = new(121, 45);

    /// <summary>
    /// Where the seated grip panel's grab point sits in <see cref="Body"/>.
    /// </summary>
    public static readonly Vector2 GripPanelInBody = new Vector2(PanelLeft, PanelTop) + GripPanelOrigin;

    /// <summary>
    /// Where the grip screw's middle sits in <see cref="Body"/>: through the middle of the grip panel.
    /// </summary>
    public static readonly Vector2 GripScrewInBody = new(146.5f, 62.5f);

    /// <summary>
    /// Where the hammer screw's middle sits in <see cref="Body"/>: on the hammer's pivot.
    /// </summary>
    public static readonly Vector2 HammerScrewInBody = HammerInBody + new Vector2(0.5f);

    /// <summary>
    /// Where the seated mainspring's grab point sits in <see cref="Body"/>: in the grip, along the backstrap.
    /// </summary>
    public static readonly Vector2 MainspringInBody = new Vector2(MainspringLeft, MainspringTop) + MainspringOrigin;

    private const int BodyWidth = 184;
    private const int BodyHeight = 88;
    private const int RowA = 180;
    private const int RowB = 194;
    private const int RowC = 226;
    private const int RowD = 276;
    private const int HeadWidth = 4;
    private const int CylinderWidth = 26;
    private const int CylinderHeight = 24;
    private const int BrushWidth = 22;
    private const int HammerSize = 16;
    private const int TriggerWidth = 4;
    private const int TriggerHeight = 10;
    private const int ScrewSize = 7;
    private const int MainspringWidth = 28;
    private const int MainspringHeight = 32;
    private const int OilCanWidth = 16;
    private const int OilCanHeight = 14;
    private const int TrayWidth = 52;
    private const int TrayHeight = 36;

    // The grip panel's sprite in body pixels: the walnut between the straps, below the frame.
    private const int PanelLeft = 130;
    private const int PanelTop = 40;
    private const int PanelWidth = 44;
    private const int PanelHeight = 47;

    // The mainspring's sprite in body pixels, and the slant of the backstrap it lies along.
    private const int MainspringLeft = 142;
    private const int MainspringTop = 51;
    private const float MainspringSlant = 0.71f;

    // The frame around the cylinder window, in body pixels.
    private const int FrameLeft = 97;
    private const int FrameRight = 141;
    private const int FrameTop = 14;
    private const int FrameBottom = 48;
    private const int WindowLeft = 100;
    private const int WindowRight = 127;
    private const int WindowTop = 17;
    private const int WindowBottom = 42;

    private const string PressAnyKeyText = "PRESS ANY KEY TO PLAY";

    // Three by five letters, row by row from the top: the ones PressAnyKeyText needs.
    private static readonly Dictionary<char, string[]> s_letters = new()
    {
        ['A'] = [".#.", "#.#", "###", "#.#", "#.#"],
        ['E'] = ["###", "#..", "##.", "#..", "###"],
        ['K'] = ["#.#", "#.#", "##.", "#.#", "#.#"],
        ['L'] = ["#..", "#..", "#..", "#..", "###"],
        ['N'] = ["##.", "#.#", "#.#", "#.#", "#.#"],
        ['O'] = ["###", "#.#", "#.#", "#.#", "###"],
        ['P'] = ["###", "#.#", "###", "#..", "#.."],
        ['R'] = ["##.", "#.#", "##.", "#.#", "#.#"],
        ['S'] = [".##", "#..", ".#.", "..#", "##."],
        ['T'] = ["###", ".#.", ".#.", ".#.", ".#."],
        ['Y'] = ["#.#", "#.#", ".#.", ".#.", ".#."],
        [' '] = ["...", "...", "...", "...", "..."],
    };

    private static readonly Color s_ink = Color.FromRgb(0x0d0a0a);
    private static readonly Color s_steelDark = Color.FromRgb(0x2c201b);
    private static readonly Color s_steel = Color.FromRgb(0x453a30);
    private static readonly Color s_steelMid = Color.FromRgb(0x5f5247);
    private static readonly Color s_steelLight = Color.FromRgb(0xb3a99b);
    private static readonly Color s_caseBlue = Color.FromRgb(0x2e4a70);
    private static readonly Color s_caseBrown = Color.FromRgb(0x622a14);
    private static readonly Color s_glow = SpriteOutline.Color;
    private static readonly Color s_brass = Color.FromRgb(0xe2ad5f);
    private static readonly Color s_brassDark = Color.FromRgb(0xd2834a);
    private static readonly Color s_walnutLight = Color.FromRgb(0xa1603b);
    private static readonly Color s_walnut = Color.FromRgb(0x823d1e);
    private static readonly Color s_walnutDark = Color.FromRgb(0x622a14);

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

    public static Image Create()
    {
        var pixels = new byte[Width * Height * Image.BytesPerPixel];
        Fill(pixels, Body, BodyColor);
        Fill(pixels, Pin, PinColor);
        Fill(pixels, Cylinder, (x, y) => CylinderColor(x, y, turned: false));
        Fill(pixels, CylinderTurned, (x, y) => CylinderColor(x, y, turned: true));
        Fill(pixels, Brush, BrushColor);
        Fill(pixels, Hammer, HammerColor);
        Fill(pixels, Trigger, TriggerColor);
        Fill(pixels, GripPanel, GripPanelColor);
        Fill(pixels, Screw, ScrewColor);
        Fill(pixels, Mainspring, (x, y) => MainspringColor(x, y, broken: false));
        Fill(pixels, MainspringBroken, (x, y) => MainspringColor(x, y, broken: true));
        Fill(pixels, OilCan, OilCanColor);
        Fill(pixels, Tray, TrayColor);
        Fill(pixels, Pixel, static (_, _) => Color.White);
        Fill(pixels, PressAnyKey, PressAnyKeyColor);
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
    public static Image CreateCylinderRust() => CreateDirt(CylinderWidth, CylinderHeight, IsCylinder, s_rust, RustCutoff);

    /// <summary>
    /// Rust over the hammer, the hammer's size and origin, like <see cref="CreateCylinderRust"/>.
    /// </summary>
    public static Image CreateHammerRust() => CreateDirt(HammerSize, HammerSize, IsHammer, s_rust, RustCutoff);

    /// <summary>
    /// Old oil and dirt over the grip panel, the panel's size and origin, like <see cref="CreateCylinderRust"/>.
    /// </summary>
    public static Image CreateGripPanelGrime() => CreateDirt(PanelWidth, PanelHeight, IsGripPanel, s_grime, GrimeCutoff);

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
    public static GrabShape CreateHammerGrab() => new(HammerSize, HammerSize, HammerOrigin, IsHammer, margin: 1);

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
    /// Blotches of <paramref name="ramp"/> over the pixels of a part, the part's size.
    /// </summary>
    private static Image CreateDirt(int width, int height, Func<int, int, bool> isPart, Color[] ramp, float cutoff)
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
                pixels[i + 3] = color.A;
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

    private static bool InBarrel(int x, int y) => x is >= 4 and <= 95 && y is >= 14 and <= 22;

    private static bool InFrontSight(int x, int y) => x is >= 7 and <= 9 && y is >= 11 and <= 13;

    private static bool InEjector(int x, int y)
        => (x is >= 34 and <= 95 && y is >= 23 and <= 26) || (x is >= 31 and <= 35 && y is >= 22 and <= 27);

    private static bool InFrame(int x, int y)
        => x is >= FrameLeft and <= FrameRight && y is >= FrameTop and <= FrameBottom && !InWindow(x, y);

    private static bool InWindow(int x, int y) => x is >= WindowLeft and <= WindowRight && y is >= WindowTop and <= WindowBottom;

    /// <summary>
    /// The grip's edges at row <paramref name="y"/>: it slants down and back from the frame, and its bottom is
    /// rounded.
    /// </summary>
    private static (float Front, float Back) GripEdges(int y)
    {
        var front = y < 46 ? 130f : 130 + ((y - 46) * 16f / 40);
        var back = 141 + ((y - 40) * 31f / 44);
        var round = Math.Max(0, y - 82);
        return (front + round, back - round);
    }

    private static bool InGrip(int x, int y)
    {
        if (y is < 40 or > 86)
        {
            return false;
        }

        var (front, back) = GripEdges(y);
        return x >= front && x <= back;
    }

    /// <summary>
    /// Whether body pixel (<paramref name="x"/>, <paramref name="y"/>) is under the grip panel: the walnut between the
    /// straps, where the frame does not cover it.
    /// </summary>
    private static bool InPanel(int x, int y)
    {
        if (!InGrip(x, y) || InFrame(x, y))
        {
            return false;
        }

        var (front, back) = GripEdges(y);
        return x >= front + 2 && x <= back - 2;
    }

    private static bool InGuard(int x, int y)
    {
        if (y < 47)
        {
            return false;
        }

        var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(118, 50));
        return distance is >= 8 and <= 11;
    }

    private static bool IsBody(int x, int y)
        => (uint)x < BodyWidth && (uint)y < BodyHeight
            && (InBarrel(x, y) || InFrontSight(x, y) || InEjector(x, y) || InFrame(x, y) || InGrip(x, y) || InGuard(x, y));

    private static Color BodyColor(int x, int y)
    {
        if (InFrame(x, y))
        {
            // A dark rim around the window and screws; the rest color-cased, mottled blue and brown.
            var nearWindow = x is >= WindowLeft - 1 and <= WindowRight + 1 && y is >= WindowTop - 1 and <= WindowBottom + 1;
            if (nearWindow || x == FrameLeft || y == FrameBottom)
            {
                return s_steelDark;
            }

            if ((x is 104 or 105 && y is 45 or 46) || (x is 136 or 137 && y is 24 or 25))
            {
                return s_steelLight;
            }

            if (y == FrameTop)
            {
                return s_steelMid;
            }

            var mottle = Hash(x / 2, y / 2);
            return mottle < 0.15f ? s_caseBlue : mottle < 0.3f ? s_caseBrown : s_steel;
        }

        if (InBarrel(x, y))
        {
            // The muzzle face with its bore on the left; light along the top.
            if (x == 4)
            {
                return y is 17 or 18 ? s_ink : s_steelDark;
            }

            return y switch
            {
                14 => s_steelMid,
                22 => s_steelDark,
                _ => s_steel,
            };
        }

        if (InFrontSight(x, y))
        {
            return s_steelMid;
        }

        if (InEjector(x, y))
        {
            return x <= 35 ? s_steelMid : y == 26 ? s_steelDark : s_steel;
        }

        if (InGrip(x, y))
        {
            // A steel backstrap, a brass front strap, and the dark channel the grip panel covers.
            var (front, back) = GripEdges(y);
            if (x > back - 2)
            {
                return s_steelMid;
            }

            if (x < front + 2)
            {
                return s_brassDark;
            }

            return x < front + 3 || x > back - 3 ? s_ink : s_steelDark;
        }

        if (InGuard(x, y))
        {
            return y > 56 ? s_brassDark : s_brass;
        }

        return Color.Transparent;
    }

    private static bool IsGripPanel(int x, int y)
        => (uint)x < PanelWidth && (uint)y < PanelHeight && InPanel(x + PanelLeft, y + PanelTop);

    private static Color GripPanelColor(int x, int y)
    {
        if (!IsGripPanel(x, y))
        {
            return Color.Transparent;
        }

        // Walnut with grain, darker along the edges.
        if (!IsGripPanel(x - 1, y) || !IsGripPanel(x + 1, y) || !IsGripPanel(x, y - 1) || !IsGripPanel(x, y + 1))
        {
            return s_walnutDark;
        }

        var grain = Hash((x + PanelLeft) / 3, y + PanelTop);
        return grain < 0.25f ? s_walnutDark : grain < 0.8f ? s_walnut : s_walnutLight;
    }

    private static bool IsScrew(int x, int y)
        => (uint)x < ScrewSize && (uint)y < ScrewSize
            && Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), ScrewOrigin) <= 3.3f;

    private static Color ScrewColor(int x, int y)
    {
        if (!IsScrew(x, y))
        {
            return Color.Transparent;
        }

        // A dark rim, the slot across the middle, and light from the top.
        if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), ScrewOrigin) > 2.4f)
        {
            return s_steelDark;
        }

        if (y == ScrewSize / 2)
        {
            return s_ink;
        }

        return y < ScrewSize / 2 ? s_steelLight : s_steelMid;
    }

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

    private static bool IsOilCan(int x, int y)
    {
        if ((uint)x >= OilCanWidth || (uint)y >= OilCanHeight)
        {
            return false;
        }

        // The spout runs from its tip down to the body; the pump's knob sits on top of the body.
        var spout = x <= 7 && y >= (x * 6 / 7) && y <= (x * 6 / 7) + 1;
        var body = InEllipse(x, y, 11, 9.5f, 4.5f, 4.2f);
        var knob = x is >= 10 and <= 12 && y is >= 3 and <= 5;
        return spout || body || knob;
    }

    private static Color OilCanColor(int x, int y)
    {
        if (!IsOilCan(x, y))
        {
            return Color.Transparent;
        }

        if (x <= 7 && !InEllipse(x, y, 11, 9.5f, 4.5f, 4.2f))
        {
            return s_steelMid;
        }

        if (y <= 5)
        {
            return s_steelLight;
        }

        return y switch
        {
            <= 8 => s_brass,
            9 => s_steelDark,
            _ => s_brassDark,
        };
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

    private static bool InEllipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
    {
        var dx = (x + 0.5f - centerX) / radiusX;
        var dy = (y + 0.5f - centerY) / radiusY;
        return (dx * dx) + (dy * dy) <= 1;
    }

    private static bool IsPin(int x, int y)
        => (uint)x < (uint)Pin.Width && (uint)y < (uint)Pin.Height && (x < HeadWidth || Math.Abs(y - (int)PinOrigin.Y) <= 1);

    private static Color PinColor(int x, int y)
    {
        if (!IsPin(x, y))
        {
            return Color.Transparent;
        }

        if (x < HeadWidth)
        {
            // Knurled head: dark grooves every other column, a dark rim around it.
            var rim = x == 0 || y == 0 || y == Pin.Height - 1;
            return rim ? s_steelDark : x % 2 == 0 ? s_steelMid : s_steelLight;
        }

        return y > PinOrigin.Y ? s_steelMid : s_steelLight;
    }

    private static bool IsCylinder(int x, int y)
    {
        if ((uint)x >= CylinderWidth || (uint)y >= CylinderHeight)
        {
            return false;
        }

        // Rounded ends: the corner pixels are cut.
        var cornerX = x is 0 or CylinderWidth - 1;
        var cornerY = y is 0 or CylinderHeight - 1;
        return !(cornerX && cornerY);
    }

    private static Color CylinderColor(int x, int y, bool turned)
    {
        if (!IsCylinder(x, y))
        {
            return Color.Transparent;
        }

        if (x is 0 or CylinderWidth - 1 || y is 0 or CylinderHeight - 1)
        {
            return s_steelDark;
        }

        // Flutes along the length, which move down as the cylinder turns, and light falling from the top.
        var flute = turned ? y is 8 or 15 : y is 5 or 12 or 19;
        if (flute && x is > 2 and < CylinderWidth - 3)
        {
            return s_ink;
        }

        return y switch
        {
            <= 4 => s_steelLight,
            <= 10 => s_steelMid,
            <= 17 => s_steel,
            _ => s_steelDark,
        };
    }

    private static bool IsHammer(int x, int y)
    {
        if ((uint)x >= HammerSize || (uint)y >= HammerSize)
        {
            return false;
        }

        var point = new Vector2(x + 0.5f, y + 0.5f);
        var hub = Vector2.Distance(point, HammerOrigin + new Vector2(0.5f)) <= 3.2f;
        var nose = x <= 2 && y is >= 10 and <= 13;

        // The neck from the hub up to the spur.
        var from = HammerOrigin + new Vector2(0.5f);
        var to = new Vector2(12.5f, 3.5f);
        var along = Math.Clamp(Vector2.Dot(point - from, to - from) / (to - from).LengthSquared(), 0, 1);
        var neck = Vector2.Distance(point, Vector2.Lerp(from, to, along)) <= 1.6f;
        var spur = x is >= 10 and <= 15 && y <= 3;
        return hub || nose || neck || spur;
    }

    private static Color HammerColor(int x, int y)
    {
        if (!IsHammer(x, y))
        {
            return Color.Transparent;
        }

        if (x >= 10 && y <= 3)
        {
            // The knurled spur the thumb pulls.
            return y == 0 ? s_steelLight : x % 2 == 0 ? s_steelDark : s_steelMid;
        }

        return Vector2.Distance(new Vector2(x, y), HammerOrigin) < 1.5f ? s_steelLight : s_steel;
    }

    private static bool IsTrigger(int x, int y)
    {
        if ((uint)x >= TriggerWidth || (uint)y >= TriggerHeight)
        {
            return false;
        }

        // Straight down from the pivot, then curving forward towards the muzzle.
        var left = y < 8 ? 1 : 0;
        return x == left || x == left + 1;
    }

    private static Color TriggerColor(int x, int y) => IsTrigger(x, y) ? (x == 1 ? s_steelLight : s_steelMid) : Color.Transparent;

    private static bool IsBrush(int x, int y)
    {
        if ((uint)x >= (uint)Brush.Width || (uint)y >= (uint)Brush.Height)
        {
            return false;
        }

        return x switch
        {
            // Ragged bristle tips at the left end.
            0 => y is > 0 and < 6,
            <= 5 => true,
            <= 7 => y is > 0 and < 6,
            BrushWidth - 1 => y == 3,
            _ => y is > 1 and < 5,
        };
    }

    private static Color BrushColor(int x, int y)
    {
        if (!IsBrush(x, y))
        {
            return Color.Transparent;
        }

        if (x <= 5)
        {
            // Wire bristles: light and dark strands.
            return y % 2 == 0 ? s_steelLight : s_steelMid;
        }

        if (x <= 7)
        {
            return y <= 2 ? s_brass : s_brassDark;
        }

        return y switch
        {
            2 => s_walnutLight,
            3 => s_walnut,
            _ => s_walnutDark,
        };
    }

    private static Color PressAnyKeyColor(int x, int y)
    {
        // A column of space between letters.
        var column = x % 4;
        return column < 3 && s_letters[PressAnyKeyText[x / 4]][y][column] == '#' ? Color.White : Color.Transparent;
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
