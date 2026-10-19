using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// One order on the bench: the parts it unlocks with their blocks, the dirt on them, which are broken and which need
/// oil, and the tools: the brush, the oil can and the tray of spare parts. It runs the rules of the order loop that do
/// not depend on a browser: nothing moves before the hammer is at half cock, parts move as <see cref="PartAssembly"/>
/// allows and lie anywhere on the work surface once out, the brush scrubs whatever part out of the gun it rubs, the
/// oil can's spout oils whatever part out of the gun it touches, a broken part put down in the tray is swapped for a
/// spare, and a part does not go back onto its rail while it is dirty, dry or broken. The game draws it and plays its
/// sounds. Advance it once per fixed step with <see cref="Step(in PointerSample, float, bool)"/>.
/// </summary>
public sealed class OrderBench
{
    /// <summary>
    /// Radius of the brush's bristles in virtual pixels.
    /// </summary>
    public const float BrushRadius = 2.5f;

    /// <summary>
    /// How close to a part the tip of the oil can's spout must come to oil it, in virtual pixels.
    /// </summary>
    public const float OilRadius = 2;

    // How fast the brush speed that drives the scrub sound follows the real one, per second.
    private const float BrushSpeedSmoothing = 20;

    // While the brush is in the hand, the parts see this pointer instead: nowhere, no button.
    private static readonly PointerSample s_noPointer = new(new Vector2(-10_000), IsDown: false, IsPressed: false);

    private readonly string[] _ids;
    private readonly PartLayout[] _layouts;
    private readonly DirtLayer?[] _dirt;
    private readonly bool[] _broken;
    private readonly bool[] _needsOil;
    private readonly bool[] _wasOut;
    private readonly int _hammer;
    private Vector2 _lastBrush;

    /// <exception cref="NotSupportedException">The bench cannot play the order yet (see <see cref="CanSetUp"/>).</exception>
    public OrderBench(Order order)
        : this(order, CreateBrush(), CreateOilCan())
    {
    }

    /// <summary>
    /// Sets up <paramref name="order"/> with tools that stay on the bench from one order to the next: they lie where
    /// the last order left them, dropped if they were in the hand.
    /// </summary>
    /// <exception cref="NotSupportedException">The bench cannot play the order yet (see <see cref="CanSetUp"/>).</exception>
    public OrderBench(Order order, BrushTool brush, BrushTool oilCan)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(oilCan);
        if (!CanSetUp(order, out var reason))
        {
            throw new NotSupportedException(reason);
        }

        Order = order;
        var count = order.Parts.Count;
        _ids = [.. order.Parts];
        _layouts = new PartLayout[count];
        _dirt = new DirtLayer?[count];
        _broken = new bool[count];
        _needsOil = new bool[count];
        _wasOut = new bool[count];
        var parts = new PartDrag[count];
        for (var i = 0; i < count; i++)
        {
            BenchLayout.TryGetPart(_ids[i], out var layout);
            _layouts[i] = layout!;
            parts[i] = new PartDrag(layout!.Path, layout.GrabRadius, layout.Feel, PartStop.Start, layout.Free, layout.Grab);
        }

        Gun = new PartAssembly(parts);
        for (var i = 0; i < count; i++)
        {
            Gun.SetDepth(i, (int)_layouts[i].Layer);
        }

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

        foreach (var part in order.Broken)
        {
            _broken[IndexOf(part)] = true;
        }

        foreach (var part in order.Oil)
        {
            _needsOil[IndexOf(part)] = true;
        }

