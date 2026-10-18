using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// The bench in code until the real art and its layout exist: where the revolver, each part and the brush lie, and the
/// placeholder sprites and dirt for them. Only the parts that have a layout here can be played.
/// </summary>
public static class BenchLayout
{
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
    /// The cylinder's middle when it is out: below the revolver, on the mat, clear of the trigger guard.
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
        UnderFrame: true,
        ClickPitch: 1);

    private static readonly PartLayout s_cylinder = new(
        new LinePath(CylinderSeated, CylinderOut),
        GrabRadius: 11,
        new DragFeel { BreakawayAtStart = 4 },
        PlaceholderParts.Cylinder,
        PlaceholderParts.CylinderGlow,
        PlaceholderParts.CylinderOrigin,
        UnderFrame: false,
        ClickPitch: 0.8f,
        PlaceholderParts.CylinderTurned);

    /// <summary>
    /// The layout of part <paramref name="id"/>, if it has one yet.
    /// </summary>
    public static bool TryGetPart(string id, [NotNullWhen(true)] out PartLayout? layout)
    {
        layout = id switch
        {
            RevolverParts.BasePin => s_basePin,
            RevolverParts.Cylinder => s_cylinder,
            _ => null,
        };
        return layout is not null;
    }

    /// <summary>
    /// Whether <see cref="CreateDirt"/> can draw <paramref name="kind"/> on part <paramref name="id"/>.
    /// </summary>
    public static bool HasDirt(string id, DirtKind kind) => string.Equals(id, RevolverParts.Cylinder, StringComparison.Ordinal) && kind == DirtKind.Rust;

    /// <summary>
    /// The dirt layer of part <paramref name="id"/>: the part's sprite size, drawn with the same origin.
    /// </summary>
    /// <exception cref="NotSupportedException">There is no such dirt yet (see <see cref="HasDirt"/>).</exception>
    public static Image CreateDirt(string id, DirtKind kind)
        => HasDirt(id, kind) ? PlaceholderParts.CreateCylinderRust() : throw new NotSupportedException($"No {kind} for '{id}' yet.");
}
