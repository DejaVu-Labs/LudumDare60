using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// The hammer and the trigger of a single-action revolver, as a repair uses them. The first step of every repair is
/// half cock: the player pulls the hammer's spur back until the half-cock notch clicks, and it stays there, so the
/// cylinder turns freely and the parts can come out. The last step is the test: from half cock the player pulls the
/// hammer on to full cock (the cylinder turns one chamber), then clicks the trigger, and the hammer falls with a
/// dry-fire click. The trigger does nothing unless the hammer is at full cock; a hammer let go before the half-cock
/// notch falls back down; once it sits in the notch it cannot be pushed lower. The spur moves along the chord of its
/// arc, and the hammer turns with it. Advanced once per fixed step.
/// </summary>
public sealed class ActionTest
{
    /// <summary>
    /// How far the hammer turns back around its pivot from rest to full cock, in radians, clockwise on screen.
    /// </summary>
    public const float CockAngle = MathF.PI / 4;

    /// <summary>
    /// Where the half-cock notch sits, as a share of the way from rest to full cock.
    /// </summary>
    public const float HalfCock = 0.45f;

    /// <summary>
    /// How long the trigger stays pulled after firing, in seconds.
    /// </summary>
    public const float TriggerPullSeconds = 0.2f;

    // How fast the hammer follows the thumb: the gap shrinks to 1/e in 1/HammerFollow seconds.
    private const float HammerFollow = 20;

    // How close to full cock the hammer must come for the sear to catch it.
    private const float FullCockCatch = 0.97f;

    // Where the hammer and the trigger take the pointer, by their shapes: the hammer around its pivot as far as it is
    // turned back, the trigger around its pivot.
    private static readonly GrabShape s_cockGrab = PlaceholderParts.CreateCockGrab();
    private static readonly GrabShape s_triggerGrab = PlaceholderParts.CreateTriggerGrab();

    private static readonly Vector2 s_spurCocked = Vector2.Transform(PlaceholderParts.HammerSpur, Matrix3x2.CreateRotation(CockAngle));

    private readonly LinePath _spurPath = new(BenchLayout.HammerPivot + PlaceholderParts.HammerSpur, BenchLayout.HammerPivot + s_spurCocked);
    private float _grabOffset;

    /// <summary>
    /// Where the hammer's spur is at rest, on the mat.
    /// </summary>
    public Vector2 SpurAtRest => _spurPath.Start;

    /// <summary>
    /// Where the hammer's spur is at full cock, on the mat.
    /// </summary>
    public Vector2 SpurCocked => _spurPath.End;

    /// <summary>
    /// The direction the spur moves when cocked.
    /// </summary>
    public Vector2 CockDirection => _spurPath.Direction;

    /// <summary>
    /// Where the spur is now.
    /// </summary>
    public Vector2 Spur => _spurPath.PointAt(Share * _spurPath.Length);

    /// <summary>
    /// A point of the trigger the player clicks, on the mat: the middle of the finger piece.
    /// </summary>
    public static Vector2 TriggerCenter => BenchLayout.TriggerPivot + new Vector2(4, 11);

    public HammerState State { get; private set; }

    /// <summary>
    /// How far back the hammer is, from 0 (down) to 1 (full cock).
    /// </summary>
    public float Share { get; private set; }

    /// <summary>
    /// <see cref="Share"/> before the last step, for interpolated rendering.
    /// </summary>
    public float PreviousShare { get; private set; }

    public bool IsHammerHeld { get; private set; }

    /// <summary>
    /// Whether the pointer is over the hammer, above its screw, and could take it.
    /// </summary>
    public bool IsHammerHovered { get; private set; }

    /// <summary>
    /// Whether the hammer is at least at half cock: the parts may come out.
    /// </summary>
    public bool IsOpen => State != HammerState.Down;

    /// <summary>
    /// Whether the trigger has dropped the hammer from full cock: the test is done.
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
    public float GetHammerAngle(float alpha) => float.Lerp(PreviousShare, Share, alpha) * CockAngle;

    /// <summary>
    /// Advances the hammer and the trigger by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    /// <param name="canCock">Whether the hammer may go on from half cock to full cock: only for the test, since a
    /// cocked hammer locks the cylinder.</param>
    /// <param name="canGrab">Whether the hammer and the trigger may be taken: false while the player holds a part or
    /// the brush.</param>
    public void Step(in PointerSample input, float deltaTime, bool canCock, bool canGrab)
    {
        Events = TestEvents.None;
        PreviousShare = Share;
        TriggerPull = MathF.Max(0, TriggerPull - (deltaTime / TriggerPullSeconds));
        MoveHammer(input, deltaTime, canCock, canGrab);

        IsTriggerHovered = canGrab && !IsHammerHeld && s_triggerGrab.Contains(input.Position - BenchLayout.TriggerPivot);
        if (!input.IsPressed || !IsTriggerHovered)
        {
            return;
        }

        if (State != HammerState.FullCock)
        {
            Events |= TestEvents.TriggerRefused;
            return;
        }

        // The sear lets go: the hammer falls on an empty chamber.
        State = HammerState.Down;
        Share = PreviousShare = 0;
        IsFired = true;
        TriggerPull = 1;
        Events |= TestEvents.DryFire;
    }

    private void MoveHammer(in PointerSample input, float deltaTime, bool canCock, bool canGrab)
    {
        if (!IsHammerHeld)
        {
            IsHammerHovered = canGrab && State != HammerState.FullCock && s_cockGrab.Contains(input.Position - BenchLayout.HammerPivot, Share * CockAngle);
            if (!input.IsPressed || !IsHammerHovered)
            {
                return;
            }

            IsHammerHeld = true;
            IsHammerHovered = false;
            _grabOffset = _spurPath.Project(input.Position) - (Share * _spurPath.Length);
        }

        if (!input.IsDown)
        {
            // Let go: the half-cock notch holds the hammer; below it the mainspring takes it back down.
            IsHammerHeld = false;
            if (State == HammerState.HalfCock)
            {
                Share = HalfCock;
            }
            else
            {
                if (Share > 0.01f)
                {
                    Events |= TestEvents.HammerEased;
                }

                Share = 0;
            }

            return;
        }

        // Once in the notch the hammer cannot be pushed lower; without the test it goes no further than half cock.
        var floor = State == HammerState.HalfCock ? HalfCock : 0;
        var ceiling = canCock ? 1 : HalfCock;
        var target = Math.Clamp((_spurPath.Project(input.Position) - _grabOffset) / _spurPath.Length, floor, ceiling);
        Share += (target - Share) * (1 - MathF.Exp(-HammerFollow * deltaTime));
        if (State == HammerState.Down && Share >= HalfCock - 0.01f)
        {
            State = HammerState.HalfCock;
            Share = MathF.Max(Share, HalfCock);
            Events |= TestEvents.HalfCock;
        }

        if (canCock && Share >= FullCockCatch)
        {
            // The sear catches the full-cock notch: the hammer stays back and the cylinder turns one chamber.
            State = HammerState.FullCock;
            Share = 1;
            IsHammerHeld = false;
            Chamber = (Chamber + 1) % 6;
            Events |= TestEvents.Cocked;
        }
    }
}
