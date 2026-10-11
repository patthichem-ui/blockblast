using System.Drawing.Drawing2D;
using BlockBlast.WinForms.Rendering;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Controls;

internal sealed class ToggleSwitch : CheckBox
{
    private GameTheme _theme = GameTheme.All[0];

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(56, 32);
        Cursor = Cursors.Hand;
        AutoSize = false;
    }

    public void SetPalette(GameTheme theme) { _theme = theme; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? _theme.Panel);
        float height = Math.Min(Height - 6, Width * .49f);
        var track = new RectangleF(2, (Height - height) / 2, Width - 4, height);
        UiDrawing.Fill(g, track, height / 2, Checked ? _theme.Accent : _theme.EmptyCell);
        float diameter = height - 6;
        float x = Checked ? track.Right - diameter - 3 : track.Left + 3;
        using var knob = new SolidBrush(Checked ? _theme.Background : _theme.Muted);
        g.FillEllipse(knob, x, track.Top + 3, diameter, diameter);
        if (Focused) UiDrawing.Outline(g, RectangleF.Inflate(track, 1, 1), height / 2, _theme.Secondary);
    }

    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
