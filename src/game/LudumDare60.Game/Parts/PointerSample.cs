using System.Numerics;
using System.Runtime.InteropServices;
using LudumDare60.Engine.Input;

namespace LudumDare60.Game.Parts;

/// <summary>
/// The pointer as one fixed step sees it: where it is and what the primary button does.
/// </summary>
/// <param name="Position">In virtual pixels.</param>
/// <param name="IsDown">Whether the button is down at the end of the step.</param>
/// <param name="IsPressed">Whether the button went down during the step.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PointerSample(Vector2 Position, bool IsDown, bool IsPressed)
{
    /// <summary>
    /// Reads the pointer and the primary button from <paramref name="input"/>.
    /// </summary>
    public static PointerSample From(InputState input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new(input.PointerPosition, input.IsDown(PointerButton.Primary), input.IsPressed(PointerButton.Primary));
    }
}
