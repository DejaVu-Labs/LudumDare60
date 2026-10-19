using System.Numerics;
using System.Runtime.InteropServices;

namespace LudumDare60.Game.Parts;

/// <summary>
/// The parts of one gun that an order lets move, and which part blocks which: the disassembly puzzle of
/// docs/gamedesign/core-loop.md. A part that blocks another must be out (<see cref="PartStop.End"/>) before the other
/// can move, and the other must be back in (<see cref="PartStop.Start"/>) before the blocker can go back. Reassembly is
/// the same graph backwards. Parts are addressed by their index in the constructor's list.
/// </summary>
public sealed class PartAssembly
{
    private readonly PartDrag[] _parts;
    private readonly DragEvents[] _events;
    private readonly bool[] _locked;
    private readonly bool[] _undockable;
    private readonly int[] _depths;

    // The parts top first, as the last step picked them for the pointer.
    private readonly int[] _order;
    private readonly List<Block> _blocks = [];

    /// <param name="parts">The parts. Where grab areas overlap, the part on top wins (see <see cref="SetDepth"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is empty or holds a part twice.</exception>
    public PartAssembly(params ReadOnlySpan<PartDrag> parts)
    {
        if (parts.IsEmpty)
        {
            throw new ArgumentException("An assembly needs at least one part.", nameof(parts));
        }

        _parts = parts.ToArray();
        for (var i = 0; i < _parts.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(_parts[i], nameof(parts));
            if (Array.IndexOf(_parts, _parts[i]) != i)
            {
                throw new ArgumentException("A part is listed twice.", nameof(parts));
            }
        }

        _events = new DragEvents[_parts.Length];
        _locked = new bool[_parts.Length];
        _undockable = new bool[_parts.Length];
        _depths = new int[_parts.Length];
        _order = new int[_parts.Length];
    }

    public int Count => _parts.Length;

    public PartDrag this[int index] => _parts[index];

    /// <summary>
    /// What happened to each part in the last <see cref="Step"/>, by index.
    /// </summary>
    public ReadOnlySpan<DragEvents> Events => _events;

