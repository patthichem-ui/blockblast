namespace BlockBlast.Core;

/// <summary>A board coordinate, or a coordinate relative to a shape's top-left corner.</summary>
public readonly record struct Cell(int Row, int Column);

/// <summary>An immutable, normalized shape. Shapes are rotated in the catalog, never during a move.</summary>
public sealed class BlockShape
{
    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<Cell> Cells { get; }
    public int Width { get; }
    public int Height { get; }
    public int CellCount => Cells.Count;

    internal IReadOnlyList<ShapePlacement> Placements { get; }

    public BlockShape(string id, string name, IEnumerable<Cell> cells)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(cells);
        var input = cells.ToArray();
        if (input.Length == 0 || input.Length > Board.Size * Board.Size)
            throw new ArgumentException("A shape must contain between 1 and 64 cells.", nameof(cells));
        if (input.Distinct().Count() != input.Length)
            throw new ArgumentException("A shape cannot contain duplicate cells.", nameof(cells));

        int minimumRow = input.Min(cell => cell.Row);
        int minimumColumn = input.Min(cell => cell.Column);
        long height = (long)input.Max(cell => cell.Row) - minimumRow + 1;
        long width = (long)input.Max(cell => cell.Column) - minimumColumn + 1;
        if (height > Board.Size || width > Board.Size)
            throw new ArgumentException("A shape must fit within an 8 by 8 board.", nameof(cells));

        var normalized = input
            .Select(cell => new Cell(cell.Row - minimumRow, cell.Column - minimumColumn))
            .OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray();
        Id = id;
        Name = name;
        Height = (int)height;
        Width = (int)width;
        Cells = Array.AsReadOnly(normalized);

        var placements = new List<ShapePlacement>();
        for (int row = 0; row <= Board.Size - Height; row++)
        for (int column = 0; column <= Board.Size - Width; column++)
        {
            ulong mask = 0;
            foreach (var cell in normalized)
                mask |= 1UL << ((row + cell.Row) * Board.Size + column + cell.Column);
            placements.Add(new ShapePlacement(row, column, mask));
        }
        Placements = placements.AsReadOnly();
    }
}

internal readonly record struct ShapePlacement(int Row, int Column, ulong Mask);

/// <summary>40 distinct normalized variants, including all distinct quarter-turn rotations.</summary>
public static class ShapeCatalog
{
    public static IReadOnlyList<BlockShape> All { get; } = Build();

    private static IReadOnlyList<BlockShape> Build()
    {
        var result = new List<BlockShape>();
        Add("single", "Ô đơn", "#");
        Add("line-2", "Thanh 2", "##");
        Add("line-3", "Thanh 3", "###");
        Add("corner-3", "Góc 3", "##", "#.");
        Add("square-2", "Vuông 2×2", "##", "##");
        Add("line-4", "Thanh 4", "####");
        Add("el-4", "Chữ L", "#.", "#.", "##");
        Add("jay-4", "Chữ J", ".#", ".#", "##");
        Add("tee-4", "Chữ T", "###", ".#.");
        Add("ess-4", "Chữ S", ".##", "##.");
        Add("zee-4", "Chữ Z", "##.", ".##");
        Add("line-5", "Thanh 5", "#####");
        Add("rectangle-6", "Chữ nhật 2×3", "###", "###");
        Add("corner-5", "Góc lớn", "###", "#..", "#..");
        Add("plus-5", "Dấu cộng", ".#.", "###", ".#.");
        Add("square-3", "Vuông 3×3", "###", "###", "###");
        Add("rectangle-8", "Chữ nhật 2×4", "####", "####");
        return result.AsReadOnly();

        void Add(string id, string name, params string[] pattern)
        {
            var cells = new List<Cell>();
            for (int row = 0; row < pattern.Length; row++)
            for (int column = 0; column < pattern[row].Length; column++)
                if (pattern[row][column] == '#') cells.Add(new Cell(row, column));

            var signatures = new HashSet<string>(StringComparer.Ordinal);
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var shape = new BlockShape($"{id}-r{rotation}", name, cells);
                string signature = string.Join(';', shape.Cells.Select(cell => $"{cell.Row},{cell.Column}"));
                if (signatures.Add(signature)) result.Add(shape);
                cells = shape.Cells.Select(cell => new Cell(cell.Column, -cell.Row)).ToList();
            }
        }
    }
}
