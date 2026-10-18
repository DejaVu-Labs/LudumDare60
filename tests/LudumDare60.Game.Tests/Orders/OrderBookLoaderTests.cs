using System.Text;
using LudumDare60.Game.Orders;

namespace LudumDare60.Game.Tests.Orders;

public sealed class OrderBookLoaderTests
{
    private const string ValidOrder = """
        {
          "id": "hoodie",
          "customer": { "look": "hoodie", "description": "Young person in a hoodie" },
          "symptom": "jammed-cylinder",
          "parts": ["base-pin", "cylinder"],
          "dirty": [{ "part": "cylinder", "kind": "rust" }],
          "find": { "id": "rolled-note", "part": "cylinder", "text": "A note." },
          "message": "Thanks!",
          "targetSeconds": 90
        }
        """;

    private static OrderBook Parse(string json) => OrderBookLoader.Parse("data/orders.json", Encoding.UTF8.GetBytes(json));

    private static string Book(params string[] orders) => $$"""{ "orders": [{{string.Join(",", orders)}}] }""";

    /// <summary>
    /// <see cref="ValidOrder"/> with one property replaced, added (when it is not there) or removed (with a null
    /// value).
    /// </summary>
    private static string OrderWith(string property, string? json)
    {
        var lines = ValidOrder.Split('\n').Where(line => !line.TrimStart().StartsWith($"\"{property}\":", StringComparison.Ordinal)).ToList();
        if (json is not null)
        {
            lines.Insert(1, $"  \"{property}\": {json},");
        }

        return string.Join('\n', lines);
    }

    private static string ProblemsOf(string json) => Assert.Throws<InvalidDataException>(() => Parse(json)).Message;

    [Fact]
    public void Parse_ValidOrder_ReadsEveryField()
    {
        var order = Assert.Single(Parse(Book(ValidOrder)).Orders);

        Assert.Equal("hoodie", order.Id);
        Assert.Equal("hoodie", order.Customer.Look);
        Assert.Equal(Symptom.JammedCylinder, order.Symptom);
        Assert.Equal([RevolverParts.BasePin, RevolverParts.Cylinder], order.Parts);
        var dirt = Assert.Single(order.Dirty);
        Assert.Equal(RevolverParts.Cylinder, dirt.Part);
        Assert.Equal(DirtKind.Rust, dirt.Kind);
        Assert.Equal(0.9f, dirt.Threshold);
        Assert.Empty(order.Broken);
        Assert.Empty(order.Oil);
        Assert.Equal("rolled-note", order.Find.Id);
        Assert.Equal("A note.", order.Find.Text);
        Assert.Equal(90, order.TargetSeconds);
    }

    [Fact]
    public void Parse_FindWithoutText_HasNoText()
    {
        var order = Assert.Single(Parse(Book(OrderWith("find", """{ "id": "photo", "part": "cylinder" }"""))).Orders);

        Assert.Null(order.Find.Text);
    }

    [Fact]
    public void Parse_CommentsAndTrailingCommas_AreAllowed()
    {
        var book = Parse($$"""
            {
              // Customers in the order they come in.
              "orders": [{{ValidOrder}},],
            }
            """);

        Assert.Single(book.Orders);
    }

    [Theory]
    [InlineData("loose-grip", Symptom.LooseGrip)]
    [InlineData("broken-spring", Symptom.BrokenSpring)]
    [InlineData("stiff-action", Symptom.StiffAction)]
    [InlineData("everything", Symptom.Everything)]
    public void Parse_Symptom_ReadsKebabCaseNames(string name, Symptom expected)
        => Assert.Equal(expected, Parse(Book(OrderWith("symptom", $"\"{name}\""))).Orders[0].Symptom);

    [Theory]
    [InlineData("\"JammedCylinder\"")]
    [InlineData("\"jammed\"")]
    [InlineData("0")]
    public void Parse_SymptomNotAKebabCaseName_Throws(string json)
        => ProblemsOf(Book(OrderWith("symptom", json)));

    [Fact]
    public void Parse_UnknownProperty_ThrowsNamingIt()
        => Assert.Contains("colour", ProblemsOf(Book(OrderWith("colour", "\"red\""))), StringComparison.Ordinal);

    [Fact]
    public void Parse_MissingRequiredProperty_Throws()
        => Assert.Contains("message", ProblemsOf(Book(OrderWith("message", null))), StringComparison.Ordinal);

    [Fact]
    public void Parse_NullWhereNoneIsAllowed_Throws()
        => ProblemsOf(Book(OrderWith("message", "null")));

    [Fact]
    public void Parse_NotJson_ThrowsWithThePath()
        => Assert.StartsWith("data/orders.json:", ProblemsOf("{ orders: "), StringComparison.Ordinal);

    [Fact]
    public void Parse_NullDocument_Throws()
        => ProblemsOf("null");

    [Fact]
    public void Validate_NoOrders_IsAProblem()
        => Assert.Contains("no orders", ProblemsOf(Book()), StringComparison.Ordinal);