        _hammer = IndexOf(RevolverParts.Hammer);
        SparePart = Array.IndexOf(_broken, true);
        brush.Drop();
        oilCan.Drop();
        Brush = brush;
        OilCan = oilCan;
    }

    public Order Order { get; }

    /// <summary>
    /// The scrubbing brush at its place on the bench.
    /// </summary>
    public static BrushTool CreateBrush()
        => new(BenchLayout.BrushRest, BenchLayout.BrushGrabMin, BenchLayout.BrushGrabMax, BenchLayout.MatMin, BenchLayout.MatMax);

    /// <summary>
    /// The oil can at its place on the bench.
    /// </summary>
    public static BrushTool CreateOilCan()
        => new(BenchLayout.OilCanRest, BenchLayout.OilCanGrabMin, BenchLayout.OilCanGrabMax, BenchLayout.MatMin, BenchLayout.MatMax);

    /// <summary>
    /// The order's parts, in the order the order lists them.
    /// </summary>
    public PartAssembly Gun { get; }

    public BrushTool Brush { get; }

    /// <summary>
    /// The oil can, carried like the brush: its position is the tip of the spout.
    /// </summary>
    public BrushTool OilCan { get; }

    /// <summary>
    /// The broken part the tray holds a spare of, or -1 when nothing in this order is broken.
    /// </summary>
    public int SparePart { get; }

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
    /// What happened to the oil can in the last step.
    /// </summary>
    public BrushEvents OilCanEvents { get; private set; }

    /// <summary>
    /// Whether the hammer is off its pivot or in the hand: an order that takes it out, from the moment it moves until
    /// it is back. The hammer cannot be cocked meanwhile.
    /// </summary>
    public bool IsHammerOut => _hammer >= 0 && (Gun[_hammer].Stop != PartStop.Start || Gun[_hammer].IsHeld);

    /// <summary>
    /// What happened on the bench in the last step.
    /// </summary>
    public BenchEvents Events { get; private set; }

    /// <summary>
    /// The part <see cref="Events"/> happened to, or -1.
    /// </summary>
    public int EventPart { get; private set; } = -1;

    /// <summary>
    /// Whether the bench can play <paramref name="order"/>: every part has a layout, every dirt a drawing, and every
    /// broken part a spare in the tray.
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

        foreach (var part in order.Broken)
        {
            if (!BenchLayout.HasSpare(part))
            {
                reason = $"Order '{order.Id}': the tray has no spare '{part}' yet.";
                return false;
            }
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
    /// Whether <paramref name="part"/> lies out of the gun with dirt still on it: it does not go back before it is
    /// clean.
    /// </summary>
    public bool NeedsScrubbing(int part) => _dirt[part] is { IsClean: false } && IsOutOnTheMat(part);

    /// <summary>
    /// Whether <paramref name="part"/> is broken: it does not go back until it is swapped for the spare in the tray.
    /// </summary>
    public bool IsBroken(int part) => _broken[part];

    /// <summary>
    /// Whether <paramref name="part"/> still needs a drop of oil before it goes back.
    /// </summary>
    public bool NeedsOil(int part) => _needsOil[part];

    /// <summary>
    /// Whether <paramref name="part"/> lies out of the gun, still dry: the oil can's spout oils it.
    /// </summary>
    public bool NeedsOiling(int part) => _needsOil[part] && IsOutOnTheMat(part);

    /// <summary>
    /// Whether <paramref name="part"/> lies out of the gun, still broken: put down in the tray, it is swapped for the
    /// spare.
    /// </summary>
    public bool NeedsSpare(int part) => _broken[part] && IsOutOnTheMat(part);

    /// <summary>
    /// Whether <paramref name="part"/> may go back now as far as it is concerned: clean, oiled and whole. Its blocks
    /// may still hold it.
    /// </summary>
    public bool IsReady(int part) => _dirt[part] is not { IsClean: false } && !_needsOil[part] && !_broken[part];

    /// <summary>
    /// Whether the repair is done: every part of the order has been out, is back in its seat, and is clean, oiled and
    /// whole. The test with the hammer is not part of it.
    /// </summary>
    public bool IsDone
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                if (!_wasOut[i] || Gun[i].Stop != PartStop.Start || Gun[i].IsHeld || !IsReady(i))
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
    /// and a press on one reports <see cref="BenchEvents.HammerFirst"/>. A part that is dirty, dry or broken does not
    /// stick to its rail: it reports <see cref="DragEvents.DockRefused"/> in <see cref="PartAssembly.Events"/>.</param>
    public void Step(in PointerSample input, float deltaTime, bool hammerOpen)
    {
        Events = BenchEvents.None;
        EventPart = -1;
        var partHeld = false;
        for (var i = 0; i < Count; i++)
        {
            Gun.SetLocked(i, !hammerOpen);
            Gun.SetDockable(i, IsReady(i));
            partHeld |= Gun[i].IsHeld;
        }

        // The tools first: a press on one takes it, and while a tool is in the hand no part sees the pointer.
        BrushEvents = Brush.Step(input, canGrab: !partHeld && !OilCan.IsHeld);
        var brushTaken = Brush.IsHeld || (BrushEvents & BrushEvents.PickedUp) != BrushEvents.None;
        OilCanEvents = OilCan.Step(input, canGrab: !partHeld && !brushTaken);
        var toolTaken = brushTaken || OilCan.IsHeld || (OilCanEvents & BrushEvents.PickedUp) != BrushEvents.None;
        Gun.Step(toolTaken ? s_noPointer : input, deltaTime);
        SwapPutDownBrokenParts();
        if (!toolTaken && input.IsPressed && !hammerOpen && Gun.PartUnder(input.Position) is var pressed and >= 0)
        {
            Report(BenchEvents.HammerFirst, pressed);
        }

        for (var i = 0; i < Count; i++)
        {
            _wasOut[i] |= Gun[i].Stop == PartStop.End;
        }

        Scrub(deltaTime);
        Oil();
    }

    private bool IsOutOnTheMat(int part) => Gun[part].Stop == PartStop.End && !Gun[part].IsHeld;

    /// <summary>
    /// Swaps a broken part just put down in the tray for the spare: the new part lies where the broken one was put.
    /// </summary>
    private void SwapPutDownBrokenParts()
    {
        var events = Gun.Events;
        for (var i = 0; i < Count; i++)
        {
            if (_broken[i] && Gun[i].IsFree && (events[i] & DragEvents.Dropped) != DragEvents.None && BenchLayout.IsInTray(Gun[i].Position))
            {
                _broken[i] = false;
                Report(BenchEvents.Replaced, i);
            }
        }
    }

    /// <summary>
    /// Oils the dry part out of the gun under the tip of the oil can's spout, while the can is in the hand.
    /// </summary>
    private void Oil()
    {
        if (!OilCan.IsHeld)
        {
            return;
        }

        var part = PartUnder(OilCan.Position, OilRadius);
        if (part >= 0 && _needsOil[part])
        {
            _needsOil[part] = false;
            Report(BenchEvents.Oiled, part);
        }
    }

    /// <summary>
    /// Rubs the brush in the hand over the part under its bristles: dirt comes off along the bristles' path while
    /// there is dirt, and the brush's speed over the part is measured either way.
    /// </summary>
    private void Scrub(float deltaTime)
    {
        var part = Brush.IsHeld ? PartUnder(Brush.Position, BrushRadius) : -1;
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
    /// The first part out of the gun whose sprite, widened by <paramref name="radius"/>, lies under
    /// <paramref name="point"/>; -1 when there is none.
    /// </summary>
    private int PartUnder(Vector2 point, float radius)
    {
        for (var i = 0; i < Count; i++)
        {
            if (!IsOutOnTheMat(i))
            {
                continue;
            }

            var local = LocalPoint(i, point);
            var sprite = _layouts[i].Sprite;
            if (local.X >= -radius && local.Y >= -radius && local.X <= sprite.Width + radius && local.Y <= sprite.Height + radius)
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
