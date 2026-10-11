using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using BlockBlast.Core;

namespace BlockBlast.WinForms;

/// <summary>
/// All board coordinates are derived from one pitch. Tray cells can be smaller;
/// dragging preserves the grabbed cell as the anchor and previews at board scale.
/// </summary>
internal sealed class GameSurface : Control
{
    internal static readonly Color BackgroundColor = Color.FromArgb(12, 20, 34);
    internal static readonly Color MutedColor = Color.FromArgb(145, 165, 187);
    private static readonly Color TextColor = Color.FromArgb(235, 244, 253);
    private static readonly Color AccentColor = Color.FromArgb(107, 239, 207);
    private static readonly Color[] PieceColors =
    [
        Color.FromArgb(90, 174, 248), Color.FromArgb(244, 187, 76),
        Color.FromArgb(170, 131, 245), Color.FromArgb(105, 213, 173),
        Color.FromArgb(248, 136, 157), Color.FromArgb(104, 204, 229)
    ];
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("vi-VN");

    private readonly GameEngine _engine;
    private readonly Font _scoreFont = new("Segoe UI", 24f, FontStyle.Bold);
    private readonly Font _headingFont = new("Segoe UI", 12f, FontStyle.Bold);
    private readonly Font _bodyFont = new("Segoe UI", 10f);
    private readonly Font _smallFont = new("Segoe UI", 9f);
    private readonly Font _overlayFont = new("Segoe UI", 22f, FontStyle.Bold);
    private readonly RectangleF[] _trayCards = new RectangleF[3];
    private readonly RectangleF[] _trayShapes = new RectangleF[3];
    private readonly float[] _trayPitches = new float[3];
    private RectangleF _board;
    private RectangleF _replayButton;
    private float _pitch;
    private int _selectedIndex = -1;
    private bool _pressedTray;
    private bool _dragging;
    private bool _keyboardMode;
    private Point _pointer;
    private Point _pressPoint;
    private Cell _grabbedCell;
    private Cell _keyboardCell;
    private Cell? _preview;
    private string _status = "Kéo khối vào bảng, hoặc chọn khối rồi bấm vị trí muốn đặt.";
    private bool _storageNotice;
    private long _bestScore;

    public event Action<MoveResult>? MoveCompleted;
    public event Action? NewGameRequested;

    public GameSurface(GameEngine engine)
    {
        _engine = engine;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.Selectable, true);
        BackColor = BackgroundColor;
        TabStop = true;
        AccessibleName = "Bảng Block Blast 8 nhân 8";
        AccessibleDescription = "Phím 1 đến 3 chọn khối, phím mũi tên chọn vị trí, Enter đặt khối, Escape hủy.";
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long BestScore
    {
        get => _bestScore;
        set { _bestScore = value; Invalidate(); }
    }

    private float UiScale => DeviceDpi / 96f;
    private BlockShape? SelectedShape => _selectedIndex >= 0 ? _engine.Tray[_selectedIndex] : null;

    public void ShowStorageNotice()
    {
        _storageNotice = true;
        Invalidate();
    }

    public void ResetInteraction()
    {
        ClearSelection();
        _status = "Kéo khối vào bảng, hoặc chọn khối rồi bấm vị trí muốn đặt.";
        Invalidate();
    }

