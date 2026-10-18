using System.Numerics;
using System.Runtime.InteropServices;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Game.Customers;
using LudumDare60.Game.Orders;

namespace LudumDare60.Game;

/// <summary>
/// Customers generated in code until the real art exists, in one sheet: a bust per look from the chest up (one base
/// figure, different hair, headwear, clothes and palette), three faces drawn over the head, a hand held out (white,
/// tinted with the customer's skin), the speech bubble and a symptom icon per <see cref="Symptom"/>. Colors come from
/// the palette (docs/gamedesign/art-direction.md).
/// </summary>
internal static class PlaceholderCustomers
{
    public const int Width = 384;
    public const int Height = 128;

    public const int BustWidth = 72;
    public const int BustHeight = 96;

    /// <summary>
    /// The looks the sheet draws, by the names <see cref="Customer.Look"/> uses, in sheet order.
    /// </summary>
    public static readonly IReadOnlyList<string> Looks = ["hoodie", "cardigan", "work-jacket", "headphones", "raincoat"];

    /// <summary>
    /// Where a bust is drawn from: the middle of its bottom edge, which stands on the far edge of the bench.
    /// </summary>
    public static readonly Vector2 BustOrigin = new(BustWidth / 2f, BustHeight);

    /// <summary>
    /// The top-left corner of a face, relative to the bust's top-left corner.
    /// </summary>
    public static readonly Vector2 FaceOffset = new(26, 30);

    /// <summary>
    /// The hand held out, white and gray so a tint gives it the customer's skin.
    /// </summary>
    public static readonly IntRect Hand = new(60, 96, 16, 8);

    public static readonly IntRect Bubble = new(80, 96, BubbleWidth, BubbleHeight);

    /// <summary>
    /// The top-left corner of the symptom icon in the bubble.
    /// </summary>
    public static readonly Vector2 IconInBubble = new(8, 3);

    private const int BubbleWidth = 32;
    private const int BubbleHeight = 28;
    private const int FaceWidth = 20;
    private const int FaceHeight = 14;
    private const int IconSize = 16;
    private const int IconsX = 112;
    private const int SheetRow = 96;

    // The head: an ellipse around this point, in bust pixels.
    private const float HeadX = 36;
    private const float HeadY = 36;
    private const float HeadRadiusX = 12.5f;
    private const float HeadRadiusY = 17;

    private static readonly Color s_ink = Color.FromRgb(0x0d0a0a);
    private static readonly Color s_paper = Color.FromRgb(0xfaebbc);
    private static readonly Color s_steel = Color.FromRgb(0x5f5247);
    private static readonly Color s_steelLight = Color.FromRgb(0xb3a99b);
    private static readonly Color s_brass = Color.FromRgb(0xe2ad5f);
    private static readonly Color s_walnut = Color.FromRgb(0x823d1e);
    private static readonly Color s_rust = Color.FromRgb(0xb37149);
    private static readonly Color s_handLight = new(255, 255, 255);
    private static readonly Color s_handShade = new(205, 205, 205);
    private static readonly Color s_handLine = new(150, 150, 150);

    private static readonly LookStyle[] s_styles =
    [
        // A hood up, in night blue.
        new(Skin: 0xb37149, SkinShade: 0x944f2d, Hair: 0x2b120b, Cloth: 0x2e4a70, ClothShade: 0x243a5a, ClothLight: 0x3c608c, Headwear.Hood),

        // Gray hair in a bun, a warm cardigan.
        new(Skin: 0xe2ad5f, SkinShade: 0xd2834a, Hair: 0xb3a99b, Cloth: 0xa1603b, ClothShade: 0x944f2d, ClothLight: 0xb37149, Headwear.Bun),

        // Short dark hair, a canvas work jacket.
        new(Skin: 0xa1603b, SkinShade: 0x823d1e, Hair: 0x1e0d09, Cloth: 0x5f5247, ClothShade: 0x453a30, ClothLight: 0xb3a99b, Headwear.Short),

        // Headphones over the hair, a blue tee.
        new(Skin: 0xd2834a, SkinShade: 0xb37149, Hair: 0x4c291e, Cloth: 0x3c608c, ClothShade: 0x2e4a70, ClothLight: 0x5479a1, Headwear.Headphones),

        // Long hair, a teal raincoat wet from the street.
        new(Skin: 0xd2834a, SkinShade: 0xa1603b, Hair: 0x622a14, Cloth: 0x165a4a, ClothShade: 0x0b4f45, ClothLight: 0x306d59, Headwear.Long),
    ];

