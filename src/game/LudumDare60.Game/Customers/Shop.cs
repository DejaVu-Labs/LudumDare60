using System.Numerics;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Customers;

/// <summary>
/// The shop loop of docs/gamedesign/core-loop.md, one customer at a time: the door opens and the customer walks to
/// the counter, the revolver lands on the mat, the player repairs it on the <see cref="OrderBench"/>, the customer
/// holds out a hand, the player drags the whole revolver up to them, they thank the player and leave, and the next
/// customer comes in. The orders come in the order of the <see cref="OrderBook"/>; at the first one the bench cannot
/// play yet, the shop closes. Advance it once per fixed step with <see cref="Step"/>.
/// </summary>
public sealed class Shop
{
    /// <summary>
    /// How long the counter stays empty before a customer comes in, in seconds.
    /// </summary>
    public const float WaitSeconds = 1.2f;

    /// <summary>
    /// How long a customer takes from the door to the counter and back, in seconds.
    /// </summary>
    public const float WalkSeconds = 1.6f;

    /// <summary>
    /// How long the revolver takes to land on the mat, in seconds.
    /// </summary>
    public const float DropSeconds = 0.25f;

    /// <summary>
    /// How high above the mat the revolver is let go, in virtual pixels.
    /// </summary>
    public const float DropHeight = 12;

    /// <summary>
    /// How long the customer thanks the player before leaving, in seconds.
    /// </summary>
    public const float ThankSeconds = 1.4f;

    /// <summary>
    /// The middle of a customer standing in the door, in virtual pixels.
    /// </summary>
    public const float DoorX = 415;

    /// <summary>
    /// The middle of a customer standing at the counter, in virtual pixels.
    /// </summary>
    public const float CounterX = 290;

    /// <summary>
    /// Where the player drags the revolver to hand it back: up to the customer's hand, relative to where it lies.
    /// </summary>
    public static readonly Vector2 HandBackOffset = new(0, -62);

    /// <summary>
    /// The point of the revolver the player grabs to hand it back: the middle of the frame.
    /// </summary>
    public static readonly Vector2 GunGrip = BenchLayout.FramePosition + new Vector2(PlaceholderParts.Frame.Width / 2f, PlaceholderParts.Frame.Height / 2f);

    // The revolver goes up without breakaway: nothing holds it to the mat.
    private static readonly DragFeel s_handBackFeel = new() { BreakawayAtStart = 0 };

    private readonly OrderBook _book;

