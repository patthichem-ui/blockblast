using BlockBlast.WinForms.Controls;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Dialogs;

internal sealed class InformationDialog : Form
{
    private readonly Font _heading = new("Segoe UI", 20, FontStyle.Bold);
    private readonly Font _body = new("Segoe UI", 10);
    private readonly Font _label = new("Segoe UI", 9, FontStyle.Bold);

    public InformationDialog(string title, string subtitle, IReadOnlyList<(string Title, string Detail)> sections, GameTheme theme)
    {
        Text = title;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = _body;
        ClientSize = new Size(620, 650);
        MinimumSize = new Size(470, 420);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = MinimizeBox = false;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        var header = new Panel { Dock = DockStyle.Top, Width = ClientSize.Width, Height = 94 };
        header.Controls.Add(new Label { Text = title.ToUpperInvariant(), Font = _heading, Location = new Point(24, 14),
            Size = new Size(570, 42), AutoEllipsis = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top });
        header.Controls.Add(new Label { Text = subtitle, Font = _body, ForeColor = theme.Muted, Location = new Point(27, 59),
            Size = new Size(566, 28), AutoEllipsis = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top });
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 0, 24, 8) };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, GrowStyle = TableLayoutPanelGrowStyle.AddRows };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var section in sections)
        {
            var card = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
                BackColor = theme.Panel, Padding = new Padding(14, 12, 14, 14), Margin = new Padding(0, 0, 0, 10) };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(new Label { Text = section.Title, Font = _label, ForeColor = theme.Accent,
                AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) });
            var detail = new Label { Text = section.Detail, Font = _body, ForeColor = theme.Text,
                AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0) };
            card.Controls.Add(detail);
            card.SizeChanged += (_, _) => detail.MaximumSize = new Size(Math.Max(180, card.ClientSize.Width - card.Padding.Horizontal), 0);
            content.Controls.Add(card);
        }
        scroll.Controls.Add(content);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(24, 8, 24, 12) };
        var close = new PillButton { Text = "Quay lại chơi", DialogResult = DialogResult.OK, Width = 150, Font = _body };
        close.SetPalette(theme, true);
        footer.Controls.Add(close);
        AcceptButton = CancelButton = close;
        Controls.Add(scroll); Controls.Add(header); Controls.Add(footer);
        Shown += (_, _) =>
        {
            var work = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width - 30), Math.Min(MinimumSize.Height, work.Height - 30));
            Size = new Size(Math.Min(Width, work.Width - 30), Math.Min(Height, work.Height - 30));
            Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { _heading.Dispose(); _body.Dispose(); _label.Dispose(); }
    }
}
