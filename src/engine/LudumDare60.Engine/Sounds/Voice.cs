using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Sounds;

/// <summary>
/// A looping voice, started by <see cref="Audio.PlayLoop"/>. A handle becomes stale once <see cref="Audio.Stop"/> runs,
/// its sound is destroyed or the run ends; using it then throws. <c>default</c> is never valid.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Voice
{
    internal Voice(int id, int generation)
    {
        Id = id;
        Generation = generation;
    }

    /// <summary>
    /// The backend's voice id. Ids are reused after a voice stops; <see cref="Generation"/> tells the uses apart.
    /// </summary>
    internal int Id { get; }

    internal int Generation { get; }
}
