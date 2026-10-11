using System.Drawing.Drawing2D;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Rendering;

internal sealed class BackgroundRenderer
{
    public void Draw(Graphics g, Size size, GameTheme theme, double time, float scale)
    {
        if (size.Width <= 0 || size.Height <= 0) return;
        using var background = new LinearGradientBrush(new Rectangle(Point.Empty, size),
            theme.Background, theme.BackgroundEnd, 45f);
        g.FillRectangle(background, new Rectangle(Point.Empty, size));
        for (int i = 0; i < 13; i++)
        {
            float x = (float)((i * .371 + .11) % 1) * size.Width;
            float y = (float)((i * .239 + .16) % 1) * size.Height + (float)Math.Sin(time * .28 + i) * 9 * scale;
            float radius = (10 + i % 4 * 7) * scale;
            using var pen = new Pen(Color.FromArgb(18, i % 2 == 0 ? theme.Accent : theme.Secondary), 1.5f * scale);
            if (i % 3 == 0) g.DrawPolygon(pen, UiDrawing.Star(new PointF(x, y), radius, radius * .5f, 4, (float)(i + time * .025)));
            else g.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
        }
        // Soft corner blooms use nested translucent shapes rather than bitmap assets.
        for (int i = 5; i > 0; i--)
        {
            using var bloom = new SolidBrush(Color.FromArgb(3, theme.Accent));
            g.FillEllipse(bloom, -170 * scale, -150 * scale, (150 + i * 45) * scale, (150 + i * 45) * scale);
            using var second = new SolidBrush(Color.FromArgb(3, theme.Secondary));
            g.FillEllipse(second, size.Width - (60 + i * 40) * scale,
                size.Height - (90 + i * 40) * scale, (120 + i * 60) * scale, (120 + i * 60) * scale);
        }
    }
}
