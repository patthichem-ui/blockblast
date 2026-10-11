namespace BlockBlast.Core;

public sealed record MoveResult(
    bool Success,
    string? Error,
    int PointsEarned,
    int LinesCleared,
    int Combo,
    bool TrayRefilled,
    bool IsGameOver);

/// <summary>
/// Coordinates atomic moves, score, progression, three-piece tray lifecycle, and game over.
/// A new tray is generated only after every piece in the current tray has been used.
/// Invalid moves leave board, tray, random state, statistics, and score unchanged.
/// </summary>
public sealed class GameEngine
{
    private readonly BlockShape?[] tray = new BlockShape?[PieceGenerator.TraySize];
    private PieceGenerator generator = null!;

    public Board Board { get; private set; } = new();
    public IReadOnlyList<BlockShape?> Tray { get; }
    public long Score { get; private set; }
    public int Level => CurrentDifficulty.Level;
    public int Combo { get; private set; }
    public int TotalLinesCleared { get; private set; }
    public int PiecesPlaced { get; private set; }
    public bool IsGameOver { get; private set; }
    public int Seed { get; private set; }
    public DifficultyProfile CurrentDifficulty => DifficultyProfile.ForProgress(Score, TotalLinesCleared, PiecesPlaced);
    public GenerationReport LastGeneration => generator.LastReport;

    public GameEngine(int? seed = null)
    {
        Tray = Array.AsReadOnly(tray);
        Reset(seed);
    }

    /// <summary>Passing a seed produces a replayable new game; null chooses a fresh random seed.</summary>
    public void Reset(int? seed = null)
    {
        Seed = seed ?? Random.Shared.Next();
        generator = new PieceGenerator(Seed);
        Board = new Board();
        Score = 0;
        Combo = 0;
        TotalLinesCleared = 0;
        PiecesPlaced = 0;
        IsGameOver = false;
        Array.Clear(tray);
        RefillTray();
    }

    public MoveResult TryPlace(int trayIndex, int row, int column)
    {
        if (IsGameOver) return Failed("Ván chơi đã kết thúc. Hãy bắt đầu ván mới.");
        if (trayIndex < 0 || trayIndex >= tray.Length) return Failed("Vị trí khối trong khay không hợp lệ.");
        if (tray[trayIndex] is not { } shape) return Failed("Khối này đã được sử dụng.");
        if (!Board.CanPlace(shape, row, column)) return Failed("Khối vượt khỏi bàn hoặc trùng với ô đã có.");

        int levelBeforeMove = Level;
        var placement = Board.PlaceAndClear(shape, row, column);
        Combo = placement.LinesCleared > 0 ? Math.Min(int.MaxValue - 1, Combo) + 1 : 0;
        int points = ScoreCalculator.Calculate(shape.CellCount, placement.LinesCleared, Combo, levelBeforeMove);
        Score = Score > long.MaxValue - points ? long.MaxValue : Score + points;
        TotalLinesCleared = (int)Math.Min(int.MaxValue, (long)TotalLinesCleared + placement.LinesCleared);
        PiecesPlaced = Math.Min(int.MaxValue - 1, PiecesPlaced) + 1;
        tray[trayIndex] = null;

        bool refilled = tray.All(piece => piece is null);
        if (refilled) RefillTray();
        IsGameOver = !tray.Any(piece => piece is not null && Board.HasAnyPlacement(piece));
        return new MoveResult(true, null, points, placement.LinesCleared, Combo, refilled, IsGameOver);
    }

    private void RefillTray()
    {
        var generated = generator.Generate(Board, CurrentDifficulty);
        for (int index = 0; index < tray.Length; index++)
            tray[index] = index < generated.Count ? generated[index] : null;
        IsGameOver = !tray.Any(piece => piece is not null && Board.HasAnyPlacement(piece));
    }

    private MoveResult Failed(string error) => new(false, error, 0, 0, Combo, false, IsGameOver);
}
