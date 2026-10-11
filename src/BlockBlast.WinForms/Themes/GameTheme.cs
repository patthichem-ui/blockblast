using BlockBlast.Application;

namespace BlockBlast.WinForms.Themes;

/// <summary>Immutable palette shared by the canvas and native dialogs.</summary>
internal sealed record GameTheme(ThemeId Id, string Name, Color Background, Color BackgroundEnd,
    Color Panel, Color Board, Color EmptyCell, Color Accent, Color Secondary, Color Text, Color Muted)
{
    public static IReadOnlyList<GameTheme> All { get; } =
    [
        new(ThemeId.Aurora, "Cực quang", C(17, 21, 48), C(34, 27, 65), C(36, 40, 74), C(20, 26, 53), C(43, 49, 82), C(126, 242, 207), C(187, 158, 255), C(245, 246, 255), C(167, 178, 208)),
        new(ThemeId.Candy, "Kẹo ngọt", C(53, 28, 58), C(90, 44, 71), C(91, 51, 83), C(55, 32, 62), C(91, 56, 92), C(255, 180, 215), C(255, 218, 148), C(255, 244, 250), C(220, 181, 207)),
        new(ThemeId.Ocean, "Đại dương", C(12, 38, 57), C(19, 74, 86), C(25, 65, 82), C(12, 39, 59), C(32, 71, 92), C(121, 230, 248), C(132, 247, 202), C(239, 254, 255), C(160, 204, 218)),
        new(ThemeId.Sunset, "Hoàng hôn", C(47, 28, 48), C(92, 47, 56), C(86, 47, 63), C(52, 31, 47), C(91, 53, 66), C(255, 197, 124), C(255, 143, 163), C(255, 248, 237), C(225, 186, 179))
    ];

    public static GameTheme Get(ThemeId id) => All.FirstOrDefault(theme => theme.Id == id) ?? All[0];
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);
}