    private void ComputeLayout()
    {
        var scale = UiScale;
        var boardTop = 122 * scale;
        var boardSize = Math.Min(Math.Min(Width - 64 * scale,
            Height - boardTop - 192 * scale), 510 * scale);
        boardSize = Math.Max(8 * scale, boardSize);
        _board = new RectangleF((Width - boardSize) / 2, boardTop, boardSize, boardSize);
        _pitch = boardSize / Board.Size;

        var trayWidth = Math.Min(Width - 48 * scale, Math.Max(boardSize + 64 * scale, 440 * scale));
        var gap = 14 * scale;
        var cardWidth = (trayWidth - 2 * gap) / 3;
        var cardTop = _board.Bottom + 40 * scale;
        var cardHeight = 105 * scale;
        for (var index = 0; index < 3; index++)
        {
            _trayCards[index] = new RectangleF((Width - trayWidth) / 2 + index * (cardWidth + gap),
                cardTop, cardWidth, cardHeight);
            var shape = _engine.Tray[index];
            if (shape is null)
            {
                _trayShapes[index] = RectangleF.Empty;
                _trayPitches[index] = 0;
                continue;
            }

            var piecePitch = Math.Min(_pitch * 0.75f, Math.Min(
                (cardWidth - 28 * scale) / shape.Width, (cardHeight - 32 * scale) / shape.Height));
            _trayPitches[index] = piecePitch;
            _trayShapes[index] = new RectangleF(
                _trayCards[index].Left + (cardWidth - shape.Width * piecePitch) / 2,
                cardTop + 14 * scale + (cardHeight - 28 * scale - shape.Height * piecePitch) / 2,
                shape.Width * piecePitch, shape.Height * piecePitch);
        }

        _replayButton = new RectangleF(_board.Left + _board.Width * 0.18f,
            _board.Top + _board.Height * 0.61f, _board.Width * 0.64f, 43 * scale);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        ComputeLayout();
        DrawHud(graphics);
        DrawBoard(graphics);
        DrawTray(graphics);

        if (_dragging && _preview is null && SelectedShape is { } floatingShape)
            DrawShape(graphics, floatingShape,
                new PointF(_pointer.X - _grabbedCell.Column * _pitch - _pitch / 2,
                    _pointer.Y - _grabbedCell.Row * _pitch - _pitch / 2),
                _pitch, Color.FromArgb(185, ColorFor(floatingShape)));

        if (_engine.IsGameOver)
            DrawGameOver(graphics);
    }

    private void DrawHud(Graphics graphics)
    {
        var scale = UiScale;
        var gap = 12 * scale;
        var width = (Width - 48 * scale - gap * 2) / 3;
        string[] labels = ["ĐIỂM", "KỶ LỤC", "CẤP ĐỘ"];
        string[] values = [FormatNumber(_engine.Score), FormatNumber(_bestScore), _engine.Level.ToString(NumberCulture)];
        for (var index = 0; index < 3; index++)
        {
            var rect = new RectangleF(24 * scale + index * (width + gap), 4 * scale, width, 78 * scale);
            FillRounded(graphics, rect, 12 * scale, Color.FromArgb(21, 34, 52));
            DrawText(graphics, labels[index], _smallFont, MutedColor,
                new RectangleF(rect.Left + 8 * scale, rect.Top + 7 * scale, rect.Width - 16 * scale, 20 * scale));
            var valueRect = new RectangleF(rect.Left + 8 * scale, rect.Top + 27 * scale,
                rect.Width - 16 * scale, 42 * scale);
            DrawFittedScore(graphics, values[index], valueRect, index == 0 ? AccentColor : TextColor);
        }

        var combo = _engine.Combo > 0 ? $"COMBO ×{_engine.Combo}" : "COMBO —";
        DrawText(graphics, $"{combo}      •      {_engine.PiecesPlaced} lượt đặt      •      {_engine.TotalLinesCleared} hàng/cột đã xóa",
            _bodyFont, _engine.Combo > 0 ? AccentColor : MutedColor,
            new RectangleF(20 * scale, 90 * scale, Width - 40 * scale, 25 * scale));
    }

    private void DrawBoard(Graphics graphics)
    {
        var scale = UiScale;
        var surround = RectangleF.Inflate(_board, 9 * scale, 9 * scale);
        FillRounded(graphics, surround, 14 * scale, Color.FromArgb(17, 30, 46));
        for (var row = 0; row < Board.Size; row++)
        for (var column = 0; column < Board.Size; column++)
        {
            var occupied = _engine.Board.IsOccupied(row, column);
            var color = occupied ? Color.FromArgb(71, 158, 226) : Color.FromArgb(30, 47, 67);
            DrawTile(graphics, CellRect(row, column), color, occupied);
        }

        if (_preview is { } preview && SelectedShape is { } shape)
        {
            var valid = _engine.Board.CanPlace(shape, preview.Row, preview.Column);
            var color = valid ? Color.FromArgb(180, 107, 239, 207) : Color.FromArgb(185, 251, 111, 132);
            var saved = graphics.Save();
            graphics.SetClip(_board);
            foreach (var cell in shape.Cells)
            {
                var rect = CellRect(preview.Row + cell.Row, preview.Column + cell.Column);
                DrawTile(graphics, rect, color, true);
                using var outline = new Pen(valid ? AccentColor : Color.FromArgb(255, 153, 166), 1.7f * scale);
                DrawRounded(graphics, rect, Math.Min(5 * scale, rect.Width / 4), outline);
            }
            graphics.Restore(saved);
        }
    }

