using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// The bench in code until the real art and its layout exist: where the revolver, each part, the brush, the oil can and
/// the tray of spare parts lie, and the placeholder sprites and dirt for them. Only the parts that have a layout here
/// can be played.
/// </summary>
public static class BenchLayout
{
    /// <summary>
    /// The top-left corner of the work surface: parts out of the gun and the brush stay between it and
    /// <see cref="MatMax"/>.
    /// </summary>
    public static readonly Vector2 MatMin = new(45, 165);

    /// <inheritdoc cref="MatMin"/>
    public static readonly Vector2 MatMax = new(485, 320);

    /// <summary>
    /// Top-left corner of the revolver on the mat, as the customer puts it down.
    /// </summary>
    public static readonly Vector2 GunPosition = new(170, 186);

    /// <summary>
    /// The base pin's head when it is seated: just in front of the frame, on the cylinder's axis.
    /// </summary>
    public static readonly Vector2 PinSeated = GunPosition + PlaceholderParts.PinInBody;

    /// <summary>
    /// The base pin's head when it is pulled out: the whole rod is out of the frame, under the ejector housing.
    /// </summary>
    public static readonly Vector2 PinOut = PinSeated - new Vector2(PlaceholderParts.Pin.Width - 4, 0);

    /// <summary>
    /// The cylinder's middle when it is seated in the frame's window, on the pin's axis.
    /// </summary>
    public static readonly Vector2 CylinderSeated = GunPosition + PlaceholderParts.CylinderInBody;

    /// <summary>
    /// How far the cylinder moves out of its window before it comes free in the hand.
    /// </summary>
    public const float CylinderPull = 10;

    /// <summary>
    /// A good place for the cylinder once it is out: below the revolver, on the mat, clear of the trigger guard. The
    /// cylinder can lie anywhere; this is where the tests and the brush's first place expect it.
    /// </summary>
    public static readonly Vector2 CylinderOut = CylinderSeated + new Vector2(0, 64);

    /// <summary>
    /// The hammer's pivot on the mat.
    /// </summary>
    public static readonly Vector2 HammerPivot = GunPosition + PlaceholderParts.HammerInBody;

    /// <summary>
    /// The trigger's pivot on the mat.
    /// </summary>
    public static readonly Vector2 TriggerPivot = GunPosition + PlaceholderParts.TriggerInBody;

    /// <summary>
    /// The grip panel's grab point when it is on the grip.
    /// </summary>
    public static readonly Vector2 GripPanelSeated = GunPosition + PlaceholderParts.GripPanelInBody;

    /// <summary>
    /// How far the grip panel moves off the grip before it comes free in the hand.
    /// </summary>
    public const float GripPanelPull = 8;

    /// <summary>
    /// A good place for the grip panel once it is off: below the grip, on the mat.
    /// </summary>
    public static readonly Vector2 GripPanelOut = GripPanelSeated + new Vector2(30, 40);

    /// <summary>
    /// The middle of the grip screw, through the grip panel.
    /// </summary>
    public static readonly Vector2 GripScrewSeated = GunPosition + PlaceholderParts.GripScrewInBody;

    /// <summary>
    /// The middle of the hammer screw, on the hammer's pivot.
    /// </summary>
    public static readonly Vector2 HammerScrewSeated = GunPosition + PlaceholderParts.HammerScrewInBody;

    /// <summary>
    /// How many turns a screw takes to come out, and to go back in.
    /// </summary>
    public const float ScrewTurns = 2;

    /// <summary>
    /// The mainspring's grab point when it is in the grip.
    /// </summary>
    public static readonly Vector2 MainspringSeated = GunPosition + PlaceholderParts.MainspringInBody;

    /// <summary>
    /// The way the mainspring slides out of the bottom of the grip, along the backstrap.
    /// </summary>
    public static readonly Vector2 MainspringPullDirection = Vector2.Normalize(new Vector2(0.71f, 1));

    /// <summary>
    /// How far the mainspring slides out before it comes free in the hand.
    /// </summary>
    public const float MainspringPull = 10;

    /// <summary>
    /// How far the hammer lifts off its pivot, up out of the frame, before it comes free in the hand.
    /// </summary>
    public const float HammerPull = 8;

