using System.Drawing.Drawing2D;

namespace BlockBlast.WinForms.Rendering;

internal static class UiDrawing
{
    public static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(0.1f, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void Fill(Graphics g, RectangleF rect, float radius, Color color)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = Rounded(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void Outline(Graphics g, RectangleF rect, float radius, Color color, float width = 1)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = Rounded(rect, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void Text(Graphics g, string text, Font font, Color color, RectangleF rect,
        StringAlignment alignment = StringAlignment.Center)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = alignment, LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap
        };
        float height = font.GetHeight(g);
        using var fitted = height > rect.Height
            ? new Font(font.FontFamily, Math.Max(1, font.Size * rect.Height / height * .93f), font.Style)
            : null;
        g.DrawString(text, fitted ?? font, brush, rect, format);
    }

    public static Color Mix(Color first, Color second, float amount) => Color.FromArgb(
        (int)(first.R + (second.R - first.R) * amount),
        (int)(first.G + (second.G - first.G) * amount),
        (int)(first.B + (second.B - first.B) * amount));

    public static PointF[] Star(PointF center, float outer, float inner, int points = 5, float rotation = -MathF.PI / 2)
        => Enumerable.Range(0, points * 2).Select(index =>
        {
            float angle = rotation + index * MathF.PI / points;
            float radius = index % 2 == 0 ? outer : inner;
            return new PointF(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);
        }).ToArray();
}
