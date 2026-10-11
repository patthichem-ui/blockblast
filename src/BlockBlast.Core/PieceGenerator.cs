using System.Numerics;

namespace BlockBlast.Core;

public sealed record GenerationReport(
    bool HasCompleteSolution,
    bool UsedFallback,
    int Attempts,
    int SolverStatesVisited,
    IReadOnlyList<PlannedPlacement> Solution);

/// <summary>
/// Seeded weighted sampling with a shared, finite search budget for each three-piece tray.
/// Every candidate includes a currently legal piece, then is shuffled and tested for a complete
/// solution. If random candidates exhaust the budget, a sequential construction on a cloned
/// board produces a known playable tray using smaller shapes, including the singleton.
/// The solution is a certificate of at least one possible order, not a promise that every move
/// chosen by the player will preserve that solution.
/// </summary>
public sealed class PieceGenerator
{
    public const int TraySize = 3;
    private readonly Random random;

    public GenerationReport LastReport { get; private set; } =
        new(false, false, 0, 0, Array.Empty<PlannedPlacement>());

    public PieceGenerator(int? seed = null) : this(seed.HasValue ? new Random(seed.Value) : new Random()) { }

    public PieceGenerator(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        this.random = random;
    }

    public IReadOnlyList<BlockShape> Generate(Board board, DifficultyProfile difficulty)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(difficulty);
        Validate(difficulty);
        var legalShapes = ShapeCatalog.All.Where(board.HasAnyPlacement).ToArray();
        if (legalShapes.Length == 0)
        {
            LastReport = new(false, false, 0, 0, Array.Empty<PlannedPlacement>());
            return Array.Empty<BlockShape>();
        }

        int totalVisited = 0;
        int attempts = 0;
        for (; attempts < difficulty.CandidateTrays && totalVisited < difficulty.TotalSearchBudget; attempts++)
        {
            var tray = new List<BlockShape>(TraySize);
            tray.Add(Choose(legalShapes, difficulty, board.OccupiedCount, tray));
            while (tray.Count < TraySize)
                tray.Add(Choose(ShapeCatalog.All, difficulty, board.OccupiedCount, tray));
            Shuffle(tray);

            int budget = Math.Min(difficulty.SolverNodeBudget, difficulty.TotalSearchBudget - totalVisited);
            var solver = new TraySolver(budget);
            bool solved = solver.TrySolve(board, tray, out var solution, out var statistics);
            totalVisited += statistics.VisitedStates;
            if (solved)
            {
                LastReport = new(true, false, attempts + 1, totalVisited, solution);
                return tray.AsReadOnly();
            }
        }

