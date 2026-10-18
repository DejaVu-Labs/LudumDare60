using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// The test of a repaired revolver, the last step before handing it back: the player pulls the hammer back to full
/// cock (a ratchet click at half cock, and the cylinder turns one chamber at full cock), then clicks the trigger, and
/// the hammer falls with a dry-fire click. A single-action revolver: the trigger does nothing while the hammer is
/// down, and a hammer let go before full cock springs back down. Advanced once per fixed step.
/// </summary>
public sealed class ActionTest
{
    /// <summary>
    /// How far the hammer turns back around its pivot from rest to full cock, in radians, clockwise on screen.
    /// </summary>
    public const float CockAngle = MathF.PI / 4;

    /// <summary>
    /// The share of the way back where the ratchet clicks at half cock.
    /// </summary>
    public const float HalfCock = 0.45f;

    /// <summary>
    /// How close to the trigger the pointer must be to pull it.
    /// </summary>
    public const float TriggerRadius = 6;

    /// <summary>
    /// How long the trigger stays pulled after firing, in seconds.
    /// </summary>
    public const float TriggerPullSeconds = 0.2f;

    // The hammer's spur goes round on an arc; the drag follows its chord, and the angle follows the drag.
    private static readonly Vector2 s_spurCocked = Vector2.Transform(PlaceholderParts.HammerSpur, Matrix3x2.CreateRotation(CockAngle));

    // The mainspring holds the hammer down: it takes a firm pull to start.
    private static readonly DragFeel s_hammerFeel = new() { BreakawayAtStart = 3, Strain = 0.3f, SnapDistance = 2, Follow = 20 };

    private float _lastShare;

    public ActionTest()
    {
        var spur = BenchLayout.HammerPivot + PlaceholderParts.HammerSpur;
        Hammer = new PartDrag(new LinePath(spur, BenchLayout.HammerPivot + s_spurCocked), grabRadius: 6, s_hammerFeel);
    }

    /// <summary>
    /// The hammer's spur, dragged from rest (<see cref="PartStop.Start"/>) to full cock (<see cref="PartStop.End"/>).
    /// </summary>
    public PartDrag Hammer { get; }

    /// <summary>
    /// The point of the trigger the player clicks, on the mat.
    /// </summary>
    public static Vector2 TriggerCenter => BenchLayout.TriggerPivot + new Vector2(1, 5);

    public bool IsCocked => Hammer.Stop == PartStop.End;

    /// <summary>
    /// Whether the trigger has released the cocked hammer: the test is done.
    /// </summary>
    public bool IsFired { get; private set; }

    /// <summary>
    /// Which chamber faces the barrel, 0 to 5; every full cock turns the cylinder on by one.
    /// </summary>
    public int Chamber { get; private set; }

    /// <summary>
    /// Whether the pointer is over the trigger and could pull it.
    /// </summary>
    public bool IsTriggerHovered { get; private set; }

    /// <summary>
    /// How far the trigger is pulled, from 1 right after firing back to 0.
    /// </summary>
    public float TriggerPull { get; private set; }

    public TestEvents Events { get; private set; }

    /// <summary>
    /// The hammer's turn back from rest, in radians, <paramref name="alpha"/> of the way through the last step.
    /// </summary>
    public float GetHammerAngle(float alpha)
    {
        if (IsCocked)
        {
            return CockAngle;
        }

        var distance = float.Lerp(Hammer.PreviousDistance, Hammer.Distance, alpha);
        return distance / Hammer.Path.Length * CockAngle;
    }

    /// <summary>
    /// Advances the test by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    public void Step(in PointerSample input, float deltaTime)
    {
        Events = TestEvents.None;
        TriggerPull = MathF.Max(0, TriggerPull - (deltaTime / TriggerPullSeconds));
        if (!IsCocked)
        {
            CockHammer(input, deltaTime);
        }

        IsTriggerHovered = !Hammer.IsHeld && Vector2.Distance(input.Position, TriggerCenter) <= TriggerRadius;
        if (!input.IsPressed || !IsTriggerHovered)
        {
            return;
        }

        if (!IsCocked)
        {
            Events |= TestEvents.TriggerRefused;
            return;
        }

        // The sear lets go: the hammer falls on an empty chamber.
        Hammer.ReturnTo(PartStop.Start);
        _lastShare = 0;
        IsFired = true;
        TriggerPull = 1;
        Events |= TestEvents.DryFire;
    }

    private void CockHammer(in PointerSample input, float deltaTime)
    {
        var events = Hammer.Step(input, deltaTime);
        var share = Hammer.Distance / Hammer.Path.Length;
        if (_lastShare < HalfCock && share >= HalfCock)
        {
            Events |= TestEvents.HalfCock;
        }

        _lastShare = share;
        if ((events & DragEvents.SnappedEnd) != DragEvents.None)
        {
            Chamber = (Chamber + 1) % 6;
            Events |= TestEvents.Cocked;
        }
        else if ((events & DragEvents.Dropped) != DragEvents.None && Hammer.Stop == PartStop.None)
        {
            Hammer.ReturnTo(PartStop.Start);
            _lastShare = 0;
            Events |= TestEvents.HammerEased;
        }
    }
}
