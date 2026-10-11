using System.Diagnostics;
using BlockBlast.Application;
using BlockBlast.Core;
using BlockBlast.WinForms.Effects;
using BlockBlast.WinForms.Rendering;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms;

/// <summary>Interactive canvas. Rule changes and persistence always go through the application controller.</summary>
internal sealed partial class GameSurface : Control
{
    private readonly GameSessionController _session;
    private readonly GameLayout _layout = new();
    private readonly BlockRenderer _blocks = new();
    private readonly BackgroundRenderer _background = new();
    private readonly VisualEffects _effects = new();
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = 20 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Font _titleFont = new("Segoe UI", 25, FontStyle.Bold);
    private readonly Font _scoreFont = new("Segoe UI", 34, FontStyle.Bold);
    private readonly Font _headingFont = new("Segoe UI", 13, FontStyle.Bold);
    private readonly Font _bodyFont = new("Segoe UI", 10);
    private readonly Font _smallFont = new("Segoe UI", 8.5f);
    private readonly Font _numberFont = new("Segoe UI", 21, FontStyle.Bold);
    private PlayerSettings _settings;
    private GameTheme _theme;
    private double _lastFrame;
    private double _ambientTime;
    private string _status = "Kéo khối vào bảng hoặc chọn khối rồi bấm để đặt.";
    private double _statusUntil;
    private int _selectedIndex = -1;
    private bool _pressedTray, _dragging, _keyboardMode;
    private Point _pointer, _pressPoint;
    private Cell _grabbedCell, _keyboardCell;
    private Cell? _preview;

    public event Action<MoveResult>? MoveAttempted;
    public event Action? NewGameRequested;
    public event Action? ResumeRequested;

    public GameSurface(GameSessionController session)
    {
        _session = session;
        _settings = session.Settings;
        _theme = GameTheme.Get(_settings.Theme);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleName = "Bảng Block Blast 8 nhân 8";
        AccessibleRole = AccessibleRole.Pane;
        AccessibleDescription = "1 đến 3 chọn khối; mũi tên chọn ô; Enter đặt; H gợi ý; Ctrl Z hoàn tác; P tạm dừng.";
        _frameTimer.Tick += (_, _) => TickFrame();
        _frameTimer.Start();
    }

    private GameEngine Engine => _session.Engine;
    private BlockShape? SelectedShape => _selectedIndex >= 0 ? Engine.Tray[_selectedIndex] : null;
    private void UpdateLayout() => _layout.Update(ClientSize, DeviceDpi / 96f, Engine.Tray);

    public void ApplySettings()
    {
        _settings = _session.Settings;
        _theme = GameTheme.Get(_settings.Theme);
        BackColor = _theme.Background;
        if (!_settings.EffectsEnabled) _effects.Clear();
        UpdateAnimationState();
        Invalidate();
    }

    public void RefreshSession()
    {
        UpdateAnimationState();
        Invalidate();
    }

    private void UpdateAnimationState()
    {
        _lastFrame = _clock.Elapsed.TotalSeconds;
        _frameTimer.Enabled = _settings.EffectsEnabled && !_session.IsPaused && Visible;
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (_session is not null) UpdateAnimationState();
    }

    private void TickFrame()
    {
        double now = _clock.Elapsed.TotalSeconds;
        float elapsed = (float)Math.Clamp(now - _lastFrame, 0, .08);
        _lastFrame = now;
        _ambientTime += elapsed;
        _effects.Update(elapsed);
        Invalidate();
    }

    public void ShowNotice(string text)
    {
        _status = text;
        _statusUntil = _clock.Elapsed.TotalSeconds + 8;
        Invalidate();
    }

    public void ResetInteraction(string? notice = null)
    {
        ClearSelection();
        _effects.Clear();
        ShowNotice(notice ?? "Ván mới, cảm hứng mới. Chúc bạn phá kỷ lục!");
        RefreshSession();
    }

    public void OnPaused()
    {
        ClearSelection();
        RefreshSession();
    }

    public void ShowHint(PlannedPlacement? hint)
    {
        if (_session.IsPaused) return;
        if (hint is not { } placement)
        {
            ShowNotice("Không còn vị trí hợp lệ cho khay hiện tại.");
            return;
        }
        _selectedIndex = placement.TrayIndex;
        _keyboardCell = new Cell(placement.Row, placement.Column);
        _preview = _keyboardCell;
        _keyboardMode = true;
        _pressedTray = _dragging = false;
        Capture = false;
        ShowNotice("Gợi ý đã sáng lên trên bảng. Bấm Enter để đặt hoặc chọn vị trí khác.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frameTimer.Dispose();
            _effects.Dispose();
            _titleFont.Dispose(); _scoreFont.Dispose(); _headingFont.Dispose();
            _bodyFont.Dispose(); _smallFont.Dispose(); _numberFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