        return ConstructFallback(board, difficulty, attempts, totalVisited);
    }

    private IReadOnlyList<BlockShape> ConstructFallback(Board board, DifficultyProfile difficulty, int attempts, int totalVisited)
    {
        var simulation = board.Clone();
        var entries = new List<(BlockShape Shape, int Row, int Column, int Sequence)>(TraySize);
        for (int index = 0; index < TraySize; index++)
        {
            var legal = ShapeCatalog.All.Where(candidateShape => candidateShape.CellCount <= 4 && simulation.HasAnyPlacement(candidateShape)).ToArray();
            if (legal.Length == 0)
            {
                // Defensive handling of an externally constructed impossible board. Normal
                // PlaceAndClear states always retain a vacancy and can place the singleton.
                LastReport = new(false, true, attempts, totalVisited, Array.Empty<PlannedPlacement>());
                return entries.Select(entry => entry.Shape).ToArray();
            }
            var shape = Choose(legal, difficulty, simulation.OccupiedCount, entries.Select(entry => entry.Shape).ToArray());
            var candidates = shape.Placements
                .Where(placement => (simulation.Occupancy & placement.Mask) == 0)
                .Select(placement =>
                {
                    ulong next = Board.ClearCompletedLines(simulation.Occupancy | placement.Mask, out int rows, out int columns, out _);
                    int lines = BitOperations.PopCount((uint)rows) + BitOperations.PopCount((uint)columns);
                    return (Placement: placement, Lines: lines, Occupied: BitOperations.PopCount(next));
                })
                .OrderByDescending(candidate => candidate.Lines)
                .ThenBy(candidate => candidate.Occupied)
                .ThenBy(candidate => candidate.Placement.Row).ThenBy(candidate => candidate.Placement.Column)
                .ToArray();
            // Sample among the best few anchors to keep seeded fallback games varied.
            var chosen = candidates[random.Next(Math.Min(3, candidates.Length))].Placement;
            simulation.PlaceAndClear(shape, chosen.Row, chosen.Column);
            entries.Add((shape, chosen.Row, chosen.Column, index));
        }
        Shuffle(entries);
        var solution = entries.Select((entry, trayIndex) => (entry, trayIndex))
            .OrderBy(pair => pair.entry.Sequence)
            .Select(pair => new PlannedPlacement(pair.trayIndex, pair.entry.Row, pair.entry.Column)).ToArray();
        LastReport = new(true, true, attempts, totalVisited, Array.AsReadOnly(solution));
        return Array.AsReadOnly(entries.Select(entry => entry.Shape).ToArray());
    }

    private BlockShape Choose(
        IReadOnlyList<BlockShape> choices,
        DifficultyProfile difficulty,
        int occupiedCount,
        IReadOnlyList<BlockShape> alreadyChosen)
    {
        double density = occupiedCount / 64.0;
        double target = Math.Max(2.0, difficulty.TargetCellCount - Math.Max(0, density - 0.45) * 3.5);
        var weights = new double[choices.Count];
        double total = 0;
        for (int index = 0; index < choices.Count; index++)
        {
            var shape = choices[index];
            double distance = shape.CellCount - target;
            // Gaussian size preference, increasing preference for holes and long spans,
            // and a repeat penalty so all three slots rarely show the same orientation.
            double sizeWeight = Math.Exp(-distance * distance / (2 * difficulty.SizeSpread * difficulty.SizeSpread));
            int holes = shape.Width * shape.Height - shape.CellCount;
            int span = Math.Max(shape.Width, shape.Height);
            double complexityWeight = (1 + difficulty.ComplexityBias * (holes + Math.Max(0, span - 3))) / (1 + 0.12 * holes);
            double largeWeight = 1 + 0.1 * (difficulty.Level - 1) * Math.Max(0, shape.CellCount - 4);
            double repeatWeight = alreadyChosen.Any(chosen => chosen.Id == shape.Id) ? 0.25 : 1;
            weights[index] = Math.Max(double.Epsilon, sizeWeight * complexityWeight * largeWeight * repeatWeight);
            total += weights[index];
        }
        double sample = random.NextDouble() * total;
        for (int index = 0; index < choices.Count; index++)
        {
            sample -= weights[index];
            if (sample <= 0) return choices[index];
        }
        return choices[^1];
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int other = random.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    private static void Validate(DifficultyProfile difficulty)
    {
        if (difficulty.Level < 1 || difficulty.Level > 20
            || !double.IsFinite(difficulty.TargetCellCount) || difficulty.TargetCellCount <= 0
            || !double.IsFinite(difficulty.SizeSpread) || difficulty.SizeSpread <= 0
            || !double.IsFinite(difficulty.ComplexityBias) || difficulty.ComplexityBias < 0
            || difficulty.CandidateTrays < 1 || difficulty.CandidateTrays > 32
            || difficulty.SolverNodeBudget < 1 || difficulty.SolverNodeBudget > 100_000
            || difficulty.TotalSearchBudget < 1 || difficulty.TotalSearchBudget > 100_000)
            throw new ArgumentOutOfRangeException(nameof(difficulty), "Difficulty values and search budgets must be finite and within supported bounds.");
    }
}