    /// <summary>
    /// A good place for the hammer once it is out: above the revolver's frame, on the mat.
    /// </summary>
    public static readonly Vector2 HammerOut = HammerPivot + new Vector2(60, -30);

    /// <summary>
    /// The top-left corner of the tray of spare parts, on the right of the mat: a broken part put down in it is
    /// swapped for a spare.
    /// </summary>
    public static readonly Vector2 TrayMin = new(425, 222);

    /// <summary>
    /// The bottom-right corner of the tray.
    /// </summary>
    public static readonly Vector2 TrayMax = TrayMin + new Vector2(PlaceholderParts.Tray.Width, PlaceholderParts.Tray.Height);

    /// <summary>
    /// Where the spare part in the tray lies, by its grab point.
    /// </summary>
    public static readonly Vector2 TraySpare = TrayMin + new Vector2(16, 18);

    /// <summary>
    /// Where the tip of the oil can's spout is while it lies at its place, right of the brush.
    /// </summary>
    public static readonly Vector2 OilCanRest = new(440, 290);

    /// <summary>
    /// The oil can's grab area around the tip of its spout: the spout and the can.
    /// </summary>
    public static readonly Vector2 OilCanGrabMin = new(-2, -2);

    /// <inheritdoc cref="OilCanGrabMin"/>
    public static readonly Vector2 OilCanGrabMax = new(15, 12);

    /// <summary>
    /// Where the brush's bristles are while it lies at its place, right of the cylinder's spot on the mat.
    /// </summary>
    public static readonly Vector2 BrushRest = new(390, 296);

    /// <summary>
    /// The brush's grab area around its bristles: the bristles and the handle to the right.
    /// </summary>
    public static readonly Vector2 BrushGrabMin = new(-4, -4);

    /// <inheritdoc cref="BrushGrabMin"/>
    public static readonly Vector2 BrushGrabMax = new(19, 4);

    private static readonly PartLayout s_basePin = new(
        new LinePath(PinSeated, PinOut),
        GrabRadius: 7,
        DragFeel.Default,
        PlaceholderParts.Pin,
        PlaceholderParts.PinGlow,
        PlaceholderParts.PinOrigin,
        PartLayer.UnderFrame,
        ClickPitch: 1,
        Free: new FreeMove(MatMin, MatMax, DockRadius: 4, DockSpan: 4));

    private static readonly PartLayout s_cylinder = new(
        new LinePath(CylinderSeated, CylinderSeated + new Vector2(0, CylinderPull)),
        GrabRadius: 11,
        new DragFeel { BreakawayAtStart = 4 },
        PlaceholderParts.Cylinder,
        PlaceholderParts.CylinderGlow,
        PlaceholderParts.CylinderOrigin,
        PartLayer.OverFrame,
        ClickPitch: 0.8f,
        PlaceholderParts.CylinderTurned,
        new FreeMove(MatMin, MatMax, DockRadius: 8, DockSpan: 0, SeatsOnDock: true));

    // A screw sticks a little before it turns, and once out it goes back into its hole when put down on it.
    private static readonly DragFeel s_screwFeel = new() { BreakawayAtStart = 6, SnapDistance = 4 };

    private static readonly PartLayout s_gripScrew = new(
        PartPath.Screw(GripScrewSeated, ScrewTurns),
        GrabRadius: 5,
        s_screwFeel,
        PlaceholderParts.Screw,
        PlaceholderParts.ScrewGlow,
        PlaceholderParts.ScrewOrigin,
        PartLayer.OverPanel,
        ClickPitch: 1.6f,
        Free: new FreeMove(MatMin, MatMax, DockRadius: 5, DockSpan: 0));

    private static readonly PartLayout s_gripPanel = new(
        new LinePath(GripPanelSeated, GripPanelSeated + new Vector2(0, GripPanelPull)),
        GrabRadius: 9,
        new DragFeel { BreakawayAtStart = 3 },
        PlaceholderParts.GripPanel,
        PlaceholderParts.GripPanelGlow,
        PlaceholderParts.GripPanelOrigin,
        PartLayer.OverGrip,
        ClickPitch: 0.7f,
        Free: new FreeMove(MatMin, MatMax, DockRadius: 8, DockSpan: 0, SeatsOnDock: true));