    private enum Headwear
    {
        Hood,
        Bun,
        Short,
        Headphones,
        Long,
    }

    /// <summary>
    /// Whether the sheet has a bust for <paramref name="look"/>.
    /// </summary>
    public static bool HasLook(string look) => LookIndex(look) >= 0;

    /// <summary>
    /// The bust of <paramref name="look"/>, or of the first look when the sheet has none for it.
    /// </summary>
    public static IntRect Bust(string look) => new(Math.Max(0, LookIndex(look)) * BustWidth, 0, BustWidth, BustHeight);

    /// <summary>
    /// The skin color of <paramref name="look"/>, to tint <see cref="Hand"/> with.
    /// </summary>
    public static Color Skin(string look) => Color.FromRgb(s_styles[Math.Max(0, LookIndex(look))].Skin);

    public static IntRect Face(Expression expression) => new((int)expression * FaceWidth, SheetRow, FaceWidth, FaceHeight);

    public static IntRect Icon(Symptom symptom) => new(IconsX + ((int)symptom * IconSize), SheetRow, IconSize, IconSize);

    public static Image Create()
    {
        var pixels = new byte[Width * Height * Image.BytesPerPixel];
        for (var look = 0; look < s_styles.Length; look++)
        {
            DrawBust(pixels, look * BustWidth, s_styles[look]);
        }

        foreach (var expression in Enum.GetValues<Expression>())
        {
            var face = Face(expression);
            DrawFace(pixels, face.X, face.Y, expression);
        }

        DrawHand(pixels);
        DrawBubble(pixels);
        foreach (var symptom in Enum.GetValues<Symptom>())
        {
            var icon = Icon(symptom);
            DrawIcon(pixels, icon.X, icon.Y, symptom);
        }

        return new Image(Width, Height, pixels);
    }

