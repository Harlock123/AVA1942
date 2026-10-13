namespace AVA1942.Views;

/// <summary>
/// The planes, seen from above and pointing up the screen (the drawing code turns them): 16×16, one
/// letter per pixel. 'a' is the plane's colour, 'b' a darker and 'c' a lighter shade of it; the other
/// letters are fixed colours; '.' is clear. All original drawings.
/// </summary>
public static class Sprites
{
    public static readonly Dictionary<char, uint> Fixed = new()
    {
        ['k'] = 0xFF141218, ['w'] = 0xFFF0EEE6, ['r'] = 0xFFD83030, ['y'] = 0xFFF0D040, ['g'] = 0xFF9A9AA0,
        ['u'] = 0xFF60C0F0,
    };

    public static readonly Dictionary<string, string[]> All = new()
    {
        ["player"] =
        [
            ".......aa.......",
            "......aaaa......",
            "..g...auua...g..",
            ".aaa..aaaa..aaa.",
            "aaaaaaaaaaaaaaaa",
            "acaaaaaaaaaaaaca",
            ".aaa..aaaa..aaa.",
            "..a...aaaa...a..",
            "..a....aa....a..",
            "..a..........a..",
            "..a..........a..",
            "..a..........a..",
            ".aaaaaaaaaaaaaa.",
            ".bbbbbbbbbbbbbb.",
            "................",
            "................",
        ],
        ["fighter"] =
        [
            "................",
            ".......gg.......",
            ".......aa.......",
            "......akka......",
            "....aaaaaaaa....",
            "aaaaaaaaaaaaaaaa",
            "ayaaaaaaaaaaaaya",
            "abbbbaaaaaabbbba",
            ".......aa.......",
            ".......aa.......",
            ".......aa.......",
            ".....aaaaaa.....",
            ".....bbbbbb.....",
            "................",
            "................",
            "................",
        ],
        ["bomber"] =
        [
            ".......aa.......",
            "......akka......",
            ".......aa.......",
            "..g....aa....g..",
            ".aaa...aa...aaa.",
            "aaaaaaaaaaaaaaaa",
            "aaaaaaaaaaaaaaaa",
            "bbbbbbbaabbbbbbb",
            ".......aa.......",
            ".......aa.......",
            ".......aa.......",
            ".......aa.......",
            "....aaaaaaaa....",
            "....bbbbbbbb....",
            ".......aa.......",
            "................",
        ],
    };

    public static uint Shade(uint argb, double f)
    {
        uint r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        uint S(uint v) => (uint)Math.Clamp(f >= 1 ? v + (255 - v) * (f - 1) : v * f, 0, 255);
        return (argb & 0xFF000000) | (S(r) << 16) | (S(g) << 8) | S(b);
    }

    public static uint Colour(char ch, uint tint) => ch switch
    {
        '.' => 0,
        'a' => tint,
        'b' => Shade(tint, 0.62),
        'c' => Shade(tint, 1.45),
        _ => Fixed.GetValueOrDefault(ch, 0xFFFF00FF),
    };
}