    private static readonly PartLayout s_mainspring = new(
        new LinePath(MainspringSeated, MainspringSeated + (MainspringPullDirection * MainspringPull)),
        GrabRadius: 6,
        new DragFeel { BreakawayAtStart = 4 },
        PlaceholderParts.Mainspring,
        PlaceholderParts.MainspringGlow,
        PlaceholderParts.MainspringOrigin,
        PartLayer.InGrip,
        ClickPitch: 1.3f,
        Free: new FreeMove(MatMin, MatMax, DockRadius: 7, DockSpan: 0, SeatsOnDock: true),
        BrokenSprite: PlaceholderParts.MainspringBroken);

    private static readonly PartLayout s_hammerScrew = s_gripScrew with
    {
        Path = PartPath.Screw(HammerScrewSeated, ScrewTurns),
        Layer = PartLayer.OverHammer,
        ClickPitch = 1.8f,
    };

    private static readonly PartLayout s_hammer = new(
        new LinePath(HammerPivot, HammerPivot - new Vector2(0, HammerPull)),
        GrabRadius: 6,
        new DragFeel { BreakawayAtStart = 3 },
        PlaceholderParts.Hammer,
        PlaceholderParts.HammerGlow,
        PlaceholderParts.HammerOrigin,
        PartLayer.Hammer,
        ClickPitch: 0.9f,
        Free: new FreeMove(MatMin, MatMax, DockRadius: 6, DockSpan: 0, SeatsOnDock: true));

    /// <summary>
    /// The layout of part <paramref name="id"/>, if it has one yet.
    /// </summary>
    public static bool TryGetPart(string id, [NotNullWhen(true)] out PartLayout? layout)
    {
        layout = id switch
        {
            RevolverParts.BasePin => s_basePin,
            RevolverParts.Cylinder => s_cylinder,
            RevolverParts.GripScrew => s_gripScrew,
            RevolverParts.GripPanels => s_gripPanel,
            RevolverParts.Mainspring => s_mainspring,
            RevolverParts.HammerScrew => s_hammerScrew,
            RevolverParts.Hammer => s_hammer,
            _ => null,
        };
        return layout is not null;
    }

    /// <summary>
    /// Whether the tray holds a spare of part <paramref name="id"/>, so that a broken one can be swapped.
    /// </summary>
    public static bool HasSpare(string id) => TryGetPart(id, out var layout) && layout.BrokenSprite is not null;

    /// <summary>
    /// Whether <paramref name="point"/> lies in the tray of spare parts.
    /// </summary>
    public static bool IsInTray(Vector2 point) => point.X >= TrayMin.X && point.Y >= TrayMin.Y && point.X <= TrayMax.X && point.Y <= TrayMax.Y;

    /// <summary>
    /// Whether <see cref="CreateDirt"/> can draw <paramref name="kind"/> on part <paramref name="id"/>.
    /// </summary>
    public static bool HasDirt(string id, DirtKind kind)
        => (id, kind) is (RevolverParts.Cylinder, DirtKind.Rust) or (RevolverParts.Hammer, DirtKind.Rust) or (RevolverParts.GripPanels, DirtKind.Grime);

    /// <summary>
    /// The dirt layer of part <paramref name="id"/>: the part's sprite size, drawn with the same origin.
    /// </summary>
    /// <exception cref="NotSupportedException">There is no such dirt yet (see <see cref="HasDirt"/>).</exception>
    public static Image CreateDirt(string id, DirtKind kind) => (id, kind) switch
    {
        (RevolverParts.Cylinder, DirtKind.Rust) => PlaceholderParts.CreateCylinderRust(),
        (RevolverParts.Hammer, DirtKind.Rust) => PlaceholderParts.CreateHammerRust(),
        (RevolverParts.GripPanels, DirtKind.Grime) => PlaceholderParts.CreateGripPanelGrime(),
        _ => throw new NotSupportedException($"No {kind} for '{id}' yet."),
    };
}
