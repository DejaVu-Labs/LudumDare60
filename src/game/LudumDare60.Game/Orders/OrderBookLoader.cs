using System.Text.Json;
using LudumDare60.Engine.Assets;

namespace LudumDare60.Game.Orders;

/// <summary>
/// Parses and validates <c>orders.json</c>. Register it with <c>assets.Register(new OrderBookLoader())</c>.
/// </summary>
public sealed class OrderBookLoader : IAssetLoader<OrderBook>
{
    /// <exception cref="InvalidDataException">The file is not valid JSON, does not match the schema, or breaks a rule
    /// of <see cref="OrderBook.Validate"/>. The message names every problem.</exception>
    public OrderBook Load(string path, byte[] data) => Parse(path, data);

    public void Unload(OrderBook asset)
    {
    }

    /// <inheritdoc cref="Load"/>
    public static OrderBook Parse(string path, ReadOnlySpan<byte> json)
    {
        OrderBook? book;
        try
        {
            book = JsonSerializer.Deserialize(json, OrderJsonContext.Default.OrderBook);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path}: {exception.Message}", exception);
        }

        if (book is null)
        {
            throw new InvalidDataException($"{path} holds null instead of an order book.");
        }

        var problems = book.Validate();
        return problems.Count == 0 ? book : throw new InvalidDataException($"{path}: {string.Join(" ", problems)}");
    }
}