    private void DrawTray(Graphics graphics)
    {
        var scale = UiScale;
        DrawText(graphics, "BA KHỐI • DÙNG HẾT ĐỂ NHẬN BỘ MỚI", _smallFont, MutedColor,
            new RectangleF(20 * scale, _board.Bottom + 13 * scale, Width - 40 * scale, 20 * scale));
        for (var index = 0; index < 3; index++)
        {
            var selected = index == _selectedIndex;
            var rect = _trayCards[index];
            FillRounded(graphics, rect, 12 * scale,
                selected ? Color.FromArgb(30, 64, 71) : Color.FromArgb(21, 34, 52));
            if (selected)
            {
                using var outline = new Pen(AccentColor, 2 * scale);
                DrawRounded(graphics, rect, 12 * scale, outline);
            }

            DrawText(graphics, (index + 1).ToString(), _smallFont, selected ? AccentColor : MutedColor,
                new RectangleF(rect.Left + 8 * scale, rect.Top + 4 * scale, 18 * scale, 18 * scale));
            var shape = _engine.Tray[index];
            if (shape is null)
                DrawText(graphics, "Đã đặt", _bodyFont, MutedColor, rect);
            else
                DrawShape(graphics, shape, _trayShapes[index].Location, _trayPitches[index],
                    _dragging && selected ? Color.FromArgb(90, ColorFor(shape)) : ColorFor(shape));
        }

        var status = _engine.IsGameOver ? "Hết vị trí đặt khối. Chơi lại để vượt kỷ lục!" : _status;
        if (_storageNotice)
            status += "  Kỷ lục được giữ trong phiên này.";
        DrawText(graphics, status, _smallFont, MutedColor,
            new RectangleF(18 * scale, _trayCards[0].Bottom + 11 * scale,
                Width - 36 * scale, 25 * scale));
    }

