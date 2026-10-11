using System.Drawing.Drawing2D;
using System.Globalization;
using BlockBlast.Core;
using BlockBlast.WinForms.Rendering;

namespace BlockBlast.WinForms;

internal sealed partial class GameSurface
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        UpdateLayout();
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        _background.Draw(g, ClientSize, _theme, _ambientTime, _layout.Scale);
        if (_layout.Wide) DrawSidebar(g); else DrawCompactHeader(g);
        DrawBoard(g);
        DrawTray(g);
        if (!_session.IsPaused)
        {
            _effects.Draw(g, _layout);
            if (_dragging && _preview is null && SelectedShape is { } shape)
                DrawShape(g, shape, new PointF(_pointer.X - (_grabbedCell.Column + .5f) * _layout.Pitch,
                    _pointer.Y - (_grabbedCell.Row + .5f) * _layout.Pitch), _layout.Pitch, 180);
        }
        if (_session.IsPaused || Engine.IsGameOver) DrawOverlay(g);
        DrawStatus(g);
    }

    private void DrawSidebar(Graphics g)
    {
        float s = _layout.Scale;
        var r = _layout.Sidebar;
        UiDrawing.Text(g, "BLOCK", _titleFont, _theme.Text, new RectangleF(r.X, r.Y, r.Width, 40 * s), StringAlignment.Near);
        UiDrawing.Text(g, "BLAST", _titleFont, _theme.Accent, new RectangleF(r.X, r.Y + 34 * s, r.Width, 44 * s), StringAlignment.Near);
        UiDrawing.Text(g, "MỘT CHÚT CHƠI · MỘT CHÚT VUI", _smallFont, _theme.Muted,
            new RectangleF(r.X + 2 * s, r.Y + 80 * s, r.Width, 22 * s), StringAlignment.Near);
        float scoreTop = r.Top + 124 * s;
        var card = new RectangleF(r.X, scoreTop, r.Width, 122 * s);
        DrawCard(g, card);
        UiDrawing.Text(g, "ĐIỂM CỦA BẠN", _smallFont, _theme.Muted, new RectangleF(card.X + 18 * s, card.Y + 13 * s, card.Width - 36 * s, 22 * s), StringAlignment.Near);
        DrawFitted(g, Format(Engine.Score), _scoreFont, _theme.Text,
            new RectangleF(card.X + 16 * s, card.Y + 34 * s, card.Width - 32 * s, 62 * s), StringAlignment.Near);
        UiDrawing.Text(g, $"Kỷ lục   {Format(_session.Statistics.BestScore)}", _smallFont, _theme.Accent,
            new RectangleF(card.X + 18 * s, card.Bottom - 25 * s, card.Width - 36 * s, 20 * s), StringAlignment.Near);
        float rowTop = card.Bottom + 12 * s;
        DrawMetric(g, new RectangleF(r.X, rowTop, (r.Width - 10 * s) / 2, 75 * s), "CẤP ĐỘ", Engine.Level.ToString(), _theme.Secondary);
        DrawMetric(g, new RectangleF(r.X + (r.Width + 10 * s) / 2, rowTop, (r.Width - 10 * s) / 2, 75 * s), "ĐÃ XÓA", Engine.TotalLinesCleared.ToString(), _theme.Accent);
        float comboTop = rowTop + 97 * s;
        UiDrawing.Text(g, Engine.Combo > 0 ? $"COMBO ×{Engine.Combo}" : "SẴN SÀNG BÙNG NỔ?", _headingFont,
            Engine.Combo > 0 ? _theme.Accent : _theme.Text, new RectangleF(r.X, comboTop, r.Width, 30 * s), StringAlignment.Near);
        for (int i = 0; i < 7; i++)
        {
            var chip = new RectangleF(r.X + i * (r.Width / 7), comboTop + 40 * s, r.Width / 7 - 6 * s, 8 * s);
            UiDrawing.Fill(g, chip, 4 * s, i < Engine.Combo ? _theme.Accent : _theme.EmptyCell);
        }
        UiDrawing.Text(g, "Xóa hàng liên tiếp để nhân điểm.", _smallFont, _theme.Muted,
            new RectangleF(r.X, comboTop + 56 * s, r.Width, 24 * s), StringAlignment.Near);
        if (r.Height > 570 * s)
        {
            UiDrawing.Text(g, "CỨ THONG THẢ THÔI", _smallFont, _theme.Secondary,
                new RectangleF(r.X, r.Bottom - 63 * s, r.Width, 22 * s), StringAlignment.Near);
            UiDrawing.Text(g, "Không giới hạn thời gian. Tự động lưu.", _smallFont, _theme.Muted,
                new RectangleF(r.X, r.Bottom - 39 * s, r.Width, 22 * s), StringAlignment.Near);
        }
        UiDrawing.Text(g, $"{_theme.Name.ToUpperInvariant()}  /  8 × 8", _smallFont, _theme.Muted,
            new RectangleF(_layout.Board.Left, 18 * s, _layout.Board.Width, 22 * s));
    }

    private void DrawCompactHeader(Graphics g)
    {
        float s = _layout.Scale;
        UiDrawing.Text(g, "BLOCK BLAST", _titleFont, _theme.Text, new RectangleF(20 * s, 12 * s, Width - 40 * s, 42 * s));
        float totalWidth = Math.Min(Width - 44 * s, 540 * s);
        float gap = 10 * s, cardWidth = (totalWidth - gap * 2) / 3;
        float x = (Width - totalWidth) / 2;
        DrawMetric(g, new RectangleF(x, 66 * s, cardWidth, 62 * s), "ĐIỂM", Format(Engine.Score), _theme.Accent);
        DrawMetric(g, new RectangleF(x + cardWidth + gap, 66 * s, cardWidth, 62 * s), "KỶ LỤC", Format(_session.Statistics.BestScore), _theme.Text);
        DrawMetric(g, new RectangleF(x + (cardWidth + gap) * 2, 66 * s, cardWidth, 62 * s), $"CẤP {Engine.Level}", $"Combo ×{Engine.Combo}", _theme.Secondary, true);
    }

    private void DrawMetric(Graphics g, RectangleF rect, string label, string value, Color color, bool smallValue = false)
    {
        float s = _layout.Scale;
        DrawCard(g, rect);
        UiDrawing.Text(g, label, _smallFont, _theme.Muted, new RectangleF(rect.X + 6 * s, rect.Y + 6 * s, rect.Width - 12 * s, 20 * s));
        DrawFitted(g, value, smallValue ? _headingFont : _numberFont, color,
            new RectangleF(rect.X + 9 * s, rect.Y + 25 * s, rect.Width - 18 * s, rect.Height - 29 * s));
    }

    private void DrawCard(Graphics g, RectangleF rect)
    {
        UiDrawing.Fill(g, rect, 17 * _layout.Scale, _theme.Panel);
        UiDrawing.Outline(g, RectangleF.Inflate(rect, -.5f, -.5f), 17 * _layout.Scale, Color.FromArgb(22, _theme.Text));
    }

    private void DrawBoard(Graphics g)
    {
        float s = _layout.Scale;
        var shadow = RectangleF.Inflate(_layout.Board, 12 * s, 12 * s);
        shadow.Offset(0, 7 * s);
        UiDrawing.Fill(g, shadow, 23 * s, Color.FromArgb(65, Color.Black));
        var surround = RectangleF.Inflate(_layout.Board, 11 * s, 11 * s);
        UiDrawing.Fill(g, surround, 22 * s, _theme.Board);
        UiDrawing.Outline(g, surround, 22 * s, Color.FromArgb(35, _theme.Secondary), 1.3f * s);
        for (int row = 0; row < Board.Size; row++)
        for (int column = 0; column < Board.Size; column++)
        {
            var rect = _layout.CellRect(row, column);
            if (Engine.Board.IsOccupied(row, column))
                _blocks.Draw(g, rect, Engine.GetCellShapeId(row, column), _settings);
            else BlockRenderer.Empty(g, rect, _theme);
        }
        if (!_session.IsPaused && _preview is { } preview && SelectedShape is { } shape)
        {
            bool valid = Engine.Board.CanPlace(shape, preview.Row, preview.Column);
            var state = g.Save();
            g.SetClip(_layout.Board);
            foreach (var cell in shape.Cells)
            {
                var rect = _layout.CellRect(preview.Row + cell.Row, preview.Column + cell.Column);
                _blocks.Draw(g, rect, shape.Id, _settings, valid ? 135 : 60);
                UiDrawing.Outline(g, rect, rect.Width * .22f, valid ? _theme.Accent : Color.FromArgb(255, 117, 143), 2 * s);
            }
            g.Restore(state);
        }
    }

    private void DrawTray(Graphics g)
    {
        float s = _layout.Scale;
        UiDrawing.Text(g, "CHỌN MỘT KHỐI · TẠO MỘT ĐIỀU HAY", _smallFont, _theme.Muted,
            new RectangleF(_layout.Cards[0].Left, _layout.Board.Bottom + 13 * s,
                _layout.Cards[2].Right - _layout.Cards[0].Left, 18 * s));
        for (int i = 0; i < 3; i++)
        {
            var card = _layout.Cards[i];
            bool selected = _selectedIndex == i;
            DrawCard(g, card);
            if (selected) UiDrawing.Outline(g, RectangleF.Inflate(card, -.8f, -.8f), 17 * s, _theme.Accent, 1.8f * s);
            UiDrawing.Text(g, (i + 1).ToString(), _smallFont, selected ? _theme.Accent : _theme.Muted,
                new RectangleF(card.X + 5 * s, card.Y + 4 * s, 19 * s, 19 * s));
            if (Engine.Tray[i] is { } shape)
            {
                bool canPlace = Engine.Board.HasAnyPlacement(shape);
                DrawShape(g, shape, _layout.Shapes[i].Location, _layout.TrayPitches[i],
                    selected && _dragging ? 95 : canPlace ? 255 : 140);
                if (!canPlace && !Engine.IsGameOver)
                    UiDrawing.Text(g, "Chưa có chỗ", _smallFont, _theme.Muted,
                        new RectangleF(card.X + 5 * s, card.Bottom - 20 * s, card.Width - 10 * s, 18 * s));
            }
            else
            {
                using var pen = new Pen(Color.FromArgb(85, _theme.Accent), 2.5f * s);
                float cx = card.Left + card.Width / 2, cy = card.Top + card.Height / 2;
                g.DrawLines(pen, [new PointF(cx - 9 * s, cy), new PointF(cx - 2 * s, cy + 7 * s), new PointF(cx + 12 * s, cy - 9 * s)]);
            }
        }
    }

    private void DrawShape(Graphics g, BlockShape shape, PointF origin, float pitch, int alpha)
    {
        float gap = Math.Min(pitch * .25f, Math.Max(2, pitch * .105f));
        foreach (var cell in shape.Cells)
            _blocks.Draw(g, new RectangleF(origin.X + cell.Column * pitch + gap / 2,
                origin.Y + cell.Row * pitch + gap / 2, pitch - gap, pitch - gap - pitch * .025f), shape.Id, _settings, alpha);
    }

    private void DrawOverlay(Graphics g)
    {
        float s = _layout.Scale;
        var board = _layout.Board;
        UiDrawing.Fill(g, RectangleF.Inflate(board, 9 * s, 9 * s), 22 * s, Color.FromArgb(238, _theme.Background));
        bool ended = Engine.IsGameOver;
        UiDrawing.Text(g, ended ? "CHƠI HAY LẮM!" : "NGHỈ MỘT CHÚT", _titleFont, _theme.Text,
            new RectangleF(board.Left + 12 * s, board.Top + board.Height * .19f, board.Width - 24 * s, 54 * s));
        UiDrawing.Text(g, ended ? $"{Format(Engine.Score)} điểm · Cấp {Engine.Level}" :
            _session.LastSaveSucceeded ? "Tiến độ của bạn đã được giữ lại." : "Chưa ghi được tiến độ xuống máy.",
            _bodyFont, _theme.Muted, new RectangleF(board.Left + 10 * s, board.Top + board.Height * .39f, board.Width - 20 * s, 30 * s));
        UiDrawing.Text(g, ended && Engine.CanUndo ? "Bạn vẫn có thể hoàn tác để thử lại." : ended ? "Một ván mới, một cơ hội phá kỷ lục." : "Cứ thong thả, không cần vội vàng.",
            _smallFont, _theme.Muted, new RectangleF(board.Left + 10 * s, board.Top + board.Height * .48f, board.Width - 20 * s, 24 * s));
        UiDrawing.Fill(g, _layout.OverlayButton, 13 * s, _theme.Accent);
        UiDrawing.Text(g, ended ? "CHƠI LẠI  ·  R" : "TIẾP TỤC  ·  P", _headingFont, _theme.Background, _layout.OverlayButton);
    }

    private void DrawStatus(Graphics g)
    {
        float s = _layout.Scale;
        string status = !_session.LastSaveSucceeded ? "Chưa lưu được: " + _session.SaveNotice : _status;
        if (_session.LastSaveSucceeded && _clock.Elapsed.TotalSeconds > _statusUntil)
            status = "1–3 chọn khối · Enter đặt · H gợi ý · Ctrl+Z hoàn tác · P tạm dừng";
        UiDrawing.Text(g, status, _smallFont, !_session.LastSaveSucceeded ? Color.FromArgb(255, 198, 140) : _theme.Muted,
            new RectangleF(12 * s, Height - 25 * s, Width - 24 * s, 21 * s));
    }

    private static string Format(long value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    private static void DrawFitted(Graphics g, string text, Font font, Color color, RectangleF rect,
        StringAlignment alignment = StringAlignment.Center)
    {
        float measured = g.MeasureString(text, font).Width;
        if (measured <= rect.Width) { UiDrawing.Text(g, text, font, color, rect, alignment); return; }
        using var fitted = new Font(font.FontFamily, Math.Max(8, font.Size * rect.Width / measured), font.Style);
        UiDrawing.Text(g, text, fitted, color, rect, alignment);
    }
}
