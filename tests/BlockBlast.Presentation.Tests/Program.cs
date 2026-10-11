using System.Drawing;
using BlockBlast.Core;
using BlockBlast.WinForms.Rendering;

namespace BlockBlast.Presentation.Tests;

/// <summary>Tests real canvas geometry using portable drawing primitives, without Windows or GDI.</summary>
internal static class Program
{
    private static readonly (Size Size, float Dpi)[] Viewports =
    [
        (new(1044, 610), 1), (new(704, 505), 1), (new(1318, 552), 1.5f),
        (new(1224, 366), 2), (new(968, 330), 2), (new(760, 241), 2),
        (new(968, 162), 2), (new(320, 480), 1), (new(320, 128), 3),
        (new(2088, 1220), 2)
    ];
    private static readonly BlockShape?[] Tray = ShapeCatalog.All.Take(3).Cast<BlockShape?>().ToArray();

    private static int Main()
    {
        (string Name, Action Run)[] cases =
        [
            ("Board, tray and status fit representative small/high-DPI viewports", RepresentativeViewports),
            ("Geometry stays separated across window sizes and DPI settings", ViewportMatrix),
            ("Every catalog shape fits every card and leaves its label clear", CatalogShapes),
            ("All 64 cell centers map to the same painted cell", CellHitTesting),
            ("DPI scaling preserves board and tray proportions", ProportionalScaling),
            ("Consumed slots clear old shape hit geometry", ConsumedSlots),
            ("Zero-sized canvas clears stale layout before resizing", EmptyCanvas)
        ];
        int failed = 0;
        foreach (var test in cases)
        {
            try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
            catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {error.Message}"); }
        }
        Console.WriteLine($"{cases.Length - failed}/{cases.Length} presentation test groups passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void RepresentativeViewports()
    {
        foreach (var viewport in Viewports) CheckLayout(viewport.Size, viewport.Dpi);
    }

    private static void ViewportMatrix()
    {
        foreach (int width in new[] { 320, 630, 704, 900, 1280, 1920, 2560 })
        foreach (int height in new[] { 128, 162, 241, 330, 530, 768, 1440 })
        foreach (float dpi in new[] { 1, 1.25f, 1.5f, 2, 3 })
            CheckLayout(new Size(width, height), dpi);
    }

    private static void CheckLayout(Size size, float dpi)
    {
        var layout = new GameLayout();
        layout.Update(size, dpi, Tray);
        var canvas = new RectangleF(Point.Empty, size);
        string context = $"{size.Width}x{size.Height} at {dpi * 100}%";
        Require(layout.Scale > 0 && float.IsFinite(layout.Scale), $"Invalid scale: {context}");
        Require(Contains(canvas, layout.Board), $"Board outside canvas: {context}");
        Require(Near(layout.Board.Width, layout.Board.Height), $"Board is not square: {context}");
        Require(Contains(layout.Board, layout.OverlayButton), $"Overlay button outside board: {context}");
        Require(layout.OverlayButton.Width > 0 && layout.OverlayButton.Height > 0, $"Empty overlay button: {context}");
        float statusTop = size.Height - 25 * layout.Scale; // The status band used by GameSurface.Drawing.
        for (int index = 0; index < 3; index++)
        {
            var card = layout.Cards[index];
            Require(card.Width > 0 && card.Height > 0 && Contains(canvas, card), $"Tray outside canvas: {context}");
            Require(card.Top > layout.Board.Bottom, $"Tray overlaps board: {context}");
            Require(card.Bottom < statusTop, $"Tray overlaps status: {context}");
            if (index > 0) Require(layout.Cards[index - 1].Right < card.Left, $"Tray cards overlap: {context}");
        }
    }

    private static void CatalogShapes()
    {
        var layout = new GameLayout();
        foreach (var viewport in Viewports)
        foreach (var shape in ShapeCatalog.All)
        {
            layout.Update(viewport.Size, viewport.Dpi, [shape, shape, shape]);
            for (int index = 0; index < 3; index++)
            {
                var bounds = layout.Shapes[index];
                var card = layout.Cards[index];
                float pitch = layout.TrayPitches[index];
                Require(pitch > 0 && float.IsFinite(pitch), $"Invalid pitch for {shape.Id}");
                // DrawShape reserves a minimum two-pixel gap and a small lower shadow.
                float drawingGap = Math.Max(2, pitch * .105f);
                Require(pitch - drawingGap - pitch * .025f > 0,
                    $"Shape {shape.Id} would produce a nonpositive painted tile");
                Require(Contains(card, bounds), $"Shape {shape.Id} extends outside slot {index}");
                Require(bounds.Bottom < card.Bottom - 20 * layout.Scale, $"Shape {shape.Id} overlaps the no-space label");
                foreach (var cell in shape.Cells)
                {
                    var tile = new RectangleF(bounds.Left + cell.Column * pitch,
                        bounds.Top + cell.Row * pitch, pitch, pitch);
                    Require(Contains(bounds, tile), $"Cell outside shape bounds for {shape.Id}");
                }
            }
        }
    }

    private static void CellHitTesting()
    {
        var layout = new GameLayout();
        foreach (var viewport in Viewports)
        {
            layout.Update(viewport.Size, viewport.Dpi, Tray);
            for (int row = 0; row < Board.Size; row++)
            for (int column = 0; column < Board.Size; column++)
            {
                var rect = layout.CellRect(row, column);
                Require(rect.Width > 0 && rect.Height > 0 && Contains(layout.Board, rect), "Invalid painted cell bounds");
                var center = Point.Round(new PointF(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2));
                Require(layout.PointerCell(center) == new Cell(row, column), $"Hit test disagrees at {row},{column}");
                if (column > 0) Require(layout.CellRect(row, column - 1).Right < rect.Left, "Painted columns overlap");
                if (row > 0) Require(layout.CellRect(row - 1, column).Bottom < rect.Top, "Painted rows overlap");
            }
            var outside = new Point((int)Math.Floor(layout.Board.Left) - 1, (int)Math.Floor(layout.Board.Top) - 1);
            Require(!layout.Board.Contains(outside), "Outside point accepted by board bounds");
            Require(layout.PointerCell(outside).Row < 0 && layout.PointerCell(outside).Column < 0,
                "Outside pointer incorrectly maps to first cell");
        }
    }

    private static void ProportionalScaling()
    {
        var normal = new GameLayout();
        var doubled = new GameLayout();
        normal.Update(new Size(1044, 610), 1, Tray);
        doubled.Update(new Size(2088, 1220), 2, Tray);
        Require(normal.Wide && doubled.Wide, "Wide layout unexpectedly changed");
        Require(Near(normal.Board.Width * 2, doubled.Board.Width), "Board did not scale with DPI");
        for (int index = 0; index < 3; index++)
        {
            Require(Near(normal.Cards[index].Left * 2, doubled.Cards[index].Left), "Tray X did not scale with DPI");
            Require(Near(normal.Cards[index].Top * 2, doubled.Cards[index].Top), "Tray Y did not scale with DPI");
            Require(Near(normal.TrayPitches[index] * 2, doubled.TrayPitches[index]), "Tray tile did not scale with DPI");
        }
        normal.Update(new Size(640, 650), 1, Tray);
        Require(!normal.Wide, "Narrow window should use compact header");
    }

    private static void ConsumedSlots()
    {
        var layout = new GameLayout();
        layout.Update(new Size(1044, 610), 1, Tray);
        layout.Update(new Size(1044, 610), 1, [null, Tray[1], null]);
        Require(layout.Shapes[0].IsEmpty && layout.Shapes[2].IsEmpty, "Consumed shape bounds were retained");
        Require(layout.TrayPitches[0] == 0 && layout.TrayPitches[2] == 0, "Consumed shape pitches were retained");
        Require(!layout.Shapes[1].IsEmpty && layout.TrayPitches[1] > 0, "Remaining shape was removed");
        Require(layout.Cards.All(card => card.Width > 0), "Consumed tray cards should remain visible");
    }

    private static void EmptyCanvas()
    {
        var layout = new GameLayout();
        layout.Update(new Size(1044, 610), 1, Tray);
        layout.Update(Size.Empty, 1, Tray);
        Require(layout.Board.IsEmpty && layout.Sidebar.IsEmpty && layout.OverlayButton.IsEmpty, "Stale empty-window bounds");
        Require(layout.Cards.All(card => card.IsEmpty) && layout.Shapes.All(shape => shape.IsEmpty), "Stale tray bounds");
        Require(layout.TrayPitches.All(pitch => pitch == 0), "Stale tray pitches");
        layout.Update(new Size(760, 241), 2, Tray);
        Require(layout.Board.Width > 0 && layout.Cards.All(card => card.Width > 0), "Resize did not restore geometry");
    }

    private static bool Contains(RectangleF outer, RectangleF inner) =>
        inner.Left >= outer.Left - .001f && inner.Top >= outer.Top - .001f &&
        inner.Right <= outer.Right + .001f && inner.Bottom <= outer.Bottom + .001f;
    private static bool Near(float first, float second) => Math.Abs(first - second) < .001f;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
