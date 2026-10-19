using LudumDare60.Engine.Platform.Desktop.Rendering;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class TextureTableTests
{
    private readonly TextureTable _table = new();

    [Fact]
    public void Add_ThenGet_ReturnsHandleAndSize()
    {
        _table.Add(40, 1234, 16, 8);

        Assert.Equal(new TextureTable.Entry(1234, 16, 8), _table.Get(40));
        Assert.Equal(1, _table.Count);
        Assert.True(_table.Contains(40));
    }

    [Fact]
    public void CheckCreate_TakenId_Throws()
    {
        _table.Add(1, 5, 1, 1);

        var exception = Assert.Throws<InvalidOperationException>(() => _table.CheckCreate(1, 1, 1, 4));

        Assert.Equal("Texture 1 already exists.", exception.Message);
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 1, 0)]
    public void CheckCreate_BadIdOrSize_Throws(int id, int width, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _table.CheckCreate(id, width, height, 0));

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public void CheckCreate_PixelsOfTheWrongSize_Throws(int bytes)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _table.CheckCreate(0, 4, 2, bytes));

        Assert.Equal($"A 4x2 texture needs 32 bytes, not {bytes}.", exception.Message);
    }

    [Fact]
    public void CheckUpdate_RowsInside_ReturnsTheTexture()
    {
        _table.Add(2, 9, 4, 3);

        Assert.Equal(new TextureTable.Entry(9, 4, 3), _table.CheckUpdate(2, 1, 2, 32));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    public void CheckUpdate_RowsOutside_Throws(int y, int height)
    {
        _table.Add(2, 9, 4, 3);

        var exception = Assert.Throws<InvalidOperationException>(() => _table.CheckUpdate(2, y, height, 4 * height * 4));

        Assert.Equal($"Rows {y}+{height} lie outside texture 2.", exception.Message);
    }

    [Fact]
    public void CheckUpdate_WrongByteCount_Throws()
    {
        _table.Add(2, 9, 4, 3);

        Assert.Throws<InvalidOperationException>(() => _table.CheckUpdate(2, 0, 1, 15));
    }

    [Fact]
    public void Remove_ReturnsHandleAndFreesTheId()
    {
        _table.Add(3, 77, 1, 1);

        Assert.Equal(77, _table.Remove(3));
        Assert.False(_table.Contains(3));
        Assert.Equal(0, _table.Count);
        _table.CheckCreate(3, 1, 1, 4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public void Get_MissingTexture_Throws(int id)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _table.Get(id));

        Assert.Equal($"Texture {id} does not exist.", exception.Message);
    }

    [Fact]
    public void GetHandles_ReturnsLiveHandlesAndKeepsThem()
    {
        _table.Add(0, 10, 1, 1);
        _table.Add(5, 50, 1, 1);
        _table.Add(2, 20, 1, 1);
        _table.Remove(2);

        Assert.Equal([10, 50], _table.GetHandles());
        Assert.Equal(2, _table.Count);
    }
}
