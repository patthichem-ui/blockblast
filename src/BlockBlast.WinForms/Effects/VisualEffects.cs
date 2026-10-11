using BlockBlast.Core;
using BlockBlast.WinForms.Rendering;
using BlockBlast.WinForms.Themes;

namespace BlockBlast.WinForms.Effects;

/// <summary>Bounded, time-based particles, cell flashes and floating score callouts.</summary>
internal sealed class VisualEffects : IDisposable
{
    private const int MaximumParticles = 180;
    private readonly List<Particle> _particles = [];
    private readonly List<Flash> _flashes = [];
    private readonly List<Callout> _callouts = [];
    private readonly Font _scoreFont = new("Segoe UI", 19, FontStyle.Bold);
    private readonly Font _comboFont = new("Segoe UI", 25, FontStyle.Bold);
    private readonly Random _random = new();

    public void AddMove(MoveResult move, GameLayout layout, GameTheme theme)
    {
        foreach (var cell in move.PlacedCells) _flashes.Add(new Flash(cell, theme.Accent, .3f, .3f));
        foreach (var cell in move.ClearedCells)
        {
            _flashes.Add(new Flash(cell, theme.Secondary, .6f, .6f));
            var rect = layout.CellRect(cell.Row, cell.Column);
            for (int i = 0; i < 5 && _particles.Count < MaximumParticles; i++)
            {
                float angle = _random.NextSingle() * MathF.PI * 2;
                float speed = (35 + _random.NextSingle() * 160) * layout.Scale;
                _particles.Add(new Particle(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2,
                    MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 30,
                    3 + _random.NextSingle() * 5, .6f + _random.NextSingle() * .5f,
                    i % 2 == 0 ? theme.Accent : BlockRenderer.ColorFor(move.ShapeId), i % 3 == 0));
            }
        }
        while (_flashes.Count > 96) _flashes.RemoveAt(0);
        string text = move.Combo > 1 ? $"COMBO ×{move.Combo}!" : move.LinesCleared > 1 ? "QUÁ ĐỈNH!" : move.LinesCleared > 0 ? "TUYỆT VỜI!" : "";
        var target = move.PlacedCells.FirstOrDefault();
        float x = layout.Board.Left + layout.Board.Width / 2;
        float y = layout.CellRect(target.Row, target.Column).Top;
        if (text.Length > 0)
            _callouts.Add(new Callout(text, x, layout.Board.Top + layout.Board.Height * .38f, theme.Accent, 1.5f, true));
        _callouts.Add(new Callout($"+{move.PointsEarned:N0}", x,
            text.Length > 0 ? layout.Board.Top + layout.Board.Height * .5f : Math.Max(layout.Board.Top + 25, y),
            theme.Text, 1.1f, false));
        while (_callouts.Count > 6) _callouts.RemoveAt(0);
    }

    public void Update(float seconds)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= seconds;
            if (p.Life <= 0) { _particles.RemoveAt(i); continue; }
            p.X += p.Vx * seconds; p.Y += p.Vy * seconds; p.Vy += 190 * seconds;
        }
        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            _flashes[i].Life -= seconds;
            if (_flashes[i].Life <= 0) _flashes.RemoveAt(i);
        }
        for (int i = _callouts.Count - 1; i >= 0; i--)
        {
            _callouts[i].Life -= seconds; _callouts[i].Y -= seconds * 27;
            if (_callouts[i].Life <= 0) _callouts.RemoveAt(i);
        }
    }

    public void Draw(Graphics g, GameLayout layout)
    {
        foreach (var flash in _flashes)
        {
            var rect = layout.CellRect(flash.Cell.Row, flash.Cell.Column);
            UiDrawing.Fill(g, rect, rect.Width * .22f, Color.FromArgb((int)(180 * flash.Life / flash.Duration), flash.Color));
        }
        foreach (var p in _particles)
        {
            using var brush = new SolidBrush(Color.FromArgb((int)(255 * Math.Clamp(p.Life / .35f, 0, 1)), p.Color));
            float r = p.Radius * layout.Scale;
            if (p.Star) g.FillPolygon(brush, UiDrawing.Star(new PointF(p.X, p.Y), r, r * .4f, 4, p.Life * 3));
            else g.FillEllipse(brush, p.X - r, p.Y - r, r * 2, r * 2);
        }
        foreach (var c in _callouts)
        {
            int alpha = (int)(255 * Math.Clamp(c.Life / .35f, 0, 1));
            var rect = new RectangleF(layout.Board.Left - 20, c.Y - 20, layout.Board.Width + 40, 55 * layout.Scale);
            var shadow = rect; shadow.Offset(0, 2);
            UiDrawing.Text(g, c.Text, c.Combo ? _comboFont : _scoreFont, Color.FromArgb(alpha, 25, 18, 50), shadow);
            UiDrawing.Text(g, c.Text, c.Combo ? _comboFont : _scoreFont, Color.FromArgb(alpha, c.Color), rect);
        }
    }

    public void Clear() { _particles.Clear(); _flashes.Clear(); _callouts.Clear(); }
    public void Dispose() { _scoreFont.Dispose(); _comboFont.Dispose(); Clear(); }
    private sealed class Particle(float x, float y, float vx, float vy, float radius, float life, Color color, bool star)
    {
        public float X = x, Y = y, Vx = vx, Vy = vy, Radius = radius, Life = life;
        public Color Color = color;
        public bool Star = star;
    }
    private sealed class Flash(Cell cell, Color color, float life, float duration)
    {
        public Cell Cell = cell; public Color Color = color; public float Life = life, Duration = duration;
    }
    private sealed class Callout(string text, float x, float y, Color color, float life, bool combo)
    {
        public string Text = text; public float X = x, Y = y, Life = life; public Color Color = color; public bool Combo = combo;
    }
}
