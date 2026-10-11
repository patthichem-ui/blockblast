using System.Numerics;

namespace BlockBlast.Core;

public readonly record struct PlannedPlacement(int TrayIndex, int Row, int Column);
public readonly record struct SearchStatistics(int VisitedStates, bool HitLimit);

/// <summary>
/// A bounded depth-first search over tray order and legal anchors, including line clears.
/// Occupancy + remaining-piece mask is memoized; equivalent shapes do not create duplicate
/// permutation branches. Search is deterministic and never changes the caller's board.
/// A false result with HitLimit=true means "not proven within the budget", not "impossible".
/// </summary>
public sealed class TraySolver
{
    public int MaxVisitedStates { get; }

    public TraySolver(int maxVisitedStates = 2048)
    {
        if (maxVisitedStates < 1) throw new ArgumentOutOfRangeException(nameof(maxVisitedStates));
        MaxVisitedStates = maxVisitedStates;
    }

    public bool TrySolve(Board board, IReadOnlyList<BlockShape> tray, out IReadOnlyList<PlannedPlacement> solution)
        => TrySolve(board, tray, out solution, out _);

    public bool TrySolve(
        Board board,
        IReadOnlyList<BlockShape> tray,
        out IReadOnlyList<PlannedPlacement> solution,
        out SearchStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(tray);
        if (tray.Count > 8) throw new ArgumentException("The bounded solver supports at most eight pieces.", nameof(tray));
        if (tray.Any(shape => shape is null)) throw new ArgumentException("Solver pieces must be non-null.", nameof(tray));

        var deadStates = new HashSet<SearchState>();
        var path = new List<PlannedPlacement>();
        int visited = 0;
        bool hitLimit = false;
        bool solved = Search(board.Occupancy, (1 << tray.Count) - 1);
        solution = solved ? Array.AsReadOnly(path.ToArray()) : Array.Empty<PlannedPlacement>();
        statistics = new SearchStatistics(visited, hitLimit);
        return solved;

        bool Search(ulong occupancy, int remaining)
        {
            if (remaining == 0) return true;
            var state = new SearchState(occupancy, remaining);
            if (deadStates.Contains(state)) return false;
            if (visited >= MaxVisitedStates)
            {
                hitLimit = true;
                return false;
            }
            visited++;

            var branches = new List<(int Index, List<SearchPlacement> Placements)>();
            var equivalentShapes = new HashSet<ulong>();
            for (int index = 0; index < tray.Count; index++)
            {
                if ((remaining & (1 << index)) == 0) continue;
                var shape = tray[index];
                // The mask at (0,0) uniquely describes normalized shape geometry.
                if (!equivalentShapes.Add(shape.Placements[0].Mask)) continue;
                var candidates = new List<SearchPlacement>();
                var equivalentResults = new HashSet<ulong>();
                foreach (var placement in shape.Placements)
                {
                    if ((occupancy & placement.Mask) != 0) continue;
                    ulong next = Board.ClearCompletedLines(occupancy | placement.Mask, out int rows, out int columns, out _);
                    if (!equivalentResults.Add(next)) continue;
                    int lines = BitOperations.PopCount((uint)rows) + BitOperations.PopCount((uint)columns);
                    candidates.Add(new SearchPlacement(placement.Row, placement.Column, next, lines));
                }
                if (candidates.Count > 0)
                    branches.Add((index, candidates));
            }

            // A currently unplaceable piece is not grounds for pruning: another piece may clear
            // space first. Among placeable pieces, try the most constrained shape first.
            foreach (var branch in branches.OrderBy(branch => branch.Placements.Count).ThenBy(branch => branch.Index))
            foreach (var placement in branch.Placements
                .OrderByDescending(placement => placement.LinesCleared)
                .ThenBy(placement => BitOperations.PopCount(placement.NextOccupancy))
                .ThenBy(placement => placement.Row).ThenBy(placement => placement.Column))
            {
                path.Add(new PlannedPlacement(branch.Index, placement.Row, placement.Column));
                if (Search(placement.NextOccupancy, remaining & ~(1 << branch.Index))) return true;
                path.RemoveAt(path.Count - 1);
                if (hitLimit) return false;
            }
            deadStates.Add(state);
            return false;
        }
    }

    private readonly record struct SearchState(ulong Occupancy, int Remaining);
    private readonly record struct SearchPlacement(int Row, int Column, ulong NextOccupancy, int LinesCleared);
}
