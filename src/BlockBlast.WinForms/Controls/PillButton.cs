using System.Drawing.Drawing2D;
using BlockBlast.WinForms.Rendering;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Controls;

internal sealed class PillButton : Button
{
    private GameTheme _theme = GameTheme.All[0];
    private bool _primary, _hovered, _pressed;

    public PillButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Height = 38;
    }

    public void SetPalette(GameTheme theme, bool primary = false)
    {
        _theme = theme; _primary = primary;
        BackColor = theme.Background;
        ForeColor = primary ? theme.Background : theme.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? _theme.Background);
        var rect = new RectangleF(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
        var color = _primary ? _theme.Accent : _theme.Panel;
        if (!Enabled) color = UiDrawing.Mix(color, _theme.Background, .65f);
        else if (_pressed) color = UiDrawing.Mix(color, _theme.Background, .22f);
        else if (_hovered) color = UiDrawing.Mix(color, Color.White, .10f);
        UiDrawing.Fill(g, rect, 11 * DeviceDpi / 96f, color);
        if (Focused) UiDrawing.Outline(g, RectangleF.Inflate(rect, -2, -2), 9 * DeviceDpi / 96f, _theme.Accent, 1.5f);
        UiDrawing.Text(g, Text, Font, Enabled ? ForeColor : _theme.Muted, RectangleF.Inflate(rect, -5, -2));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = _pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _pressed = e.Button == MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}
