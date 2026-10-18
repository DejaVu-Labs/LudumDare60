using System.Text.Json.Serialization;

namespace LudumDare60.Game.Orders;

/// <summary>
/// One customer's order: who brings the revolver, what is wrong with it, which parts the player may move, what is
/// dirty or broken, what hides inside, and the phone message that follows. See docs/gamedesign/core-loop.md.
/// </summary>
public sealed record Order
{
    /// <summary>
    /// The constructor the JSON reader uses: lists the JSON leaves out are empty.
    /// </summary>
    [JsonConstructor]
    public Order(
        string id,
        Customer customer,
        Symptom symptom,
        IReadOnlyList<string> parts,
        Find find,
        string message,
        int targetSeconds,
        IReadOnlyList<DirtSpec>? dirty = null,
        IReadOnlyList<string>? broken = null,
        IReadOnlyList<string>? oil = null)
    {
        Id = id;
        Customer = customer;
        Symptom = symptom;
        Parts = parts;
        Find = find;
        Message = message;
        TargetSeconds = targetSeconds;
        Dirty = dirty ?? [];
        Broken = broken ?? [];
        Oil = oil ?? [];
    }

    /// <summary>
    /// A short lowercase kebab-case name, unique in the book.
    /// </summary>
    public string Id { get; }

    public Customer Customer { get; }

    /// <summary>
    /// The icon in the customer's speech bubble.
    /// </summary>
    public Symptom Symptom { get; }

    /// <summary>
    /// The parts the player may move, by <see cref="RevolverParts"/> id. Every other part stays fixed.
    /// </summary>
    public IReadOnlyList<string> Parts { get; }

    public Find Find { get; }

    /// <summary>
    /// The phone message after the customer leaves: today's side of the story.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// How long the order should take a player who does not stall, for pacing checks.
    /// </summary>
    public int TargetSeconds { get; }

    /// <summary>
    /// Parts that must be scrubbed clean before they go back in.
    /// </summary>
    public IReadOnlyList<DirtSpec> Dirty { get; }

    /// <summary>
    /// Parts that must be swapped for a new one from the drawer.
    /// </summary>
    public IReadOnlyList<string> Broken { get; }

    /// <summary>
    /// Parts whose joint needs a drop of oil.
    /// </summary>
    public IReadOnlyList<string> Oil { get; }

    /// <summary>
    /// Whether the order lets the player move <paramref name="part"/>.
    /// </summary>
    public bool Unlocks(string part)
    {
        foreach (var id in Parts)
        {
            if (string.Equals(id, part, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal void Validate(List<string> problems)
    {
        var where = $"Order '{Id}'";
        if (!IsKebab(Id))
        {
            problems.Add($"{where}: the id must be lowercase kebab-case.");
        }

        if (Parts.Count == 0)
        {
            problems.Add($"{where} unlocks no parts.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            if (!RevolverParts.IsKnown(part))
            {
                problems.Add($"{where}: '{part}' is not a revolver part.");
            }
            else if (!seen.Add(part))
            {
                problems.Add($"{where} lists '{part}' twice.");
            }
        }

        foreach (var block in RevolverParts.Blocks)
        {
            if (Unlocks(block.Blocked) && !Unlocks(block.Blocker))
            {
                problems.Add($"{where}: '{block.Blocked}' cannot come off, because '{block.Blocker}' blocks it and stays fixed.");
            }
        }

        foreach (var dirt in Dirty)
        {
            RequireUnlocked(problems, where, dirt.Part, "dirty");
            if (dirt.Threshold is not (> 0 and <= 1))
            {
                problems.Add($"{where}: the clean threshold of '{dirt.Part}' must lie in (0, 1].");
            }
        }

        foreach (var part in Broken)
        {
            RequireUnlocked(problems, where, part, "broken");
        }

        foreach (var part in Oil)
        {
            RequireUnlocked(problems, where, part, "oiled");
        }

        RequireUnlocked(problems, where, Find.Part, "the find's");
        if (!IsKebab(Find.Id))
        {
            problems.Add($"{where}: the find id must be lowercase kebab-case.");
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            problems.Add($"{where} has no phone message.");
        }

        if (TargetSeconds <= 0)
        {
            problems.Add($"{where}: the target time must be positive.");
        }
    }

    private void RequireUnlocked(List<string> problems, string where, string part, string role)
    {
        if (!Unlocks(part))
        {
            problems.Add($"{where}: {role} part '{part}' is not one of the parts it unlocks.");
        }
    }

    private static bool IsKebab(string id)
    {
        if (id.Length == 0 || id[0] == '-' || id[^1] == '-')
        {
            return false;
        }

        foreach (var c in id)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
