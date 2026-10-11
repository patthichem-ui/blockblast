using System.Drawing.Drawing2D;
using BlockBlast.Application;
using BlockBlast.WinForms.Rendering;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Dialogs;

internal sealed class AppearancePreview : Control
{
    private PlayerSettings _settings = new();
    private readonly BlockRenderer _blocks = new();

    public AppearancePreview()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 104;
        AccessibleName = "Xem trước màu nền và bộ sticker";
    }

    public void UpdatePreview(PlayerSettings settings) { _settings = settings with { }; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 0 || Height <= 0) return;
        var theme = GameTheme.Get(_settings.Theme);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new LinearGradientBrush(ClientRectangle, theme.Background, theme.BackgroundEnd, 25f);
        g.FillRectangle(brush, ClientRectangle);
        string[] identities = ["single-r0", "line-3-r0", "el-4-r1", "tee-4-r0", "square-2-r0", "corner-5-r2"];
        float size = Math.Max(10, Math.Min(64 * DeviceDpi / 96f, (Width - 40f) / identities.Length - 9));
        float left = (Width - identities.Length * (size + 9) + 9) / 2;
        for (int i = 0; i < identities.Length; i++)
            _blocks.Draw(g, new RectangleF(left + i * (size + 9), (Height - size) / 2, size, size), identities[i], _settings);
    }
}
