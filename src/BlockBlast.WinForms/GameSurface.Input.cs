using BlockBlast.Core;

namespace BlockBlast.WinForms;

internal sealed partial class GameSurface
{
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        UpdateLayout();
        if (_session.IsPaused || Engine.IsGameOver)
        {
            if (_layout.OverlayButton.Contains(e.Location))
            {
                if (Engine.IsGameOver) NewGameRequested?.Invoke();
                else ResumeRequested?.Invoke();
            }
            return;
        }
        for (int index = 0; index < _layout.Cards.Length; index++)
        {
            if (!_layout.Cards[index].Contains(e.Location) || Engine.Tray[index] is not { } shape) continue;
            _selectedIndex = index;
            _pressedTray = true;
            _dragging = _keyboardMode = false;
            _pointer = _pressPoint = e.Location;
            _preview = null;
            _grabbedCell = ClosestShapeCell(shape, index, e.Location);
            Capture = true;
            ShowNotice($"{shape.Name} · kéo vào bảng hoặc bấm ô góc trên bên trái để đặt.");
            return;
        }
        if (_layout.Board.Contains(e.Location) && SelectedShape is not null)
            PlaceSelected(_layout.PointerCell(e.Location));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        UpdateLayout();
        _pointer = e.Location;
        if (_session.IsPaused || Engine.IsGameOver)
        {
            Cursor = _layout.OverlayButton.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            return;
        }
        if (_pressedTray && !_dragging)
        {
            var threshold = SystemInformation.DragSize;
            _dragging = Math.Abs(e.X - _pressPoint.X) > threshold.Width / 2 ||
                        Math.Abs(e.Y - _pressPoint.Y) > threshold.Height / 2;
        }
        _keyboardMode = false;
        if (SelectedShape is not null && _layout.Board.Contains(e.Location))
        {
            var cell = _layout.PointerCell(e.Location);
            _preview = _dragging ? new Cell(cell.Row - _grabbedCell.Row, cell.Column - _grabbedCell.Column) : cell;
        }
        else _preview = null;
        Cursor = SelectedShape is not null || _layout.Cards.Any(card => card.Contains(e.Location))
            ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_pressedTray) return;
        UpdateLayout();
        if (!_session.IsPaused && !Engine.IsGameOver && _dragging && _layout.Board.Contains(e.Location))
        {
            var cell = _layout.PointerCell(e.Location);
            PlaceSelected(new Cell(cell.Row - _grabbedCell.Row, cell.Column - _grabbedCell.Column));
        }
        _pressedTray = _dragging = false;
        Capture = false;
        _preview = SelectedShape is not null && _layout.Board.Contains(e.Location)
            ? _layout.PointerCell(e.Location) : null;
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            _pressedTray = _dragging = false;
            if (!_keyboardMode) _preview = null;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_keyboardMode) _preview = null;
        Invalidate();
    }

    public bool HandleGameKey(Keys keyData)
    {
        if ((keyData & Keys.Modifiers) != Keys.None) return false;
        if (keyData == Keys.Escape && _selectedIndex >= 0)
        {
            ClearSelection();
            ShowNotice("Đã hủy chọn khối.");
            return true;
        }
        if (_session.IsPaused || Engine.IsGameOver) return false;
        int selection = keyData switch
        {
            Keys.D1 or Keys.NumPad1 => 0,
            Keys.D2 or Keys.NumPad2 => 1,
            Keys.D3 or Keys.NumPad3 => 2,
            _ => -1
        };
        if (selection >= 0)
        {
            if (Engine.Tray[selection] is { } shape)
            {
                ClearSelection();
                _selectedIndex = selection;
                _keyboardCell = FirstFit(shape);
                _keyboardMode = true;
                _preview = _keyboardCell;
                Focus();
                ShowNotice($"{shape.Name} · mũi tên chọn vị trí, Enter để đặt.");
            }
            return true;
        }
        if (SelectedShape is not { } selected) return false;
        var delta = keyData switch
        {
            Keys.Left => new Cell(0, -1), Keys.Right => new Cell(0, 1),
            Keys.Up => new Cell(-1, 0), Keys.Down => new Cell(1, 0),
            _ => new Cell(0, 0)
        };
        if (delta.Row != 0 || delta.Column != 0)
        {
            if (!_keyboardMode) _keyboardCell = _preview ?? FirstFit(selected);
            _keyboardCell = new Cell(Math.Clamp(_keyboardCell.Row + delta.Row, 0, Board.Size - selected.Height),
                Math.Clamp(_keyboardCell.Column + delta.Column, 0, Board.Size - selected.Width));
            _keyboardMode = true;
            _preview = _keyboardCell;
            Invalidate();
            return true;
        }
        if (keyData is Keys.Enter or Keys.Space)
        {
            PlaceSelected(_preview ?? FirstFit(selected));
            return true;
        }
        return false;
    }

    private void PlaceSelected(Cell target)
    {
        if (SelectedShape is null || _session.IsPaused) return;
        var result = _session.Place(_selectedIndex, target.Row, target.Column);
        if (!result.Success)
        {
            _preview = target;
            ShowNotice(result.Error ?? "Khối chưa vừa ở đây. Thử vị trí khác nhé.");
            MoveAttempted?.Invoke(result);
            return;
        }
        ClearSelection();
        UpdateLayout();
        if (_settings.EffectsEnabled) _effects.AddMove(result, _layout, _theme);
        string notice = $"+{result.PointsEarned:N0} điểm";
        if (result.LinesCleared > 0) notice += $" · Xóa {result.LinesCleared} hàng/cột · Combo ×{result.Combo}";
        if (result.TrayRefilled) notice += " · Bộ khối mới đã sẵn sàng";
        ShowNotice(notice);
        AccessibleDescription = $"Điểm {Engine.Score}. Cấp độ {Engine.Level}. {notice}";
        AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1);
        MoveAttempted?.Invoke(result);
        RefreshSession();
    }

    private void ClearSelection()
    {
        _selectedIndex = -1;
        _pressedTray = _dragging = _keyboardMode = false;
        _preview = null;
        Capture = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    private Cell FirstFit(BlockShape shape)
    {
        for (int row = 0; row < Board.Size; row++)
        for (int column = 0; column < Board.Size; column++)
            if (Engine.Board.CanPlace(shape, row, column)) return new Cell(row, column);
        return new Cell(0, 0);
    }

    private Cell ClosestShapeCell(BlockShape shape, int index, Point point)
    {
        var rect = _layout.Shapes[index];
        float pitch = _layout.TrayPitches[index];
        return shape.Cells.MinBy(cell =>
        {
            float dx = rect.Left + (cell.Column + .5f) * pitch - point.X;
            float dy = rect.Top + (cell.Row + .5f) * pitch - point.Y;
            return dx * dx + dy * dy;
        });
    }
}
