using BlockBlast.Application;

namespace BlockBlast.WinForms.Rendering;

/// <summary>Resolution-independent original stickers: faces, constellations and blossoms.</summary>
internal sealed class StickerRenderer
{
    public void Draw(Graphics g, RectangleF cell, StickerPack pack, uint variant, int alpha = 255)
    {
        if (cell.Width < 14) return;
        var state = g.Save();
        g.TranslateTransform(cell.Left, cell.Top);
        g.ScaleTransform(cell.Width, cell.Height);
        using var ink = new Pen(Color.FromArgb(alpha, 54, 42, 77), 0.038f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        using var white = new SolidBrush(Color.FromArgb(alpha, 255, 249, 239));
        using var dark = new SolidBrush(ink.Color);
        using var blush = new SolidBrush(Color.FromArgb(Math.Min(alpha, 185), 255, 120, 164));
        switch (pack)
        {
            case StickerPack.Cosmic:
                if (variant % 3 == 0)
                {
                    g.FillEllipse(white, .25f, .23f, .51f, .51f);
                    using var crescent = new SolidBrush(Color.FromArgb(alpha, 142, 111, 210));
                    g.FillEllipse(crescent, .41f, .18f, .41f, .42f);
                    g.FillPolygon(white, UiDrawing.Star(new PointF(.71f, .7f), .09f, .035f, 4));
                }
                else
                {
                    g.FillPolygon(white, UiDrawing.Star(new PointF(.5f, .49f), .31f, .17f));
                    g.FillEllipse(dark, .4f, .45f, .045f, .075f);
                    g.FillEllipse(dark, .55f, .45f, .045f, .075f);
                    g.DrawArc(ink, .46f, .48f, .08f, .1f, 0, 180);
                }
                break;
            case StickerPack.Garden:
                using (var petals = new SolidBrush(Color.FromArgb(alpha, variant % 2 == 0 ? Color.FromArgb(255, 245, 218) : Color.FromArgb(255, 207, 232))))
                {
                    for (int index = 0; index < 5; index++)
                    {
                        float angle = index * MathF.PI * 2 / 5;
                        g.FillEllipse(petals, .365f + .16f * MathF.Cos(angle), .35f + .16f * MathF.Sin(angle), .27f, .27f);
                    }
                    using var center = new SolidBrush(Color.FromArgb(alpha, 255, 203, 95));
                    g.FillEllipse(center, .35f, .335f, .3f, .3f);
                    g.FillEllipse(dark, .415f, .43f, .035f, .055f);
                    g.FillEllipse(dark, .55f, .43f, .035f, .055f);
                    g.DrawArc(ink, .46f, .46f, .08f, .07f, 0, 180);
                }
                break;
            default:
                if (variant % 3 == 0)
                {
                    g.FillPolygon(white, [new PointF(.25f, .35f), new PointF(.26f, .13f), new PointF(.43f, .3f)]);
                    g.FillPolygon(white, [new PointF(.58f, .3f), new PointF(.75f, .13f), new PointF(.77f, .36f)]);
                }
                else if (variant % 3 == 1)
                {
                    g.FillEllipse(white, .28f, .08f, .15f, .35f);
                    g.FillEllipse(white, .56f, .08f, .15f, .35f);
                }
                g.FillEllipse(white, .2f, .26f, .6f, .51f);
                g.FillEllipse(blush, .255f, .53f, .125f, .07f);
                g.FillEllipse(blush, .625f, .53f, .125f, .07f);
                g.FillEllipse(dark, .36f, .43f, .055f, .09f);
                g.FillEllipse(dark, .585f, .43f, .055f, .09f);
                g.DrawArc(ink, .445f, .49f, .11f, .1f, 0, 180);
                break;
        }
        g.Restore(state);
    }
}