    private static int LookIndex(string look)
    {
        for (var i = 0; i < Looks.Count; i++)
        {
            if (string.Equals(Looks[i], look, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool InEllipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
    {
        var dx = (x + 0.5f - centerX) / radiusX;
        var dy = (y + 0.5f - centerY) / radiusY;
        return (dx * dx) + (dy * dy) <= 1;
    }

    private static bool InHead(int x, int y) => InEllipse(x, y, HeadX, HeadY, HeadRadiusX, HeadRadiusY);

    private static void DrawBust(byte[] pixels, int left, LookStyle style)
    {
        var skin = Color.FromRgb(style.Skin);
        var skinShade = Color.FromRgb(style.SkinShade);
        var hair = Color.FromRgb(style.Hair);
        var cloth = Color.FromRgb(style.Cloth);
        var clothShade = Color.FromRgb(style.ClothShade);
        var clothLight = Color.FromRgb(style.ClothLight);
        for (var y = 0; y < BustHeight; y++)
        {
            for (var x = 0; x < BustWidth; x++)
            {
                var color = BustColor(x, y, style.Headwear, skin, skinShade, hair, cloth, clothShade, clothLight);
                if (color.A != 0)
                {
                    Set(pixels, left + x, y, color);
                }
            }
        }
    }

    private static Color BustColor(int x, int y, Headwear headwear, Color skin, Color skinShade, Color hair, Color cloth, Color clothShade, Color clothLight)
    {
        // Shoulders and chest: they widen from the neck down to the bottom edge. The lamp is on the left.
        if (y >= 62)
        {
            var halfWidth = Math.Min(35f, 12 + ((y - 62) * 2.2f));
            var dx = x + 0.5f - HeadX;
            if (MathF.Abs(dx) <= halfWidth)
            {
                // A collar opening down the middle shows the shirt or the skin.
                if (y < 74 && MathF.Abs(dx) < 4 - ((y - 62) / 4f))
                {
                    return headwear == Headwear.Hood ? clothShade : skinShade;
                }

                return dx < -halfWidth + 3 ? clothLight : dx > halfWidth - 6 ? clothShade : cloth;
            }

            return Color.Transparent;
        }

        // Behind the head: a hood, or long hair falling to the shoulders.
        var inHead = InHead(x, y);
        if (!inHead)
        {
            if (headwear == Headwear.Hood && InEllipse(x, y, HeadX, HeadY - 1, HeadRadiusX + 5, HeadRadiusY + 5))
            {
                return x < HeadX - 8 ? clothLight : cloth;
            }

            if (headwear == Headwear.Long && y > 24 && InEllipse(x, y, HeadX, HeadY + 6, HeadRadiusX + 4, HeadRadiusY + 6))
            {
                return hair;
            }

            if (headwear == Headwear.Bun && InEllipse(x, y, HeadX, 17, 6, 5))
            {
                return hair;
            }

            if (headwear == Headwear.Headphones)
            {
                var cup = (x is >= 20 and <= 24 || x is >= 48 and <= 52) && y is >= 33 and <= 42;
                var band = InEllipse(x, y, HeadX, HeadY - 1, HeadRadiusX + 3, HeadRadiusY + 3) && y < 30;
                if (cup || band)
                {
                    return s_ink;
                }
            }

            // The neck.
            return x is >= 31 and <= 41 && y is >= 50 and < 62 ? skinShade : Color.Transparent;
        }

        // On the head: hair over the top, a hood's rim around the face, then skin, shaded on the side away from the
        // lamp.
        var hairLine = headwear switch
        {
            Headwear.Hood => 25,
            Headwear.Short => 26,
            _ => 27,
        };
        if (y < hairLine || (headwear == Headwear.Long && y < 40 && (x < 26 || x > 46)))
        {
            return headwear == Headwear.Hood ? cloth : hair;
        }

        if (headwear == Headwear.Headphones && y < hairLine + 1 && x is > 28 and < 44)
        {
            return s_ink;
        }

        return x > HeadX + 6 ? skinShade : skin;
    }

    private static void DrawFace(byte[] pixels, int left, int top, Expression expression)
    {
        // Eyes 10 px apart, the Mouth below them, in face pixels.
        const int LeftEye = 5;
        const int RightEye = 14;
        const int Eyes = 5;
        const int Mouth = 11;
        switch (expression)
        {
            case Expression.Surprised:
                Rect(pixels, left + LeftEye - 1, top + Eyes - 1, 2, 2, s_ink);
                Rect(pixels, left + RightEye - 1, top + Eyes - 1, 2, 2, s_ink);
                Rect(pixels, left + 9, top + Mouth - 1, 2, 3, s_ink);
                break;
            case Expression.Thankful:
                // Eyes closed in a smile, and a smile.
                foreach (var eye in (ReadOnlySpan<int>)[LeftEye, RightEye])
                {
                    Set(pixels, left + eye - 1, top + Eyes, s_ink);
                    Set(pixels, left + eye, top + Eyes - 1, s_ink);
                    Set(pixels, left + eye + 1, top + Eyes, s_ink);
                }

                Rect(pixels, left + 8, top + Mouth + 1, 4, 1, s_ink);
                Set(pixels, left + 7, top + Mouth, s_ink);
                Set(pixels, left + 12, top + Mouth, s_ink);
                break;
            default:
                Rect(pixels, left + LeftEye, top + Eyes - 1, 1, 2, s_ink);
                Rect(pixels, left + RightEye, top + Eyes - 1, 1, 2, s_ink);
                Rect(pixels, left + 8, top + Mouth, 4, 1, s_ink);
                break;
        }
    }

    private static void DrawHand(byte[] pixels)
    {
        // An open palm seen from above, fingers to the left, the thumb up.
        Rect(pixels, Hand.X + 1, Hand.Y + 2, 14, 5, s_handLight);
        Rect(pixels, Hand.X + 1, Hand.Y + 6, 14, 1, s_handShade);
        Rect(pixels, Hand.X + 9, Hand.Y, 3, 2, s_handLight);
        for (var finger = 0; finger < 3; finger++)
        {
            Rect(pixels, Hand.X + 1, Hand.Y + 3 + finger, 5, 1, finger % 2 == 0 ? s_handLight : s_handLine);
        }

        Rect(pixels, Hand.X + 15, Hand.Y + 2, 1, 5, s_handLine);
    }

    private static void DrawBubble(byte[] pixels)
    {
        // A rounded box with a tail pointing down to the customer on the left.
        for (var y = 0; y < 22; y++)
        {
            for (var x = 0; x < Bubble.Width; x++)
            {
                var corner = x is 0 or BubbleWidth - 1 && y is 0 or 21;
                if (corner)
                {
                    continue;
                }

                var edge = x is 0 or BubbleWidth - 1 || y is 0 or 21;
                Set(pixels, Bubble.X + x, Bubble.Y + y, edge ? s_ink : s_paper);
            }
        }

        for (var y = 21; y < Bubble.Height; y++)
        {
            var width = Bubble.Height - y;
            for (var x = 0; x < width; x++)
            {
                var edge = x == 0 || x == width - 1 || y == Bubble.Height - 1;
                Set(pixels, Bubble.X + 4 + x, Bubble.Y + y, edge && y > 21 ? s_ink : s_paper);
            }
        }
    }

    private static void DrawIcon(byte[] pixels, int left, int top, Symptom symptom)
    {
        switch (symptom)
        {
            case Symptom.JammedCylinder:
                // The cylinder from the side, and a padlock.
                Rect(pixels, left + 1, top + 5, 9, 7, s_steelLight);
                Rect(pixels, left + 1, top + 7, 9, 1, s_steel);
                Rect(pixels, left + 1, top + 10, 9, 1, s_steel);
                Rect(pixels, left + 11, top + 8, 5, 5, s_brass);
                Rect(pixels, left + 12, top + 5, 1, 3, s_ink);
                Rect(pixels, left + 14, top + 5, 1, 3, s_ink);
                Rect(pixels, left + 12, top + 5, 3, 1, s_ink);
                Set(pixels, left + 13, top + 10, s_ink);
                break;
            case Symptom.LooseGrip:
                // The grip, tilted, with a wobble on each side.
                for (var y = 2; y < 14; y++)
                {
                    Rect(pixels, left + 5 + (y / 4), top + y, 5, 1, s_walnut);
                }

                Rect(pixels, left + 1, top + 5, 1, 4, s_ink);
                Rect(pixels, left + 14, top + 7, 1, 4, s_ink);
                break;
            case Symptom.BrokenSpring:
                // A zigzag spring with a gap in the middle.
                for (var x = 0; x < 16; x++)
                {
                    if (x is 7 or 8)
                    {
                        continue;
                    }

                    var step = x % 2 * 3;
                    var y = 8 + (x % 4 < 2 ? -step : step);
                    Rect(pixels, left + x, top + y - 1, 1, 2, s_steel);
                }

                break;
            case Symptom.StiffAction:
                // The hammer's spur with rust spots.
                Rect(pixels, left + 4, top + 4, 4, 10, s_steelLight);
                Rect(pixels, left + 2, top + 2, 4, 3, s_steelLight);
                Set(pixels, left + 5, top + 7, s_rust);
                Set(pixels, left + 6, top + 10, s_rust);
                Set(pixels, left + 4, top + 12, s_rust);
                Rect(pixels, left + 10, top + 4, 1, 3, s_ink);
                Rect(pixels, left + 12, top + 6, 1, 3, s_ink);
                break;
            default:
                // Everything: a question mark.
                Rect(pixels, left + 5, top + 2, 6, 2, s_ink);
                Rect(pixels, left + 10, top + 3, 2, 4, s_ink);
                Rect(pixels, left + 7, top + 7, 3, 2, s_ink);
                Rect(pixels, left + 7, top + 9, 2, 2, s_ink);
                Rect(pixels, left + 7, top + 12, 2, 2, s_ink);
                break;
        }
    }

    private static void Rect(byte[] pixels, int left, int top, int width, int height, Color color)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                Set(pixels, x, y, color);
            }
        }
    }

    private static void Set(byte[] pixels, int x, int y, Color color)
    {
        var i = ((y * Width) + x) * Image.BytesPerPixel;
        pixels[i] = color.R;
        pixels[i + 1] = color.G;
        pixels[i + 2] = color.B;
        pixels[i + 3] = color.A;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct LookStyle(uint Skin, uint SkinShade, uint Hair, uint Cloth, uint ClothShade, uint ClothLight, Headwear Headwear);
}
