using System.Numerics;

namespace BlockBlast.Core;

public sealed record BoardClearingResult(
    int CellsPlaced,
    IReadOnlyList<int> RowsCleared,
    IReadOnlyList<int> ColumnsCleared,
    int CellsCleared)
{
    public int LinesCleared => RowsCleared.Count + ColumnsCleared.Count;
}

/// <summary>
/// An 8×8 board represented by 64 bits. Application moves should go through GameEngine;
/// direct placement is also exposed for simulation, diagnostics, and unit tests.
/// </summary>
public sealed class Board
{
    public const int Size = 8;
    private const ulong FirstColumnMask = 0x0101010101010101UL;
    private ulong occupied;

    public int OccupiedCount => BitOperations.PopCount(occupied);
    internal ulong Occupancy => occupied;

    public Board() { }
    internal Board(ulong occupied) => this.occupied = occupied;
    public Board Clone() => new(occupied);

    public bool IsOccupied(int row, int column)
    {
        if (row < 0 || row >= Size) throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0 || column >= Size) throw new ArgumentOutOfRangeException(nameof(column));
        return (occupied & (1UL << (row * Size + column))) != 0;
    }

    public bool CanPlace(BlockShape shape, int row, int column)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (row < 0 || column < 0 || row > Size - shape.Height || column > Size - shape.Width)
            return false;
        ulong mask = PlacementMask(shape, row, column);
        return (occupied & mask) == 0;
    }

    public bool HasAnyPlacement(BlockShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape.Placements.Any(placement => (occupied & placement.Mask) == 0);
    }

    /// <summary>
    /// Places atomically, then detects all full rows AND columns against the same board
    /// before clearing their union. Intersection cells count once in CellsCleared.
    /// An invalid placement throws without changing the board.
    /// </summary>
    public BoardClearingResult PlaceAndClear(BlockShape shape, int row, int column)
    {
        if (!CanPlace(shape, row, column))
            throw new InvalidOperationException("The shape overlaps occupied cells or extends beyond the board.");
        ulong afterPlacement = occupied | PlacementMask(shape, row, column);
        ulong afterClear = ClearCompletedLines(afterPlacement, out int rowsMask, out int columnsMask, out int cellsCleared);
        var rows = new List<int>();
        var columns = new List<int>();
        for (int index = 0; index < Size; index++)
        {
            if ((rowsMask & (1 << index)) != 0) rows.Add(index);
            if ((columnsMask & (1 << index)) != 0) columns.Add(index);
        }
        occupied = afterClear;
        return new BoardClearingResult(shape.CellCount, rows.AsReadOnly(), columns.AsReadOnly(), cellsCleared);
    }

    internal static ulong PlacementMask(BlockShape shape, int row, int column)
    {
        ulong mask = 0;
        foreach (var cell in shape.Cells)
            mask |= 1UL << ((row + cell.Row) * Size + column + cell.Column);
        return mask;
    }

    internal static ulong ClearCompletedLines(ulong occupancy, out int rowsMask, out int columnsMask, out int cellsCleared)
    {
        rowsMask = 0;
        columnsMask = 0;
        ulong clearMask = 0;
        for (int index = 0; index < Size; index++)
        {
            ulong rowMask = 0xffUL << (index * Size);
            ulong columnMask = FirstColumnMask << index;
            if ((occupancy & rowMask) == rowMask)
            {
                rowsMask |= 1 << index;
                clearMask |= rowMask;
            }
            if ((occupancy & columnMask) == columnMask)
            {
                columnsMask |= 1 << index;
                clearMask |= columnMask;
            }
        }
        cellsCleared = BitOperations.PopCount(occupancy & clearMask);
        return occupancy & ~clearMask;
    }
}
