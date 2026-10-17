using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Sounds;

/// <summary>
/// A decoded (or decoding) sound, created by <see cref="Audio.CreateSound"/>. A handle becomes stale once
/// <see cref="Audio.DestroySound"/> runs or the run ends; using it then throws. <c>default</c> is never valid.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Sound
{
    internal Sound(int id, int generation)
    {
        Id = id;
        Generation = generation;
    }

    /// <summary>
    /// The backend's sound id. Ids are reused after a sound is destroyed; <see cref="Generation"/> tells the uses apart.
    /// </summary>
    internal int Id { get; }

    internal int Generation { get; }
}
