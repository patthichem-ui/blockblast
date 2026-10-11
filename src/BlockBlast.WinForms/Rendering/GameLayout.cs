using System.Drawing;
using BlockBlast.Core;

namespace BlockBlast.WinForms.Rendering;

/// <summary>One layout source for painting, hit testing and drag anchoring.</summary>
internal sealed class GameLayout
{
    public RectangleF Board { get; private set; }
    public RectangleF Sidebar { get; private set; }
    public RectangleF[] Cards { get; } = new RectangleF[3];
    public RectangleF[] Shapes { get; } = new RectangleF[3];
    public float[] TrayPitches { get; } = new float[3];
    public RectangleF OverlayButton { get; private set; }
    public bool Wide { get; private set; }
    public float Pitch => Board.Width / Core.Board.Size;
    public float Scale { get; private set; }

    public void Update(Size size, float dpiScale, IReadOnlyList<BlockShape?> tray)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            Scale = 0;
            Wide = false;
            Board = Sidebar = OverlayButton = RectangleF.Empty;
            Array.Clear(Cards);
            Array.Clear(Shapes);
            Array.Clear(TrayPitches);
            return;
        }
        // The toolbar can consume several rows at high DPI. Scale every section
        // together to the actual remaining canvas; fixed minimum sizes would overflow it.
        Scale = Math.Min(dpiScale, Math.Min(size.Width / 630f, size.Height / 530f));
        float s = Scale;
        Wide = size.Width >= 900 * s;
        float left = Wide ? 298 * s : 0;
        float areaWidth = size.Width - left;
        float top = Wide ? 54 * s : 152 * s;
        float boardSize = Math.Min(444 * s, Math.Min(areaWidth - 76 * s, size.Height - top - 166 * s));
        Board = new RectangleF(left + (areaWidth - boardSize) / 2, top, boardSize, boardSize);
        Sidebar = new RectangleF(30 * s, 24 * s, 242 * s, size.Height - 48 * s);
        float trayWidth = Math.Min(areaWidth - 40 * s, Math.Max(boardSize + 30 * s, 370 * s));
        float gap = 10 * s;
        float width = (trayWidth - 2 * gap) / 3;
        float cardHeight = 100 * s;
        for (int i = 0; i < Cards.Length; i++)
        {
            Cards[i] = new RectangleF(left + (areaWidth - trayWidth) / 2 + i * (width + gap),
                Board.Bottom + 34 * s, width, cardHeight);
            if (tray[i] is not { } shape)
            {
                Shapes[i] = RectangleF.Empty;
                TrayPitches[i] = 0;
                continue;
            }
            // Leave the bottom of each card free for the "no space yet" label.
            var shapeArea = new RectangleF(Cards[i].Left + 12 * s, Cards[i].Top + 13 * s,
                width - 24 * s, cardHeight - 35 * s);
            float pitch = Math.Min(Pitch * .74f, Math.Min(shapeArea.Width / shape.Width,
                shapeArea.Height / shape.Height));
            TrayPitches[i] = pitch;
            Shapes[i] = new RectangleF(shapeArea.Left + (shapeArea.Width - shape.Width * pitch) / 2,
                shapeArea.Top + (shapeArea.Height - shape.Height * pitch) / 2,
                shape.Width * pitch, shape.Height * pitch);
        }
        OverlayButton = new RectangleF(Board.Left + Board.Width * .16f, Board.Top + Board.Height * .62f,
            Board.Width * .68f, Math.Min(48 * s, Board.Height * .16f));
    }

    public Cell PointerCell(Point point) => new((int)Math.Floor((point.Y - Board.Top) / Pitch),
        (int)Math.Floor((point.X - Board.Left) / Pitch));

    public RectangleF CellRect(int row, int column)
    {
        float gap = Math.Min(Pitch * .25f, Math.Max(2, Pitch * .105f));
        return new RectangleF(Board.Left + column * Pitch + gap / 2, Board.Top + row * Pitch + gap / 2,
            Pitch - gap, Pitch - gap - Pitch * .025f);
    }
}
