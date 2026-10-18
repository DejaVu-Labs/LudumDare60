using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// One order on the bench: the parts it unlocks with their blocks, the dirt on them, and the brush. It runs the rules
/// of the order loop that do not depend on a browser: parts move as <see cref="PartAssembly"/> allows, a dirty part
/// out on the mat is locked until the brush scrubs it clean, and bare hands do nothing to dirt. The game draws it and
/// plays its sounds. Advance it once per fixed step with <see cref="Step(in PointerSample, float, bool)"/>.
/// </summary>
public sealed class OrderBench
{
    /// <summary>
    /// Radius of the brush's bristles in virtual pixels.
    /// </summary>
    public const float BrushRadius = 2.5f;

    // How fast the brush speed that drives the scrub sound follows the real one, per second.
    private const float BrushSpeedSmoothing = 20;

    // While the brush is in the hand, the parts see this pointer instead: nowhere, no button.
    private static readonly PointerSample s_noPointer = new(new Vector2(-10_000), IsDown: false, IsPressed: false);

    private readonly string[] _ids;
    private readonly PartLayout[] _layouts;
    private readonly DirtLayer?[] _dirt;
    private readonly bool[] _wasOut;
    private Vector2 _lastBrush;

    /// <exception cref="NotSupportedException">The bench cannot play the order yet (see <see cref="CanSetUp"/>).</exception>
    public OrderBench(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!CanSetUp(order, out var reason))
        {
            throw new NotSupportedException(reason);
        }

        Order = order;
        var count = order.Parts.Count;
        _ids = [.. order.Parts];
        _layouts = new PartLayout[count];
        _dirt = new DirtLayer?[count];
        _wasOut = new bool[count];
        var parts = new PartDrag[count];
        for (var i = 0; i < count; i++)
        {
            BenchLayout.TryGetPart(_ids[i], out var layout);
            _layouts[i] = layout!;
            parts[i] = new PartDrag(layout!.Path, layout.GrabRadius, layout.Feel);
        }

        Gun = new PartAssembly(parts);
        foreach (var block in RevolverParts.Blocks)
        {
            var blocker = IndexOf(block.Blocker);
            var blocked = IndexOf(block.Blocked);
            if (blocker >= 0 && blocked >= 0)
            {
                Gun.AddBlock(blocker, blocked);
            }
        }

        foreach (var dirt in order.Dirty)
        {
            _dirt[IndexOf(dirt.Part)] = new DirtLayer(BenchLayout.CreateDirt(dirt.Part, dirt.Kind), dirt.Threshold);
        }