    private void DrawGameOver(Graphics graphics)
    {
        var scale = UiScale;
        FillRounded(graphics, RectangleF.Inflate(_board, 5 * scale, 5 * scale),
            12 * scale, Color.FromArgb(235, 12, 20, 34));
        DrawText(graphics, "HẾT LƯỢT!", _overlayFont, TextColor,
            new RectangleF(_board.Left, _board.Top + _board.Height * 0.22f, _board.Width, 50 * scale));
        DrawText(graphics, $"{FormatNumber(_engine.Score)} điểm", _headingFont, AccentColor,
            new RectangleF(_board.Left, _board.Top + _board.Height * 0.41f, _board.Width, 32 * scale));
        FillRounded(graphics, _replayButton, 10 * scale, AccentColor);
        DrawText(graphics, "Chơi lại  •  R", _headingFont, Color.FromArgb(14, 41, 40), _replayButton);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        Focus();
        ComputeLayout();
        if (_engine.IsGameOver)
        {
            if (_replayButton.Contains(e.Location))
                NewGameRequested?.Invoke();
            return;
        }

        for (var index = 0; index < 3; index++)
        {
            if (!_trayCards[index].Contains(e.Location) || _engine.Tray[index] is not { } shape)
                continue;

            _selectedIndex = index;
            _pressedTray = true;
            _dragging = false;
            _keyboardMode = false;
            _pointer = _pressPoint = e.Location;
            _preview = null;
            _grabbedCell = ClosestShapeCell(shape, index, e.Location);
            Capture = true;
            _status = $"Đã chọn {shape.Name}. Bấm vào bảng hoặc kéo khối để đặt.";
            Invalidate();
            return;
        }

        if (_board.Contains(e.Location) && SelectedShape is not null)
        {
            var target = PointerCell(e.Location);
            PlaceSelected(target);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _pointer = e.Location;
        if (_engine.IsGameOver)
        {
            Cursor = _replayButton.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            return;
        }

        if (_pressedTray && !_dragging)
        {
            var dragSize = SystemInformation.DragSize;
            _dragging = Math.Abs(e.X - _pressPoint.X) > dragSize.Width / 2 ||
                        Math.Abs(e.Y - _pressPoint.Y) > dragSize.Height / 2;
        }

        _keyboardMode = false;
        if (SelectedShape is not null && _board.Contains(e.Location))
        {
            var pointerCell = PointerCell(e.Location);
            _preview = _dragging
                ? new Cell(pointerCell.Row - _grabbedCell.Row, pointerCell.Column - _grabbedCell.Column)
                : pointerCell;
        }
        else
            _preview = null;

        Cursor = SelectedShape is not null || _trayCards.Any(rect => rect.Contains(e.Location))
            ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_pressedTray)
            return;

        if (_dragging && _board.Contains(e.Location))
        {
            var pointerCell = PointerCell(e.Location);
            PlaceSelected(new Cell(pointerCell.Row - _grabbedCell.Row, pointerCell.Column - _grabbedCell.Column));
        }

        _pressedTray = false;
        _dragging = false;
        Capture = false;
        // A following click always uses the shape's bounding-box top-left.
        _preview = SelectedShape is not null && _board.Contains(e.Location) ? PointerCell(e.Location) : null;
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            _pressedTray = false;
            _dragging = false;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_keyboardMode)
            _preview = null;
        Invalidate();
    }

    public bool HandleGameKey(Keys keyData)
    {
        if ((keyData & Keys.Modifiers) != Keys.None)
            return false;
        if (keyData == Keys.Escape)
        {
            ClearSelection();
            _status = "Đã hủy chọn khối.";
            Invalidate();
            return true;
        }
        if (_engine.IsGameOver)
            return false;

        var selection = keyData switch
        {
            Keys.D1 or Keys.NumPad1 => 0,
            Keys.D2 or Keys.NumPad2 => 1,
            Keys.D3 or Keys.NumPad3 => 2,
            _ => -1
        };
        if (selection >= 0)
        {
            if (_engine.Tray[selection] is { } shape)
            {
                ClearSelection();
                _selectedIndex = selection;
                _keyboardCell = FirstFit(shape);
                _keyboardMode = true;
                _preview = _keyboardCell;
                _status = $"{shape.Name}: dùng phím mũi tên chọn vị trí, Enter để đặt.";
                Focus();
                Invalidate();
            }
            return true;
        }

        if (SelectedShape is null)
            return false;
        var delta = keyData switch
        {
            Keys.Left => new Cell(0, -1),
            Keys.Right => new Cell(0, 1),
            Keys.Up => new Cell(-1, 0),
            Keys.Down => new Cell(1, 0),
            _ => new Cell(0, 0)
        };
        if (delta.Row != 0 || delta.Column != 0)
        {
            if (!_keyboardMode)
                _keyboardCell = _preview ?? FirstFit(SelectedShape);
            _keyboardCell = new Cell(Math.Clamp(_keyboardCell.Row + delta.Row, 0, Board.Size - 1),
                Math.Clamp(_keyboardCell.Column + delta.Column, 0, Board.Size - 1));
            _keyboardMode = true;
            _preview = _keyboardCell;
            Invalidate();
            return true;
        }
        if (keyData is Keys.Enter or Keys.Space)
        {
            PlaceSelected(_preview ?? FirstFit(SelectedShape));
            return true;
        }
        return false;
    }

    private void PlaceSelected(Cell target)
    {
        if (SelectedShape is null)
            return;
        var move = _engine.TryPlace(_selectedIndex, target.Row, target.Column);
        if (!move.Success)
        {
            _status = "Khối này chưa vừa ở đây. Thử vị trí khác.";
            _preview = target;
            Invalidate();
            return;
        }

        ClearSelection();
        _status = $"+{FormatNumber(move.PointsEarned)} điểm";
        if (move.LinesCleared > 0)
            _status += $" • Xóa {move.LinesCleared} hàng/cột • Combo ×{move.Combo}";
        if (move.TrayRefilled)
            _status += " • Bộ khối mới đã sẵn sàng";
        MoveCompleted?.Invoke(move);
        AccessibleDescription = $"Điểm {_engine.Score}. Cấp độ {_engine.Level}. {_status} " +
            "Phím 1 đến 3 chọn khối, mũi tên chọn vị trí, Enter đặt khối, Escape hủy.";
        AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1);
        Invalidate();
    }

    private void ClearSelection()
    {
        _selectedIndex = -1;
        _pressedTray = _dragging = _keyboardMode = false;
        _preview = null;
        Capture = false;
        Cursor = Cursors.Default;
    }

    private Cell FirstFit(BlockShape shape)
    {
        for (var row = 0; row < Board.Size; row++)
        for (var column = 0; column < Board.Size; column++)
            if (_engine.Board.CanPlace(shape, row, column))
                return new Cell(row, column);
        return new Cell(0, 0);
    }

    private Cell ClosestShapeCell(BlockShape shape, int index, Point point)
    {
        var shapeRect = _trayShapes[index];
        var pitch = _trayPitches[index];
        return shape.Cells.MinBy(cell =>
        {
            var dx = shapeRect.Left + (cell.Column + 0.5f) * pitch - point.X;
            var dy = shapeRect.Top + (cell.Row + 0.5f) * pitch - point.Y;
            return dx * dx + dy * dy;
        });
    }

    private Cell PointerCell(Point point) => new(
        (int)Math.Floor((point.Y - _board.Top) / _pitch),
        (int)Math.Floor((point.X - _board.Left) / _pitch));

    private RectangleF CellRect(int row, int column)
    {
        var gap = Math.Min(4 * UiScale, _pitch * 0.13f);
        return new RectangleF(_board.Left + column * _pitch + gap / 2,
            _board.Top + row * _pitch + gap / 2, _pitch - gap, _pitch - gap);
    }

    private void DrawShape(Graphics graphics, BlockShape shape, PointF origin, float pitch, Color color)
    {
        var gap = Math.Min(3 * UiScale, pitch * 0.13f);
        foreach (var cell in shape.Cells)
            DrawTile(graphics, new RectangleF(origin.X + cell.Column * pitch + gap / 2,
                origin.Y + cell.Row * pitch + gap / 2, pitch - gap, pitch - gap), color, true);
    }

    private void DrawTile(Graphics graphics, RectangleF rect, Color color, bool filled)
    {
        var radius = Math.Min(5 * UiScale, rect.Width / 4);
        FillRounded(graphics, rect, radius, color);
        if (!filled)
            return;
        using var shine = new Pen(Color.FromArgb(Math.Min(100, (int)color.A), 255, 255, 255), UiScale);
        graphics.DrawLine(shine, rect.Left + radius, rect.Top + 2 * UiScale,
            rect.Right - radius, rect.Top + 2 * UiScale);
    }

    private void DrawFittedScore(Graphics graphics, string text, RectangleF rect, Color color)
    {
        var measured = graphics.MeasureString(text, _scoreFont);
        if (measured.Width <= rect.Width)
        {
            DrawText(graphics, text, _scoreFont, color, rect);
            return;
        }

        using var reduced = new Font(_scoreFont.FontFamily,
            Math.Max(10f, _scoreFont.Size * rect.Width / measured.Width), FontStyle.Bold);
        DrawText(graphics, text, reduced, color, rect);
    }

    private static void DrawText(Graphics graphics, string text, Font font, Color color, RectangleF rect)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString(text, font, brush, rect, format);
    }

    private static void FillRounded(Graphics graphics, RectangleF rect, float radius, Color color)
    {
        using var path = RoundedPath(rect, radius);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    private static void DrawRounded(Graphics graphics, RectangleF rect, float radius, Pen pen)
    {
        using var path = RoundedPath(rect, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color ColorFor(BlockShape shape)
    {
        uint hash = 2166136261;
        foreach (var character in shape.Id)
            hash = unchecked((hash ^ character) * 16777619);
        return PieceColors[hash % (uint)PieceColors.Length];
    }

    private static string FormatNumber(long value) => value.ToString("N0", NumberCulture);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scoreFont.Dispose();
            _headingFont.Dispose();
            _bodyFont.Dispose();
            _smallFont.Dispose();
            _overlayFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
