using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Rendering;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests;

public sealed class PlaceholderPartsTests
{
    private static readonly Image s_sheet = PlaceholderParts.Create();

    private static bool IsOpaque(IntRect sprite, int x, int y)
        => s_sheet.Pixels[((((sprite.Y + y) * s_sheet.Width) + sprite.X + x) * Image.BytesPerPixel) + 3] != 0;

    /// <summary>
    /// Asserts that every drawn pixel of <paramref name="sprite"/> takes the part.
    /// </summary>
    private static void AssertCoversSprite(GrabShape shape, IntRect sprite, Vector2 origin)
    {
        var drawn = 0;
        for (var y = 0; y < sprite.Height; y++)
        {
            for (var x = 0; x < sprite.Width; x++)
            {
                if (IsOpaque(sprite, x, y))
                {
                    drawn++;
                    Assert.True(shape.Contains(new Vector2(x + 0.5f, y + 0.5f) - origin), $"Pixel ({x}, {y}) does not take the part.");
                }
            }
        }

        Assert.True(drawn > 0);
    }

    [Fact]
    public void CreatePinGrab_CoversHeadAndRod()
        => AssertCoversSprite(PlaceholderParts.CreatePinGrab(), PlaceholderParts.Pin, PlaceholderParts.PinOrigin);

    [Fact]
    public void CreateCylinderGrab_CoversTheCylinderInBothTurns()
    {
        AssertCoversSprite(PlaceholderParts.CreateCylinderGrab(), PlaceholderParts.Cylinder, PlaceholderParts.CylinderOrigin);
        AssertCoversSprite(PlaceholderParts.CreateCylinderGrab(), PlaceholderParts.CylinderTurned, PlaceholderParts.CylinderOrigin);
    }

    [Fact]
    public void CreateHammerGrab_CoversTheWholeHammer()
        => AssertCoversSprite(PlaceholderParts.CreateHammerGrab(), PlaceholderParts.Hammer, PlaceholderParts.HammerOrigin);

    [Fact]
    public void CreateTriggerGrab_CoversTheWholeTrigger()
        => AssertCoversSprite(PlaceholderParts.CreateTriggerGrab(), PlaceholderParts.Trigger, PlaceholderParts.TriggerOrigin);

    [Fact]
    public void CreateCockGrab_CoversTheSpurButNotTheHammerScrew()
    {
        var grab = PlaceholderParts.CreateCockGrab();
        var screwReach = (PlaceholderParts.Screw.Width / 2f) + 2;

        Assert.True(grab.Contains(PlaceholderParts.HammerSpur));
        for (var y = -screwReach; y <= screwReach; y++)
        {
            for (var x = -screwReach; x <= screwReach; x++)
            {
                Assert.False(grab.Contains(new Vector2(x, y)), $"({x}, {y}) from the pivot takes the hammer, not the screw.");
            }
        }
    }

    [Fact]
    public void CreateScrewGrab_CoversTheWholeHead()
        => AssertCoversSprite(PlaceholderParts.CreateScrewGrab(), PlaceholderParts.Screw, PlaceholderParts.ScrewOrigin);

    [Fact]
    public void CreateBodyCover_CoversEveryDrawnPixelOfTheRevolverWhereItLies()
    {
        var at = new Vector2(170, 186);
        var cover = PlaceholderParts.CreateBodyCover(at);

        for (var y = 0; y < PlaceholderParts.Body.Height; y++)
        {
            for (var x = 0; x < PlaceholderParts.Body.Width; x++)
            {
                Assert.Equal(IsOpaque(PlaceholderParts.Body, x, y), cover.Contains(at + new Vector2(x + 0.5f, y + 0.5f)));
            }
        }
    }

    [Fact]
    public void CreateGripPanelGrab_CoversTheWholePanel()
        => AssertCoversSprite(PlaceholderParts.CreateGripPanelGrab(), PlaceholderParts.GripPanel, PlaceholderParts.GripPanelOrigin);

    [Fact]
    public void CreateGripPanelGrab_OutsideThePanel_DoesNotTakeIt()
    {
        var shape = PlaceholderParts.CreateGripPanelGrab();

        Assert.False(shape.Contains(new Vector2(-2, -2) - PlaceholderParts.GripPanelOrigin));
        Assert.False(shape.Contains(new Vector2(PlaceholderParts.GripPanel.Width + 2, 0) - PlaceholderParts.GripPanelOrigin));
    }

    [Fact]
    public void CreateMainspringGrab_CoversTheWholeSpring()
        => AssertCoversSprite(PlaceholderParts.CreateMainspringGrab(), PlaceholderParts.Mainspring, PlaceholderParts.MainspringOrigin);

    [Fact]
    public void CreateMainspringGrab_CoversTheBrokenSpring()
        => AssertCoversSprite(PlaceholderParts.CreateMainspringGrab(), PlaceholderParts.MainspringBroken, PlaceholderParts.MainspringOrigin);

    [Fact]
    public void CreateMainspringGrab_BesideTheSpring_DoesNotTakeIt()
    {
        var shape = PlaceholderParts.CreateMainspringGrab();

        // The spring slants down to the right: the bottom-left and top-right corners are well clear of it.
        Assert.False(shape.Contains(new Vector2(1, PlaceholderParts.Mainspring.Height - 1) - PlaceholderParts.MainspringOrigin));
        Assert.False(shape.Contains(new Vector2(PlaceholderParts.Mainspring.Width - 1, 1) - PlaceholderParts.MainspringOrigin));
    }

    [Fact]
    public void Pixel_IsOpaqueWhite()
    {
        var sheet = PlaceholderParts.Create();
        var pixel = PlaceholderParts.Pixel;
        for (var y = pixel.Y; y < pixel.Bottom; y++)
        {
            for (var x = pixel.X; x < pixel.Right; x++)
            {
                var at = ((y * sheet.Width) + x) * Image.BytesPerPixel;
                Assert.Equal([255, 255, 255, 255], sheet.Pixels.Slice(at, 4).ToArray());
            }
        }
    }

    [Fact]
    public void PressAnyKey_HasWhiteLettersAndClearGaps()
    {
        var sheet = PlaceholderParts.Create();
        var text = PlaceholderParts.PressAnyKey;
        var lit = 0;
        for (var y = 0; y < text.Height; y++)
        {
            for (var x = 0; x < text.Width; x++)
            {
                var at = ((((text.Y + y) * sheet.Width) + text.X + x) * Image.BytesPerPixel) + 3;
                lit += sheet.Pixels[at] != 0 ? 1 : 0;
                if (x % 4 == 3)
                {
                    Assert.Equal(0, sheet.Pixels[at]);
                }
            }
        }

        Assert.InRange(lit, text.Width, text.Width * text.Height / 2);
    }
}
