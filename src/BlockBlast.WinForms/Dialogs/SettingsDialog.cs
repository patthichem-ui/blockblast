using BlockBlast.Application;
using BlockBlast.WinForms.Controls;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Dialogs;

internal sealed class SettingsDialog : Form
{
    private readonly PlayerSettings _editing;
    private readonly Font _body = new("Segoe UI", 10);
    private readonly Font _heading = new("Segoe UI", 20, FontStyle.Bold);
    private readonly Font _small = new("Segoe UI", 9);
    private readonly AppearancePreview _preview = new();
    private readonly ComboBox _themes = new(), _packs = new();
    private readonly List<ToggleSwitch> _toggles = [];
    private readonly TrackBar _volume = new();
    private readonly Label _volumeValue = new();
    private readonly PillButton _save = new(), _cancel = new();
    private readonly TableLayoutPanel _content;

    public SettingsDialog(PlayerSettings settings)
    {
        _editing = settings with { };
        Text = "Không gian của bạn";
        Font = _body;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(590, 700);
        MinimumSize = new Size(460, 430);
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        var header = new Panel { Dock = DockStyle.Top, Height = 82, Width = ClientSize.Width };
        header.Controls.Add(new Label { Text = "KHÔNG GIAN CỦA BẠN", Font = _heading, AutoSize = false,
            Location = new Point(24, 13), Size = new Size(540, 37), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
        header.Controls.Add(new Label { Text = "Một chút màu sắc. Một giai điệu đúng ý.", Font = _small,
            Location = new Point(27, 52), Size = new Size(530, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 0, 24, 8) };
        _content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 0, 8) };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        AddWide(_preview, 104);
        PrepareCombo(_themes);
        _themes.DisplayMember = nameof(GameTheme.Name);
        _themes.Items.AddRange(GameTheme.All.Cast<object>().ToArray());
        _themes.SelectedItem = GameTheme.Get(_editing.Theme);
        AddChoice("Theme nền", "Đổi không khí cho bàn chơi", _themes);
        PrepareCombo(_packs);
        _packs.Items.AddRange(["Kawaii · mèo & thỏ", "Cosmic · sao & trăng", "Garden · hoa nhỏ"]);
        _packs.SelectedIndex = (int)_editing.StickerPack;
        AddChoice("Bộ sticker", "Những người bạn trên từng khối", _packs);
        AddToggle("Sticker trên khối", "Bật hoặc trở về khối màu đơn giản", _editing.StickersEnabled, value => _editing.StickersEnabled = value);
        AddToggle("Hiệu ứng chuyển động", "Ánh sáng, hạt sao và chúc mừng combo", _editing.EffectsEnabled, value => _editing.EffectsEnabled = value);
        AddToggle("Nhạc nền", "Giai điệu nhẹ nhàng khi chơi", _editing.MusicEnabled, value => _editing.MusicEnabled = value);
        AddToggle("Âm thanh trò chơi", "Tiếng đặt khối, xóa hàng và combo", _editing.SoundEnabled, value => _editing.SoundEnabled = value);
        _volume.Minimum = 0; _volume.Maximum = 100; _volume.Value = _editing.Volume;
        _volume.TickStyle = TickStyle.None; _volume.SmallChange = 5; _volume.LargeChange = 10;
        _volume.Dock = DockStyle.Fill; _volume.AccessibleName = "Âm lượng chung";
        var volumePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 10, 0, 0) };
        volumePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        volumePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        _volumeValue.Dock = DockStyle.Fill; _volumeValue.TextAlign = ContentAlignment.MiddleRight;
        volumePanel.Controls.Add(_volume, 0, 0); volumePanel.Controls.Add(_volumeValue, 1, 0);
        AddChoice("Âm lượng", "Dùng chung cho nhạc và âm thanh", volumePanel);
        scroll.Controls.Add(_content);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 66, Width = ClientSize.Width,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(20, 10, 20, 10) };
        _save.Text = "Lưu tùy chọn"; _save.Size = new Size(148, 38); _save.DialogResult = DialogResult.OK;
        _cancel.Text = "Để sau"; _cancel.Size = new Size(104, 38); _cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(_save); footer.Controls.Add(_cancel);
        AcceptButton = _save; CancelButton = _cancel;
        Controls.Add(scroll); Controls.Add(header); Controls.Add(footer);
        _themes.SelectedIndexChanged += (_, _) => { _editing.Theme = ((GameTheme)_themes.SelectedItem!).Id; ApplyTheme(); };
        _packs.SelectedIndexChanged += (_, _) => { _editing.StickerPack = (StickerPack)_packs.SelectedIndex; RefreshPreview(); };
        _volume.ValueChanged += (_, _) => { _editing.Volume = _volume.Value; RefreshPreview(); };
        Shown += (_, _) => FitToScreen();
        ApplyTheme();
    }

    public PlayerSettings GetSettings() => _editing with { };

    private static void PrepareCombo(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Dock = DockStyle.Fill;
        combo.Margin = new Padding(0, 15, 0, 0);
    }

    private void AddWide(Control control, int height)
    {
        int row = _content.RowCount++;
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        control.Dock = DockStyle.Fill;
        _content.Controls.Add(control, 0, row);
        _content.SetColumnSpan(control, 2);
    }

    private void AddChoice(string title, string detail, Control control)
    {
        int row = _content.RowCount++;
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        var labels = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        labels.Controls.Add(new Label { Text = title, Font = _body, Dock = DockStyle.Top, Height = 29, Padding = new Padding(0, 9, 0, 0) });
        labels.Controls.Add(new Label { Text = detail, Font = _small, Dock = DockStyle.Bottom, Height = 26, AutoEllipsis = true });
        _content.Controls.Add(labels, 0, row);
        _content.Controls.Add(control, 1, row);
    }

    private void AddToggle(string title, string detail, bool enabled, Action<bool> set)
    {
        var toggle = new ToggleSwitch { Checked = enabled, Anchor = AnchorStyles.Right, AccessibleName = title,
            Margin = new Padding(0, 12, 2, 8) };
        toggle.CheckedChanged += (_, _) => { set(toggle.Checked); RefreshPreview(); };
        _toggles.Add(toggle);
        AddChoice(title, detail, toggle);
    }

    private void ApplyTheme()
    {
        var theme = GameTheme.Get(_editing.Theme);
        PaintChildren(this, theme);
        foreach (var toggle in _toggles) toggle.SetPalette(theme);
        _save.SetPalette(theme, true); _cancel.SetPalette(theme);
        RefreshPreview();
    }

    private void PaintChildren(Control control, GameTheme theme)
    {
        control.BackColor = control is ComboBox ? theme.Panel : theme.Background;
        control.ForeColor = control.Font == _small ? theme.Muted : theme.Text;
        foreach (Control child in control.Controls) PaintChildren(child, theme);
    }

    private void RefreshPreview()
    {
        _volumeValue.Text = $"{_editing.Volume}%";
        _preview.UpdatePreview(_editing);
    }

    private void FitToScreen()
    {
        var work = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width - 30), Math.Min(MinimumSize.Height, work.Height - 30));
        Size = new Size(Math.Min(Width, work.Width - 30), Math.Min(Height, work.Height - 30));
        Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { _body.Dispose(); _heading.Dispose(); _small.Dispose(); }
    }
}