    public Shop(OrderBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    public ShopPhase Phase { get; private set; } = ShopPhase.Waiting;

    /// <summary>
    /// Seconds since the current phase began.
    /// </summary>
    public float PhaseTime { get; private set; }

    /// <summary>
    /// The index in the book of the current customer's order, or of the next one while the counter is empty.
    /// </summary>
    public int OrderIndex { get; private set; }

    /// <summary>
    /// The current customer's order on the bench, from the moment they come in until they have left.
    /// </summary>
    public OrderBench? Bench { get; private set; }

    /// <summary>
    /// The whole revolver, dragged from the mat up to the customer to hand it back. Null when there is no customer.
    /// </summary>
    public PartDrag? HandBack { get; private set; }

    /// <summary>
    /// What happened in the last step.
    /// </summary>
    public ShopEvents Events { get; private set; }

    /// <summary>
    /// Whether the last step advanced the bench, so its events belong to that step.
    /// </summary>
    public bool BenchStepped { get; private set; }

    /// <summary>
    /// Whether a customer stands in the shop.
    /// </summary>
    public bool HasCustomer => Phase is ShopPhase.Arriving or ShopPhase.Working or ShopPhase.Ready or ShopPhase.Thanking or ShopPhase.Leaving;

    /// <summary>
    /// Whether the revolver lies on the mat: from the moment it is put down until the customer takes it.
    /// </summary>
    public bool HasGun => Phase is ShopPhase.Working or ShopPhase.Ready;

    public Expression Expression => Phase is ShopPhase.Thanking or ShopPhase.Leaving ? Expression.Thankful : Expression.Neutral;

    /// <summary>
    /// Whether the customer holds out a hand for the revolver.
    /// </summary>
    public bool IsHandOut => Phase == ShopPhase.Ready;

    /// <summary>
    /// The horizontal middle of the customer, in virtual pixels.
    /// </summary>
    public float CustomerX => Phase switch
    {
        ShopPhase.Arriving => float.Lerp(DoorX, CounterX, Ease(PhaseTime / WalkSeconds)),
        ShopPhase.Leaving => float.Lerp(CounterX, DoorX, Ease(PhaseTime / WalkSeconds)),
        _ => CounterX,
    };

    /// <summary>
    /// How far the revolver is drawn from where it lies on the bench: falling onto the mat at first, then lifted
    /// towards the customer while the player hands it back.
    /// </summary>
    /// <param name="alpha">How far the current time is between the last fixed step and the next one.</param>
    public Vector2 GetGunOffset(float alpha)
    {
        if (Phase == ShopPhase.Working && PhaseTime < DropSeconds)
        {
            var left = 1 - (PhaseTime / DropSeconds);
            return new Vector2(0, -DropHeight * left * left);
        }

        return Phase == ShopPhase.Ready ? HandBack!.GetRenderPosition(alpha) - GunGrip : Vector2.Zero;
    }

    /// <summary>
    /// Advances the shop by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    public void Step(in PointerSample input, float deltaTime)
    {
        Events = ShopEvents.None;
        BenchStepped = false;
        PhaseTime += deltaTime;
        switch (Phase)
        {
            case ShopPhase.Waiting when PhaseTime >= WaitSeconds:
                LetNextCustomerIn();
                break;
            case ShopPhase.Arriving when PhaseTime >= WalkSeconds:
                Enter(ShopPhase.Working);
                break;
            case ShopPhase.Working:
                Work(input, deltaTime);
                break;
            case ShopPhase.Ready:
                if ((HandBack!.Step(input, deltaTime) & DragEvents.SnappedEnd) != DragEvents.None)
                {
                    Events |= ShopEvents.HandedBack;
                    Enter(ShopPhase.Thanking);
                }

                break;
            case ShopPhase.Thanking when PhaseTime >= ThankSeconds:
                Enter(ShopPhase.Leaving);
                break;
            case ShopPhase.Leaving when PhaseTime >= WalkSeconds:
                Events |= ShopEvents.CustomerLeft;
                Bench = null;
                HandBack = null;
                OrderIndex++;
                Enter(ShopPhase.Waiting);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// A smooth start and stop for walking: 0 at 0, 1 at 1 and later.
    /// </summary>
    private static float Ease(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    private void LetNextCustomerIn()
    {
        if (OrderIndex >= _book.Orders.Count || !OrderBench.CanSetUp(_book.Orders[OrderIndex], out _))
        {
            Enter(ShopPhase.Closed);
            return;
        }

        Bench = new OrderBench(_book.Orders[OrderIndex]);
        HandBack = new PartDrag(new LinePath(GunGrip, GunGrip + HandBackOffset), grabRadius: 18, s_handBackFeel);
        Events |= ShopEvents.CustomerEntered;
        Enter(ShopPhase.Arriving);
    }

    private void Work(in PointerSample input, float deltaTime)
    {
        if (PhaseTime < DropSeconds)
        {
            return;
        }

        if (PhaseTime - deltaTime < DropSeconds)
        {
            // The step the revolver lands in; the parts can be touched from the next one on.
            Events |= ShopEvents.GunLanded;
            return;
        }

        Bench!.Step(input, deltaTime);
        BenchStepped = true;
        if (Bench.IsDone)
        {
            Events |= ShopEvents.Repaired;
            Enter(ShopPhase.Ready);
        }
    }

    private void Enter(ShopPhase phase)
    {
        Phase = phase;
        PhaseTime = 0;
    }
}
