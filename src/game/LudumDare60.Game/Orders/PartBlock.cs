using System.Runtime.InteropServices;

namespace LudumDare60.Game.Orders;

/// <summary>
/// <paramref name="Blocker"/> keeps <paramref name="Blocked"/> in place until it is out, by part id.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PartBlock(string Blocker, string Blocked);
