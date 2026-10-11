using System.Security;
using BlockBlast.Application;
using BlockBlast.Core;
using BlockBlast.WinForms.Audio;
using BlockBlast.WinForms.Controls;
using BlockBlast.WinForms.Dialogs;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms;

/// <summary>Coordinates window lifecycle, the application controller and optional audio.</summary>
public sealed class MainForm : Form
{
    private readonly GameSessionController _session;
    private readonly IGameAudio _audio;
    private readonly GameSurface _surface;
    private readonly FlowLayoutPanel _toolbar;
    private readonly Font _buttonFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly ToolTip _tips = new();
    private readonly System.Windows.Forms.Timer _autosave = new() { Interval = 30_000 };
    private readonly PillButton _newGame, _undo, _hint, _pause;
    private int _dialogDepth;
    private bool _closing, _resourcesDisposed;

    /// <summary>Injected session/audio ownership transfers to this form.</summary>
    public MainForm(GameSessionController? session = null, IGameAudio? audio = null)
    {
        _session = session ?? CreateSession();
        _audio = audio ?? new GameAudioService();
        Text = "Block Blast · Sticker Studio";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1060, 710);
        MinimumSize = new Size(720, 650);
        KeyPreview = true;
        _surface = new GameSurface(_session) { Dock = DockStyle.Fill, TabIndex = 7 };
        _surface.MoveAttempted += OnMoveAttempted;
        _surface.NewGameRequested += StartNewGame;
        _surface.ResumeRequested += TogglePause;
        _toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(18, 12, 18, 10), Width = ClientSize.Width
        };
        _newGame = AddButton("Ván mới", 106, "R · Bắt đầu ván mới", StartNewGame);
        _undo = AddButton("Hoàn tác · 3", 120, "Ctrl+Z · Tối đa 3 lần mỗi ván", Undo);
        _hint = AddButton("Gợi ý", 86, "H · Gợi ý một vị trí đặt hợp lệ", Hint);
        _pause = AddButton("Tạm dừng", 112, "P · Tạm dừng hoặc tiếp tục", TogglePause);
        AddButton("Tùy chỉnh", 112, "Theme, sticker, hiệu ứng và âm thanh", OpenSettings);
        AddButton("Thống kê", 104, "Thành tích chơi tích lũy", OpenStatistics);
        AddButton("Cách chơi", 104, "F1 · Luật chơi và phím tắt", OpenHelp);
        Controls.Add(_surface);
        Controls.Add(_toolbar);
        _autosave.Tick += (_, _) => { _session.Save(); _surface.RefreshSession(); };
        Shown += (_, _) =>
        {
            FitWorkingArea();
            _surface.Focus();
            _autosave.Start();
            if (_session.SaveNotice is { Length: > 0 } notice) _surface.ShowNotice(notice);
            else if (_session.HasRestoredGame)
                _surface.ShowNotice(_session.IsPaused ? "Đã khôi phục ván đang chơi. Bấm P để tiếp tục." : "Chào mừng trở lại! Ván của bạn đã được khôi phục.");
        };
        Deactivate += (_, _) =>
        {
            if (!_closing && _dialogDepth == 0) SetPaused(true);
        };
        Resize += (_, _) =>
        {
            if (!_closing && WindowState == FormWindowState.Minimized) SetPaused(true);
        };
        ApplyPresentation();
    }

    private static GameSessionController CreateSession()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localData) && Path.IsPathFullyQualified(localData))
        {
            try { return new GameSessionController(new JsonGameRepository(Path.Combine(localData, "BlockBlast"))); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or ArgumentException) { }
        }
        return new GameSessionController(new InMemoryGameRepository());
    }

    private PillButton AddButton(string text, int width, string tooltip, Action action)
    {
        var button = new PillButton
        {
            Text = text, Width = width, Height = 38, Font = _buttonFont,
            Margin = new Padding(0, 0, 8, 4), TabIndex = _toolbar.Controls.Count, AccessibleName = text
        };
        button.Click += (_, _) => { action(); _surface.Focus(); };
        _tips.SetToolTip(button, tooltip);
        _toolbar.Controls.Add(button);
        return button;
    }

    private void ApplyPresentation()
    {
        var preferences = _session.Settings;
        var theme = GameTheme.Get(preferences.Theme);
        BackColor = _toolbar.BackColor = theme.Background;
        foreach (PillButton button in _toolbar.Controls) button.SetPalette(theme, button == _newGame);
        _audio.Configure(preferences.MusicEnabled, preferences.SoundEnabled, preferences.Volume);
        _audio.SetPaused(_session.IsPaused || WindowState == FormWindowState.Minimized);
        _surface.ApplySettings();
        RefreshCommands();
    }

    private void OnMoveAttempted(MoveResult result)
    {
        if (!result.Success) _audio.Play(AudioCue.Invalid);
        else if (result.IsGameOver) _audio.Play(AudioCue.GameOver);
        else if (result.LinesCleared > 0) _audio.Play(result.Combo > 1 ? AudioCue.Combo : AudioCue.Clear, result.Combo);
        else _audio.Play(AudioCue.Place);
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        _undo.Text = $"Hoàn tác · {_session.Engine.UndosRemaining}";
        _undo.Enabled = !_session.IsPaused && _session.Engine.CanUndo;
        _hint.Enabled = !_session.IsPaused && !_session.Engine.IsGameOver;
        _pause.Text = _session.IsPaused ? "Tiếp tục" : "Tạm dừng";
        _surface.RefreshSession();
    }

    private void SetPaused(bool paused)
    {
        _session.SetPaused(paused);
        _audio.SetPaused(paused || WindowState == FormWindowState.Minimized);
        if (paused) _surface.OnPaused();
        RefreshCommands();
    }

    private void TogglePause()
    {
        SetPaused(!_session.IsPaused);
        if (!_session.IsPaused) _audio.Play(AudioCue.Click);
    }

    private void StartNewGame()
    {
        if (_session.Engine.PiecesPlaced > 0 && !_session.Engine.IsGameOver)
        {
            var answer = DuringPause(() => MessageBox.Show(this,
                "Bắt đầu ván mới? Ván hiện tại sẽ được thay bằng ván mới; kỷ lục và thống kê vẫn được giữ.",
                "Ván mới", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2));
            if (answer != DialogResult.Yes) return;
        }
        _session.NewGame();
        _surface.ResetInteraction();
        _audio.SetPaused(false);
        _audio.Play(AudioCue.NewGame);
        RefreshCommands();
    }

    private void Undo()
    {
        if (!_session.Undo())
        {
            _surface.ShowNotice(_session.IsPaused ? "Tiếp tục ván chơi trước khi hoàn tác." : "Không còn lượt hoàn tác trong ván này.");
            return;
        }
        _surface.ResetInteraction($"Đã hoàn tác · còn {_session.Engine.UndosRemaining} lượt trong ván này.");
        _audio.Play(AudioCue.Undo);
        RefreshCommands();
    }

    private void Hint()
    {
        if (_session.IsPaused) { _surface.ShowNotice("Bấm P để tiếp tục rồi nhận gợi ý."); return; }
        _surface.ShowHint(_session.Hint());
        _audio.Play(AudioCue.Click);
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsDialog(_session.Settings);
        if (DuringPause(() => dialog.ShowDialog(this)) != DialogResult.OK) return;
        _session.UpdateSettings(dialog.GetSettings());
        ApplyPresentation();
        _surface.ShowNotice("Đã lưu giao diện và âm thanh theo ý bạn.");
        _audio.Play(AudioCue.Click);
    }

    private void OpenStatistics()
    {
        _session.Save();
        var stats = _session.Statistics;
        using var dialog = new InformationDialog("Thành tích của bạn", "Mỗi lượt chơi là một chút tiến bộ.",
        [
            ("KỶ LỤC", $"{stats.BestScore:N0} điểm"),
            ("COMBO TỐT NHẤT", $"×{stats.BestCombo}"),
            ("ĐÃ XẾP", $"{stats.TotalPiecesPlaced:N0} khối · {stats.TotalLinesCleared:N0} hàng/cột"),
            ("VÁN CHƠI", $"{stats.GamesPlayed:N0} đã bắt đầu · {stats.GamesCompleted:N0} đã kết thúc"),
            ("THỜI GIAN THƯ GIÃN", $"{stats.TotalPlaySeconds / 3600:N0} giờ {(stats.TotalPlaySeconds % 3600) / 60} phút {stats.TotalPlaySeconds % 60} giây"),
            ("TIẾN ĐỘ HIỆN TẠI", $"Cấp {_session.Engine.Level} · {_session.Engine.PiecesPlaced} lượt đặt · {_session.Engine.UndosRemaining} lượt hoàn tác"),
            ("CÁCH TÍNH THỐNG KÊ", "Lượt đặt thành công và kỷ lục vẫn được ghi nhận khi hoàn tác. Thời gian tạm dừng không được tính.")
        ], GameTheme.Get(_session.Settings.Theme));
        DuringPause(() => dialog.ShowDialog(this));
    }

    private void OpenHelp()
    {
        using var dialog = new InformationDialog("Một chút hướng dẫn", "Không đồng hồ đếm ngược. Cứ chơi theo nhịp của bạn.",
        [
            ("XẾP & XÓA", "Kéo khối từ khay vào bảng 8 × 8. Hàng hoặc cột đầy sẽ biến mất; dùng hết 3 khối để nhận bộ mới. Khối giữ nguyên hướng."),
            ("ĐIỂM & COMBO", "Mỗi ô đặt được 5 điểm. Xóa nhiều hàng/cột cùng lúc có thưởng lớn hơn. Xóa ở các lượt liên tiếp để nối combo; đặt không xóa sẽ ngắt combo."),
            ("GỢI Ý & HOÀN TÁC", "H cho biết một vị trí hợp lệ. Ctrl+Z quay lại lượt trước, tối đa 3 lần mỗi ván; có thể dùng khi hết lượt. Khối còn lại có thể được mở chỗ sau khi xóa hàng."),
            ("CHƠI BẰNG BÀN PHÍM", "1–3 chọn khối · mũi tên chọn ô · Enter/Space đặt · Esc hủy chọn · P tạm dừng · R ván mới · F1 hướng dẫn. Esc khi chưa chọn khối cũng tạm dừng."),
            ("GIỮ LẠI VÁN ĐANG CHƠI", "Ván chơi tự lưu sau mỗi lượt. Đóng rồi mở game sẽ khôi phục bàn, khay, điểm và lượt hoàn tác. Khi chuyển sang cửa sổ khác, game tự tạm dừng."),
            ("KHÔNG GIAN CỦA RIÊNG BẠN", "Trong Tùy chỉnh, chọn 4 theme và 3 bộ sticker. Có thể tắt riêng nhạc, âm thanh, hiệu ứng và sticker; kéo thanh âm lượng theo ý thích.")
        ], GameTheme.Get(_session.Settings.Theme));
        DuringPause(() => dialog.ShowDialog(this));
    }

    private DialogResult DuringPause(Func<DialogResult> show)
    {
        bool wasPaused = _session.IsPaused;
        _dialogDepth++;
        SetPaused(true);
        try { return show(); }
        finally
        {
            _dialogDepth--;
            if (!wasPaused && !_closing && WindowState != FormWindowState.Minimized) SetPaused(false);
        }
    }

    private void FitWorkingArea()
    {
        var work = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width - 24), Math.Min(MinimumSize.Height, work.Height - 24));
        Size = new Size(Math.Min(Width, work.Width - 24), Math.Min(Height, work.Height - 24));
        Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (_dialogDepth > 0) return base.ProcessCmdKey(ref message, keyData);
        if (keyData == (Keys.Control | Keys.Z)) { Undo(); return true; }
        if (keyData == Keys.H) { Hint(); return true; }
        if (keyData == Keys.R) { StartNewGame(); return true; }
        if (keyData == Keys.F1) { OpenHelp(); return true; }
        if (_surface.HandleGameKey(keyData)) return true;
        if (keyData is Keys.P or Keys.Escape) { TogglePause(); return true; }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        _autosave.Stop();
        SetPaused(true);
        if (!_session.Save() && e.CloseReason == CloseReason.UserClosing &&
            MessageBox.Show(this, "Chưa ghi được tiến độ xuống máy. Thoát lúc này có thể mất những lượt chưa lưu. Bạn vẫn muốn thoát?",
                "Chưa lưu được ván chơi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            e.Cancel = true;
            _closing = false;
            _autosave.Start();
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _autosave.Dispose(); _tips.Dispose(); _audio.Dispose(); _session.Dispose();
        }
        base.Dispose(disposing);
        if (disposing) _buttonFont.Dispose();
    }
}