    [Fact]
    public void Validate_SameIdTwice_IsAProblem()
        => Assert.Contains("'hoodie' is used twice", ProblemsOf(Book(ValidOrder, ValidOrder)), StringComparison.Ordinal);

    [Theory]
    [InlineData("\"Hoodie\"")]
    [InlineData("\"hoodie one\"")]
    [InlineData("\"-hoodie\"")]
    [InlineData("\"\"")]
    public void Validate_IdNotKebabCase_IsAProblem(string id)
        => Assert.Contains("kebab-case", ProblemsOf(Book(OrderWith("id", id))), StringComparison.Ordinal);

    [Fact]
    public void Validate_UnknownPart_IsAProblem()
        => Assert.Contains("'trigger' is not a revolver part", ProblemsOf(Book(OrderWith("parts", """["base-pin", "cylinder", "trigger"]"""))), StringComparison.Ordinal);

    [Fact]
    public void Validate_PartTwice_IsAProblem()
        => Assert.Contains("lists 'base-pin' twice", ProblemsOf(Book(OrderWith("parts", """["base-pin", "cylinder", "base-pin"]"""))), StringComparison.Ordinal);

    [Fact]
    public void Validate_NoParts_IsAProblem()
        => Assert.Contains("unlocks no parts", ProblemsOf(Book(OrderWith("parts", "[]"))), StringComparison.Ordinal);

    [Fact]
    public void Validate_BlockedPartWithItsBlockerFixed_IsAProblem()
    {
        var problems = ProblemsOf(Book(OrderWith("parts", """["cylinder"]""")));

        Assert.Contains("'cylinder' cannot come off, because 'base-pin' blocks it", problems, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DirtyPartNotUnlocked_IsAProblem()
        => Assert.Contains("dirty part 'hammer'", ProblemsOf(Book(OrderWith("dirty", """[{ "part": "hammer", "kind": "rust" }]"""))), StringComparison.Ordinal);

    [Theory]
    [InlineData("0")]
    [InlineData("1.5")]
    public void Validate_ThresholdOutOfRange_IsAProblem(string threshold)
    {
        var problems = ProblemsOf(Book(OrderWith("dirty", $$"""[{ "part": "cylinder", "kind": "rust", "threshold": {{threshold}} }]""")));

        Assert.Contains("threshold of 'cylinder'", problems, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BrokenOrOiledPartNotUnlocked_IsAProblem()
    {
        var json = OrderWith("broken", """["mainspring"]""").Replace("\"oil\"", "\"x\"", StringComparison.Ordinal);
        json = json.Replace("\"broken\"", "\"oil\": [\"hammer\"],\n  \"broken\"", StringComparison.Ordinal);

        var problems = ProblemsOf(Book(json));

        Assert.Contains("broken part 'mainspring'", problems, StringComparison.Ordinal);
        Assert.Contains("oiled part 'hammer'", problems, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_FindOnAFixedPart_IsAProblem()
        => Assert.Contains("the find's part 'hammer'", ProblemsOf(Book(OrderWith("find", """{ "id": "note", "part": "hammer" }"""))), StringComparison.Ordinal);

    [Fact]
    public void Validate_FindIdNotKebabCase_IsAProblem()
        => Assert.Contains("find id", ProblemsOf(Book(OrderWith("find", """{ "id": "Note", "part": "cylinder" }"""))), StringComparison.Ordinal);

    [Fact]
    public void Validate_EmptyMessageAndZeroTarget_AreBothReported()
    {
        var json = OrderWith("message", "\" \"").Replace("\"targetSeconds\": 90", "\"targetSeconds\": 0", StringComparison.Ordinal);

        var problems = ProblemsOf(Book(json));

        Assert.Contains("no phone message", problems, StringComparison.Ordinal);
        Assert.Contains("target time", problems, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedOrders_ParseAndValidate()
    {
        var book = new OrderBookLoader().Load(AssetPaths.Orders, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", AssetPaths.Orders)));

        Assert.Equal(["hoodie", "robe", "dog-walker", "headphones", "raincoat"], book.Orders.Select(order => order.Id), StringComparer.Ordinal);
    }

    [Fact]
    public void ShippedOrders_FirstOrderIsTheFtueOrder()
    {
        var book = new OrderBookLoader().Load(AssetPaths.Orders, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", AssetPaths.Orders)));
        var first = book.Orders[0];

        Assert.Equal([RevolverParts.BasePin, RevolverParts.Cylinder], first.Parts);
        Assert.Equal(RevolverParts.Cylinder, Assert.Single(first.Dirty).Part);
        Assert.Equal(RevolverParts.Cylinder, first.Find.Part);
        Assert.Equal(Symptom.JammedCylinder, first.Symptom);
    }

    [Fact]
    public void ShippedOrders_LastOrderUnlocksTheWholeRevolver()
    {
        var book = new OrderBookLoader().Load(AssetPaths.Orders, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", AssetPaths.Orders)));

        Assert.Equal(RevolverParts.All.Order(StringComparer.Ordinal), book.Orders[^1].Parts.Order(StringComparer.Ordinal));
    }
}
