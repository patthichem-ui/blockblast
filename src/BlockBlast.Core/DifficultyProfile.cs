namespace BlockBlast.Core;

/// <summary>
/// Independent progression rules for this game; these are not the proprietary Block Blast algorithm.
/// Level = min(20, 1 + max(lines / 10, pieces / 45, floor(sqrt(score / 2500)))).
/// Increasing level shifts the shape distribution toward larger, less compact shapes.
/// High board occupancy softens that distribution, but never reduces the achieved level.
/// </summary>
public sealed record DifficultyProfile(
    int Level,
    double TargetCellCount,
    double SizeSpread,
    double ComplexityBias,
    int CandidateTrays,
    int SolverNodeBudget,
    int TotalSearchBudget)
{
    public static DifficultyProfile ForProgress(long score, int totalLinesCleared, int piecesPlaced)
    {
        if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
        if (totalLinesCleared < 0) throw new ArgumentOutOfRangeException(nameof(totalLinesCleared));
        if (piecesPlaced < 0) throw new ArgumentOutOfRangeException(nameof(piecesPlaced));
        int scoreStage = (int)Math.Min(19, Math.Floor(Math.Sqrt(score / 2500.0)));
        int level = 1 + Math.Min(19, Math.Max(scoreStage, Math.Max(totalLinesCleared / 10, piecesPlaced / 45)));
        return new DifficultyProfile(
            Level: level,
            TargetCellCount: 3.2 + Math.Min(level - 1, 12) * 0.18,
            SizeSpread: 1.35 + (level - 1) * 0.07,
            ComplexityBias: (level - 1) * 0.035,
            CandidateTrays: level < 10 ? 12 : 10,
            SolverNodeBudget: 2048,
            TotalSearchBudget: 16384);
    }
}

/// <summary>
/// Each placed cell earns 5 points. Clearing L lines earns 100×L²×level;
/// successive clearing moves add 50×(combo−1)×L×level. A move with no clear resets
/// combo to zero. The level BEFORE the move is used, so crossing a level threshold
/// never changes the price of the move that crossed it.
/// </summary>
public static class ScoreCalculator
{
    public static int Calculate(int cellCount, int linesCleared, int combo, int level)
    {
        if (cellCount < 1 || cellCount > Board.Size * Board.Size)
            throw new ArgumentOutOfRangeException(nameof(cellCount));
        if (linesCleared < 0 || linesCleared > Board.Size * 2)
            throw new ArgumentOutOfRangeException(nameof(linesCleared));
        if (combo < 0 || (linesCleared > 0 && combo < 1))
            throw new ArgumentOutOfRangeException(nameof(combo));
        if (level < 1 || level > 20) throw new ArgumentOutOfRangeException(nameof(level));

        long points = 5L * cellCount;
        if (linesCleared > 0)
            points += 100L * linesCleared * linesCleared * level
                + 50L * (combo - 1) * linesCleared * level;
        // Keep MoveResult.PointsEarned representable even in an artificially huge combo.
        return (int)Math.Min(int.MaxValue, points);
    }
}
