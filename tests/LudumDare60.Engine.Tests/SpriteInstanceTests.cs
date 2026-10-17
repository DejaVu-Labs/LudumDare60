using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Tests;

public sealed class SpriteInstanceTests
{
    [Fact]
    public void Size_Is40Bytes()
    {
        Assert.Equal(40, SpriteInstance.Size);
        Assert.Equal(SpriteInstance.Size, Unsafe.SizeOf<SpriteInstance>());
        Assert.Equal(SpriteInstance.Size, Marshal.SizeOf<SpriteInstance>());
    }

    [Fact]
    public void Constructor_FieldsLandAtWireOffsets()
    {
        var value = new SpriteInstance(
            new Vector2(1, 2), new Vector2(3, 4), new Vector2(5, 6), 7, new Color(8, 9, 10, 11), 12, 13, 14, 15);

        var bytes = MemoryMarshal.AsBytes(new ReadOnlySpan<SpriteInstance>(in value));

        Assert.Equal(1f, BitConverter.ToSingle(bytes[0..]));
        Assert.Equal(2f, BitConverter.ToSingle(bytes[4..]));
        Assert.Equal(3f, BitConverter.ToSingle(bytes[8..]));
        Assert.Equal(4f, BitConverter.ToSingle(bytes[12..]));
        Assert.Equal(5f, BitConverter.ToSingle(bytes[16..]));
        Assert.Equal(6f, BitConverter.ToSingle(bytes[20..]));
        Assert.Equal(7f, BitConverter.ToSingle(bytes[24..]));
        Assert.Equal([8, 9, 10, 11], bytes[28..32].ToArray());
        Assert.Equal(12, BitConverter.ToUInt16(bytes[32..]));
        Assert.Equal(13, BitConverter.ToUInt16(bytes[34..]));
        Assert.Equal(14, BitConverter.ToUInt16(bytes[36..]));
        Assert.Equal(15, BitConverter.ToUInt16(bytes[38..]));
    }
}
