namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Writes queued input events into <paramref name="destination"/> as <see cref="RawInputEvent"/> bytes.
/// </summary>
/// <param name="destination">Room for <c>destination.Length / RawInputEvent.Size</c> events.</param>
/// <param name="frameTimeMs">Timestamp of the current frame, in milliseconds.</param>
/// <returns>Number of events written.</returns>
internal delegate int InputDrain(Span<byte> destination, double frameTimeMs);
