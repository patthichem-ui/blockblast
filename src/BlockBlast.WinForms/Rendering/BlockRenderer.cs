using System.Drawing.Drawing2D;
using BlockBlast.Application;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Rendering;

internal sealed class BlockRenderer
{
    private static readonly Color[] Colors =
    [Color.FromArgb(118, 165, 255), Color.FromArgb(255, 194, 111), Color.FromArgb(185, 141, 246),
        Color.FromArgb(106, 217, 176), Color.FromArgb(252, 145, 177), Color.FromArgb(106, 210, 235)];
    private readonly StickerRenderer _stickers = new();

    public static uint Identity(string? shapeId)
    {
        uint hash = 2166136261;
        foreach (char character in shapeId ?? "single-r0") hash = unchecked((hash ^ character) * 16777619);
        return hash;
    }

    public static Color ColorFor(string? shapeId) => Colors[Identity(shapeId) % (uint)Colors.Length];

    public void Draw(Graphics g, RectangleF rect, string? shapeId, PlayerSettings settings, int alpha = 255)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        float radius = rect.Width * .22f;
        var color = ColorFor(shapeId);
        var shadow = rect;
        shadow.Offset(0, Math.Max(1, rect.Height * .065f));
        UiDrawing.Fill(g, shadow, radius, Color.FromArgb(alpha * 2 / 3, UiDrawing.Mix(color, Color.Black, .48f)));
        using var path = UiDrawing.Rounded(rect, radius);
        using var gradient = new LinearGradientBrush(rect,
            Color.FromArgb(alpha, UiDrawing.Mix(color, Color.White, .27f)),
            Color.FromArgb(alpha, color), LinearGradientMode.Vertical);
        g.FillPath(gradient, path);
        UiDrawing.Outline(g, RectangleF.Inflate(rect, -.7f, -.7f), radius,
            Color.FromArgb(alpha / 3, Color.White), Math.Max(.7f, rect.Width * .024f));
        using var shine = new Pen(Color.FromArgb(alpha / 2, Color.White), Math.Max(1, rect.Width * .035f));
        g.DrawLine(shine, rect.Left + radius, rect.Top + rect.Height * .09f,
            rect.Right - radius, rect.Top + rect.Height * .09f);
        if (settings.StickersEnabled)
            _stickers.Draw(g, RectangleF.Inflate(rect, -rect.Width * .065f, -rect.Height * .065f),
                settings.StickerPack, Identity(shapeId), alpha);
    }

    public static void Empty(Graphics g, RectangleF rect, GameTheme theme)
    {
        UiDrawing.Fill(g, rect, rect.Width * .2f, theme.EmptyCell);
        UiDrawing.Outline(g, rect, rect.Width * .2f, Color.FromArgb(14, Color.White));
    }
}