    /// <summary>
    /// Makes <paramref name="blocker"/> block <paramref name="blocked"/>: <paramref name="blocked"/> moves only while
    /// <paramref name="blocker"/> is out, and <paramref name="blocker"/> moves only while <paramref name="blocked"/>
    /// is in.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a part's, or both are the same part.</exception>
    public void AddBlock(int blocker, int blocked)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)blocker, (uint)_parts.Length, nameof(blocker));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)blocked, (uint)_parts.Length, nameof(blocked));
        if (blocker == blocked)
        {
            throw new ArgumentOutOfRangeException(nameof(blocked), blocked, "A part cannot block itself.");
        }

        _blocks.Add(new Block(blocker, blocked));
    }

    /// <summary>
    /// Locks or unlocks <paramref name="part"/>: a locked part cannot be grabbed, for example while the player scrubs
    /// it clean, and a press on it is not <see cref="DragEvents.Refused"/>. A held part stays held until it is let go.
    /// </summary>
    public void SetLocked(int part, bool locked) => _locked[part] = locked;

    public bool IsLocked(int part) => _locked[part];

    /// <summary>
    /// Sets how far up <paramref name="part"/> lies while it is in the gun, for the pointer: where grab areas overlap,
    /// the part with the higher depth wins, and between equal depths the earlier part. Parts loose on the work surface
    /// lie over every part in the gun, a later one over an earlier one, the way they are drawn. Depths are 0 by
    /// default.
    /// </summary>
    public void SetDepth(int part, int depth) => _depths[part] = depth;

    /// <summary>
    /// The part on top under <paramref name="point"/>, or -1 if there is none, whether it can move or not.
    /// </summary>
    public int PartUnder(Vector2 point)
    {
        SortTopFirst();
        for (var i = 0; i < _order.Length; i++)
        {
            if (_parts[_order[i]].IsUnder(point))
            {
                return _order[i];
            }
        }

        return -1;
    }

    /// <summary>
    /// Lets a loose part stick to its rail again, or not (a dirty part does not fit). Parts may dock by default.
    /// </summary>
    public void SetDockable(int part, bool dockable) => _undockable[part] = !dockable;

    /// <summary>
    /// Whether loose <paramref name="part"/> may stick to its rail now: it may dock, and no other part
    /// <see cref="Holds"/> it.
    /// </summary>
    public bool CanDock(int part) => !_undockable[part] && !IsHeldBack(part);

    /// <summary>
    /// Whether <paramref name="other"/> keeps <paramref name="part"/> from moving right now: <paramref name="other"/>
    /// blocks it and is not out yet, or <paramref name="part"/> blocks <paramref name="other"/> and
    /// <paramref name="other"/> is not back in yet.
    /// </summary>
    public bool Holds(int other, int part)
    {
        for (var i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            if (block.Blocker == other && block.Blocked == part && _parts[other].Stop != PartStop.End)
            {
                return true;
            }

            if (block.Blocker == part && block.Blocked == other && _parts[other].Stop != PartStop.Start)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="part"/> can move: it is held, or it is not locked and no other part
    /// <see cref="Holds"/> it.
    /// </summary>
    public bool CanMove(int part)
    {
        if (_parts[part].IsHeld)
        {
            return true;
        }

        return !_locked[part] && !IsHeldBack(part);
    }

    /// <summary>
    /// Whether every part is snapped into <paramref name="stop"/>: <see cref="PartStop.End"/> once the gun is taken
    /// apart, <see cref="PartStop.Start"/> once it is put back together.
    /// </summary>
    public bool AllAt(PartStop stop)
    {
        for (var i = 0; i < _parts.Length; i++)
        {
            if (_parts[i].Stop != stop)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Advances every part by one fixed step. At most one part has the pointer: the one held, else the one on top under
    /// it (see <see cref="SetDepth"/>). A press on a part that cannot move reports <see cref="DragEvents.Refused"/> for it. A part loose on
    /// the work surface can always be moved unless it is locked; whether it may go back onto its rail is
    /// <see cref="CanDock"/>.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    public void Step(in PointerSample input, float deltaTime)
    {
        var claimed = false;
        for (var i = 0; i < _parts.Length; i++)
        {
            claimed |= _parts[i].IsHeld;
        }

        SortTopFirst();
        foreach (var i in _order)
        {
            var part = _parts[i];
            var canMove = CanMove(i) || (part.IsFree && !_locked[i]);
            var events = part.Step(input, deltaTime, canMove && !claimed, CanDock(i));
            if (!canMove && !claimed && !_locked[i] && input.IsPressed && part.IsUnder(input.Position))
            {
                events |= DragEvents.Refused;
                claimed = true;
            }

            claimed |= part.IsHeld || part.IsHovered;
            _events[i] = events;
        }
    }

    /// <summary>
    /// Puts the parts into <see cref="_order"/>, top first.
    /// </summary>
    private void SortTopFirst()
    {
        for (var i = 0; i < _order.Length; i++)
        {
            var part = i;
            var at = i;
            for (; at > 0 && IsAbove(part, _order[at - 1]); at--)
            {
                _order[at] = _order[at - 1];
            }

            _order[at] = part;
        }
    }

    /// <summary>
    /// Whether <paramref name="part"/> lies over <paramref name="other"/>, an earlier part.
    /// </summary>
    private bool IsAbove(int part, int other)
    {
        var free = _parts[part].IsFree;
        if (free != _parts[other].IsFree)
        {
            return free;
        }

        return free || _depths[part] > _depths[other];
    }

    private bool IsHeldBack(int part)
    {
        for (var i = 0; i < _parts.Length; i++)
        {
            if (i != part && Holds(i, part))
            {
                return true;
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Block(int Blocker, int Blocked);
}
