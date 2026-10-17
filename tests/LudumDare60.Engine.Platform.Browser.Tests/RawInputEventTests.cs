using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LudumDare60.Engine.Platform.Browser.Tests;

/// <summary>
/// Checks <see cref="RawInputEvent"/> against the bytes <c>input.js</c> writes. The fixture is shared with
/// <c>tests/js/input.test.mjs</c>, which checks that <c>input.js</c> encodes the same events to the same bytes.
/// </summary>
public sealed class RawInputEventTests
{
    private static readonly string s_fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "raw-input-events.json");

    [Fact]
    public void Size_Is24Bytes()
    {
        Assert.Equal(24, RawInputEvent.Size);
        Assert.Equal(RawInputEvent.Size, Unsafe.SizeOf<RawInputEvent>());
        Assert.Equal(RawInputEvent.Size, Marshal.SizeOf<RawInputEvent>());
    }

    [Fact]
    public void Constructor_FieldsLandAtWireOffsets()
    {
        var value = new RawInputEvent(RawInputEventKind.Wheel, 0xAB, 0xCDEF, 0x12345678, 1, 2, 3, 4);

        var bytes = MemoryMarshal.AsBytes(new ReadOnlySpan<RawInputEvent>(in value));

        Assert.Equal((byte)RawInputEventKind.Wheel, bytes[0]);
        Assert.Equal(0xAB, bytes[1]);
        Assert.Equal(0xCDEF, BitConverter.ToUInt16(bytes[2..]));
        Assert.Equal(0x12345678, BitConverter.ToInt32(bytes[4..]));
        Assert.Equal(1f, BitConverter.ToSingle(bytes[8..]));
        Assert.Equal(2f, BitConverter.ToSingle(bytes[12..]));
        Assert.Equal(3f, BitConverter.ToSingle(bytes[16..]));
        Assert.Equal(4f, BitConverter.ToSingle(bytes[20..]));
    }

    [Fact]
    public void Cast_FixtureBytes_DecodesFixtureEvents()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(s_fixturePath));
        var bytes = Convert.FromHexString(fixture.RootElement.GetProperty("hex").GetString()!);
        var expected = fixture.RootElement.GetProperty("events").EnumerateArray().ToArray();

        var events = MemoryMarshal.Cast<byte, RawInputEvent>(bytes);

        Assert.Equal(expected.Length, events.Length);
        for (var i = 0; i < events.Length; i++)
        {
            var e = expected[i];
            var actual = events[i];
            Assert.Equal((RawInputEventKind)e.GetProperty("kind").GetInt32(), actual.Kind);
            Assert.Equal(e.GetProperty("detail").GetByte(), actual.Detail);
            Assert.Equal(e.GetProperty("code").GetUInt16(), actual.Code);
            Assert.Equal(e.GetProperty("id").GetInt32(), actual.Id);
            Assert.Equal(e.GetProperty("time").GetSingle(), actual.Time);
            Assert.Equal(e.GetProperty("x").GetSingle(), actual.X);
            Assert.Equal(e.GetProperty("y").GetSingle(), actual.Y);
            Assert.Equal(e.GetProperty("z").GetSingle(), actual.Z);
        }
    }

    [Fact]
    public void Key_MatchesInputJsKeyTable()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(s_fixturePath));
        var table = fixture.RootElement.GetProperty("keys").EnumerateObject()
            .ToDictionary(static p => p.Name, static p => p.Value.GetInt32(), StringComparer.Ordinal);

        var keys = Enum.GetValues<Key>().Where(static k => k != Key.None)
            .ToDictionary(static k => DomCode(k), static k => (int)k, StringComparer.Ordinal);

        Assert.Equal(table.OrderBy(static p => p.Key, StringComparer.Ordinal), keys.OrderBy(static p => p.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Key_ReadsCode()
    {
        var value = new RawInputEvent(RawInputEventKind.KeyDown, 0, (ushort)Key.ArrowUp, 0, 0, 0, 0, 0);

        Assert.Equal(Key.ArrowUp, value.Key);
    }

    /// <summary>
    /// <c>KeyboardEvent.code</c> of a key: letters are <c>KeyA</c>..<c>KeyZ</c>, every other name is the code itself.
    /// </summary>
    private static string DomCode(Key key)
    {
        var name = key.ToString();
        return name.Length == 1 && char.IsAsciiLetterUpper(name[0]) ? $"Key{name}" : name;
    }
}
