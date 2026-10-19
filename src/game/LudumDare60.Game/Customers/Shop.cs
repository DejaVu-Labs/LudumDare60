using System.Numerics;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Customers;

/// <summary>
/// The shop loop of docs/gamedesign/core-loop.md, one customer at a time: the door opens and the customer walks to
/// the counter and waits with the revolver until the player clicks them, the revolver flies onto the mat, the player
/// puts the hammer at half cock and repairs the revolver on the <see cref="OrderBench"/>, then tests it
/// (<see cref="ActionTest"/>: full cock, pull the trigger), the customer holds out a hand, the player drags the whole
/// revolver up to them, they thank the player and leave, and the next customer comes in. The orders come in the order
/// of the <see cref="OrderBook"/>; at the first one the bench cannot play yet, the shop closes. Advance it once per
/// fixed step with <see cref="Step"/>.
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
    /// How long the revolver flies from the customer's hands to the mat, in seconds.
    /// </summary>
    public const float ThrowSeconds = 0.45f;

    /// <summary>
    /// How far above the straight line from the hands to the mat the revolver flies at the top of its arc, in virtual
    /// pixels.
    /// </summary>
    public const float ThrowHeight = 28;

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
    /// Where a customer's bust stands, in virtual pixels: on the far edge of the bench.
    /// </summary>
    public const float CustomerBottom = 141;

    /// <summary>
    /// How far left and right of their middle the player can click a customer, in virtual pixels.
    /// </summary>
    public const float CustomerHalfWidth = 40;

    /// <summary>
    /// How far up from their bottom the player can click a customer, in virtual pixels.
    /// </summary>
    public const float CustomerHeight = 124;

    /// <summary>
    /// Where the revolver leaves the customer's hands, relative to where it lies on the mat: the middle of the
    /// cylinder held at the customer's chest.
    /// </summary>
    public static readonly Vector2 ThrowFrom = new Vector2(CounterX, CustomerBottom - 24) - BenchLayout.CylinderSeated;

    /// <summary>
    /// Where the player drags the revolver to hand it back: up to the customer's hand, relative to where it lies.
    /// </summary>
    public static readonly Vector2 HandBackOffset = new(0, -48);

    /// <summary>
    /// The point of the revolver the player grabs to hand it back: the middle of the cylinder in its frame.
    /// </summary>
    public static readonly Vector2 GunGrip = BenchLayout.CylinderSeated;

    // What the bench sees while the player holds the hammer: nowhere, no button.
    private static readonly PointerSample s_noPointer = new(new Vector2(-10_000), IsDown: false, IsPressed: false);

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
    /// The scrubbing brush. It belongs to the shop, not to an order: it stays where the player left it from one
    /// customer to the next, and lies on the bench while the counter is empty.
    /// </summary>
    public BrushTool Brush { get; } = OrderBench.CreateBrush();

    /// <summary>
    /// The oil can, kept like <see cref="Brush"/>.
    /// </summary>
    public BrushTool OilCan { get; } = OrderBench.CreateOilCan();

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
    /// The hammer and trigger of the current customer's revolver, tested once it is back together. Null when there is
    /// no customer.
    /// </summary>
    public ActionTest? Test { get; private set; }

    /// <summary>
    /// What happened in the last step.
    /// </summary>
    public ShopEvents Events { get; private set; }

    /// <summary>
    /// Whether the last step advanced the bench, so its events belong to that step.
    /// </summary>
    public bool BenchStepped { get; private set; }

    /// <summary>
    /// Whether the last step advanced the test, so its events belong to that step.
    /// </summary>
    public bool TestStepped { get; private set; }

    /// <summary>
    /// Whether the pointer is over the customer while they wait to hand the revolver over.
    /// </summary>
    public bool IsCustomerHovered { get; private set; }

    /// <summary>
    /// Whether a customer stands in the shop.
    /// </summary>
    public bool HasCustomer => Phase is not (ShopPhase.Waiting or ShopPhase.Closed);

    /// <summary>
    /// Whether the revolver lies on the mat: from the moment it is put down until the customer takes it.
    /// </summary>
    public bool HasGun => Phase is ShopPhase.Working or ShopPhase.Testing or ShopPhase.Ready;

    public Expression Expression => Phase is ShopPhase.Thanking or ShopPhase.Leaving ? Expression.Thankful : Expression.Neutral;

    /// <summary>
    /// Whether the customer holds out a hand for the revolver.
    /// </summary>
    public bool IsHandOut => Phase == ShopPhase.Ready;

    /// <summary>
    /// The horizontal middle of the customer, in virtual pixels. A customer with a fixed place (a parcel) does not
    /// walk: it stays at its place from coming in until it is gone.
    /// </summary>
    public float CustomerX => FixedPlace is { } place ? place.Bottom.X : Phase switch
    {
        ShopPhase.Arriving => float.Lerp(DoorX, CounterX, Ease(PhaseTime / WalkSeconds)),
        ShopPhase.Leaving => float.Lerp(CounterX, DoorX, Ease(PhaseTime / WalkSeconds)),
        _ => CounterX,
    };

    /// <summary>
    /// The place the current customer stays at in every phase (see <see cref="CustomerSeats.TryGetFixedPlace"/>), or
    /// null for a customer who walks in to the counter.
    /// </summary>
    public CustomerSeat? FixedPlace
        => Bench is { } bench && CustomerSeats.TryGetFixedPlace(bench.Order.Customer.Look, out var place) ? place : null;

    /// <summary>
    /// How long the customer keeps presenting the revolver at the counter before waiting on the phone, in seconds.
    /// </summary>
    public const float PresentSeconds = 2.5f;

    /// <summary>
    /// How the customer stands: presenting the revolver as they come in and for a moment after putting it down, on the
    /// phone while the player works, presenting again with a hand out to take it back, and pleased once they have it.
    /// A parcel (a customer with a <see cref="FixedPlace"/>) comes in closed and is open from the moment the revolver
    /// comes out of it until it goes back in, then taped shut.
    /// </summary>
    public CustomerPose Pose => Phase switch
    {
        ShopPhase.Working when PhaseTime < PresentSeconds && FixedPlace is null => CustomerPose.Presenting,
        ShopPhase.Working or ShopPhase.Testing => CustomerPose.Waiting,
        ShopPhase.Ready when FixedPlace is not null => CustomerPose.Waiting,
        ShopPhase.Thanking or ShopPhase.Leaving => CustomerPose.Pleased,
        _ => CustomerPose.Presenting,
    };

    /// <summary>
    /// How far the revolver is drawn from where it lies on the bench: flying from the customer's hands onto the mat at
    /// first, then lifted towards the customer while the player hands it back.
    /// </summary>
    /// <param name="alpha">How far the current time is between the last fixed step and the next one.</param>
    public Vector2 GetGunOffset(float alpha)
    {
        if (Phase == ShopPhase.Working && PhaseTime < ThrowSeconds)
        {
            // Straight from the hands (or out of the parcel) to the mat, lifted on an arc that peaks halfway.
            var t = PhaseTime / ThrowSeconds;
            var from = FixedPlace is { } place ? new Vector2(place.Bottom.X, place.Bottom.Y - 24) - BenchLayout.CylinderSeated : ThrowFrom;
            return (from * (1 - t)) + new Vector2(0, -ThrowHeight * 4 * t * (1 - t));
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
        TestStepped = false;
        IsCustomerHovered = false;
        PhaseTime += deltaTime;
        switch (Phase)
        {
            case ShopPhase.Waiting when PhaseTime >= WaitSeconds:
                LetNextCustomerIn();
                break;
            case ShopPhase.Arriving when PhaseTime >= WalkSeconds:
                Enter(ShopPhase.Offering);
                break;
            case ShopPhase.Offering:
                IsCustomerHovered = IsOverCustomer(input.Position);
                if (IsCustomerHovered && input.IsPressed)
                {
                    Events |= ShopEvents.GunThrown;
                    Enter(ShopPhase.Working);
                }

                break;
            case ShopPhase.Working:
                Work(input, deltaTime);
                break;
            case ShopPhase.Testing:
                Test!.Step(input, deltaTime, canCock: true, canGrab: true);
                TestStepped = true;
                if (Test.IsFired)
                {
                    Events |= ShopEvents.Repaired;
                    Enter(ShopPhase.Ready);
                }

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
                Test = null;
                OrderIndex++;
                Enter(ShopPhase.Waiting);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Sends the current customer away, with whatever is on the bench, and lets the customer of the order at
    /// <paramref name="orderIndex"/> in on the next step, without the wait: a debug shortcut.
    /// </summary>
    /// <param name="orderIndex">The order's index in the book.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="orderIndex"/> is not in the book.</exception>
    public void Restart(int orderIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(orderIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(orderIndex, _book.Orders.Count);
        Bench = null;
        HandBack = null;
        Test = null;
        IsCustomerHovered = false;
        OrderIndex = orderIndex;
        Enter(ShopPhase.Waiting);
        PhaseTime = WaitSeconds;
    }

    /// <summary>
    /// Sends the current customer away, with whatever is on the bench, and closes the shop at once, as after the last
    /// order: a debug shortcut to the ending.
    /// </summary>
    public void Close()
    {
        Bench = null;
        HandBack = null;
        Test = null;
        IsCustomerHovered = false;
        OrderIndex = _book.Orders.Count;
        Enter(ShopPhase.Closed);
    }

    /// <summary>
    /// Whether <paramref name="point"/>, in virtual pixels, is on the customer.
    /// </summary>
    public bool IsOverCustomer(Vector2 point)
    {
        if (!HasCustomer)
        {
            return false;
        }

        if (FixedPlace is { } place)
        {
            // A parcel fills the lower half of its frame.
            var halfWidth = place.Scale * CustomerSheet.FrameWidth / 2;
            var height = place.Scale * CustomerSheet.FrameHeight / 2;
            return MathF.Abs(point.X - place.Bottom.X) <= halfWidth && point.Y <= place.Bottom.Y && point.Y >= place.Bottom.Y - height;
        }

        return MathF.Abs(point.X - CustomerX) <= CustomerHalfWidth && point.Y <= CustomerBottom && point.Y >= CustomerBottom - CustomerHeight;
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

        Bench = new OrderBench(_book.Orders[OrderIndex], Brush, OilCan);
        HandBack = new PartDrag(new LinePath(GunGrip, GunGrip + HandBackOffset), grabRadius: 24, s_handBackFeel);
        Test = new ActionTest();
        Events |= ShopEvents.CustomerEntered;
        Enter(ShopPhase.Arriving);
    }

    private void Work(in PointerSample input, float deltaTime)
    {
        if (PhaseTime < ThrowSeconds)
        {
            return;
        }

        if (PhaseTime - deltaTime < ThrowSeconds)
        {
            // The step the revolver lands in; the parts can be touched from the next one on.
            Events |= ShopEvents.GunLanded;
            return;
        }

        // The hammer first: half cock opens the revolver. While it is in the hand, the parts do not see the pointer;
        // while it is off its pivot, it cannot be cocked.
        var bench = Bench!;
        var busy = bench.Brush.IsHeld || bench.OilCan.IsHeld || bench.IsHammerOut;
        for (var i = 0; i < bench.Count; i++)
        {
            busy |= bench.Gun[i].IsHeld;
        }

        Test!.Step(input, deltaTime, canCock: false, canGrab: !busy);
        TestStepped = true;
        bench.Step(Test.IsHammerHeld ? s_noPointer : input, deltaTime, Test.IsOpen, Test.GetHammerAngle(1));
        BenchStepped = true;
        if (bench.IsDone)
        {
            Events |= ShopEvents.Assembled;
            Enter(ShopPhase.Testing);
        }
    }

    private void Enter(ShopPhase phase)
    {
        Phase = phase;
        PhaseTime = 0;
    }
}