        Brush = new BrushTool(BenchLayout.BrushRest, BenchLayout.BrushGrabMin, BenchLayout.BrushGrabMax);
    }

    public Order Order { get; }

    /// <summary>
    /// The order's parts, in the order the order lists them.
    /// </summary>
    public PartAssembly Gun { get; }

    public BrushTool Brush { get; }

    public int Count => _ids.Length;

    /// <summary>
    /// The part whose dirt the brush's bristles are on, or -1. Only a part lying out on the mat can be scrubbed.
    /// </summary>
    public int ScrubbedPart { get; private set; } = -1;

    /// <summary>
    /// How fast the bristles move over <see cref="ScrubbedPart"/>, smoothed, in virtual pixels per second. It falls to 0
    /// when they stop or leave the part.
    /// </summary>
    public float BrushSpeed { get; private set; }

    /// <summary>
    /// What happened to the brush in the last step.
    /// </summary>
    public BrushEvents BrushEvents { get; private set; }

    /// <summary>
    /// What happened on the bench in the last step.
    /// </summary>
    public BenchEvents Events { get; private set; }

    /// <summary>
    /// The part <see cref="Events"/> happened to, or -1.
    /// </summary>
    public int EventPart { get; private set; } = -1;

    /// <summary>
    /// Whether the bench can play <paramref name="order"/>: every part has a layout and every dirt a drawing, and it
    /// asks for no repair or oil, which the bench cannot do yet.
    /// </summary>
    public static bool CanSetUp(Order order, [NotNullWhen(false)] out string? reason)
    {
        ArgumentNullException.ThrowIfNull(order);
        foreach (var part in order.Parts)
        {
            if (!BenchLayout.TryGetPart(part, out _))
            {
                reason = $"Order '{order.Id}': part '{part}' has no place on the bench yet.";
                return false;
            }
        }

        foreach (var dirt in order.Dirty)
        {
            if (!BenchLayout.HasDirt(dirt.Part, dirt.Kind))
            {
                reason = $"Order '{order.Id}': there is no {dirt.Kind} for '{dirt.Part}' yet.";
                return false;
            }
        }

        if (order.Broken.Count > 0 || order.Oil.Count > 0)
        {
            reason = $"Order '{order.Id}': the parts drawer and oiling do not exist yet.";
            return false;
        }

        reason = null;
        return true;
    }

    public string IdAt(int part) => _ids[part];

    public PartLayout LayoutAt(int part) => _layouts[part];

    /// <summary>
    /// The dirt on <paramref name="part"/>, or null when it is not dirty in this order.
    /// </summary>
    public DirtLayer? DirtAt(int part) => _dirt[part];

    /// <summary>
    /// The index of part <paramref name="id"/>, or -1 when the order does not unlock it.
    /// </summary>
    public int IndexOf(string id) => Array.IndexOf(_ids, id);

    /// <exception cref="KeyNotFoundException">The order does not unlock part <paramref name="id"/>.</exception>
    public PartDrag Part(string id) => Gun[Find(id)];

    /// <inheritdoc cref="DirtAt"/>
    /// <exception cref="KeyNotFoundException">The order does not unlock part <paramref name="id"/>.</exception>
    public DirtLayer? Dirt(string id) => _dirt[Find(id)];

    /// <summary>
    /// Whether <paramref name="part"/> lies out on the mat with dirt still on it: it stays put until it is clean.
    /// </summary>
    public bool NeedsScrubbing(int part) => _dirt[part] is { IsClean: false } && Gun[part].Stop == PartStop.End && !Gun[part].IsHeld;

    /// <summary>
    /// Whether the repair is done: every part of the order has been out, is back in its seat, and every dirty part
    /// is clean. The test with the hammer is not part of it yet.
    /// </summary>
    public bool IsDone
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                if (!_wasOut[i] || Gun[i].Stop != PartStop.Start || Gun[i].IsHeld || _dirt[i] is { IsClean: false })
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Whether <paramref name="part"/> has been out of the gun in this order.
    /// </summary>
    public bool WasOut(int part) => _wasOut[part];

    /// <summary>
    /// Advances the bench by one fixed step, with the hammer at half cock.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    public void Step(in PointerSample input, float deltaTime) => Step(input, deltaTime, hammerOpen: true);

    /// <summary>
    /// Advances the bench by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    /// <param name="hammerOpen">Whether the hammer is at half cock or further. While it is down, every part stays put,
    /// and a press on one reports <see cref="BenchEvents.HammerFirst"/>.</param>
    public void Step(in PointerSample input, float deltaTime, bool hammerOpen)
    {
        Events = BenchEvents.None;
        EventPart = -1;
        var partHeld = false;
        for (var i = 0; i < Count; i++)
        {
            Gun.SetLocked(i, !hammerOpen || NeedsScrubbing(i));
            partHeld |= Gun[i].IsHeld;
        }

        // The brush first: a press on it takes it, and while it is in the hand no part sees the pointer.
        BrushEvents = Brush.Step(input, deltaTime, canGrab: !partHeld);
        var brushTaken = Brush.IsHeld || (BrushEvents & BrushEvents.PickedUp) != BrushEvents.None;
        Gun.Step(brushTaken ? s_noPointer : input, deltaTime);
        if (!brushTaken && input.IsPressed)
        {
            for (var i = 0; i < Count; i++)
            {
                if (!Gun[i].IsUnder(input.Position))
                {
                    continue;
                }

                if (!hammerOpen)
                {
                    Report(BenchEvents.HammerFirst, i);
                    break;
                }

                if (NeedsScrubbing(i))
                {
                    Report(BenchEvents.BareHands, i);
                    break;
                }
            }
        }

        for (var i = 0; i < Count; i++)
        {
            _wasOut[i] |= Gun[i].Stop == PartStop.End;
        }

        Scrub(deltaTime);
    }

    /// <summary>
    /// Rubs the brush in the hand over the part under its bristles: dirt comes off along the bristles' path while
    /// there is dirt, and the brush's speed over the part is measured either way.
    /// </summary>
    private void Scrub(float deltaTime)
    {
        var part = Brush.IsHeld ? PartUnderBristles() : -1;
        var speed = part >= 0 ? Vector2.Distance(Brush.Position, Brush.PreviousPosition) / deltaTime : 0;
        BrushSpeed += (speed - BrushSpeed) * (1 - MathF.Exp(-BrushSpeedSmoothing * deltaTime));
        var local = part >= 0 ? LocalPoint(part, Brush.Position) : default;
        if (part >= 0 && _dirt[part] is { IsClean: false } dirt)
        {
            // A stroke starts where the bristles first touch the part.
            dirt.Scrub(part == ScrubbedPart ? _lastBrush : local, local, BrushRadius);
            if (dirt.IsClean)
            {
                Report(BenchEvents.Cleaned, part);
            }
        }

        ScrubbedPart = part;
        _lastBrush = local;
    }

    /// <summary>
    /// The first part lying out on the mat whose sprite, widened by the bristles' radius, lies under the bristles; -1
    /// when there is none.
    /// </summary>
    private int PartUnderBristles()
    {
        for (var i = 0; i < Count; i++)
        {
            if (Gun[i].Stop != PartStop.End || Gun[i].IsHeld)
            {
                continue;
            }

            var local = LocalPoint(i, Brush.Position);
            var sprite = _layouts[i].Sprite;
            if (local.X >= -BrushRadius && local.Y >= -BrushRadius && local.X <= sprite.Width + BrushRadius && local.Y <= sprite.Height + BrushRadius)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// <paramref name="point"/> in the sprite pixels of <paramref name="part"/>, where it lies now.
    /// </summary>
    private Vector2 LocalPoint(int part, Vector2 point) => point - Gun[part].Position + _layouts[part].Origin;

    private void Report(BenchEvents events, int part)
    {
        Events |= events;
        EventPart = part;
    }

    private int Find(string id)
    {
        var index = IndexOf(id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Order '{Order.Id}' does not unlock part '{id}'.");
    }
}
