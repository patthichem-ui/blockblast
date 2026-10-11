namespace BlockBlast.Core;

public sealed record MoveResult(
    bool Success,
    string? Error,
    int PointsEarned,
    int LinesCleared,
    int Combo,
    bool TrayRefilled,
    bool IsGameOver)
{
    public IReadOnlyList<Cell> PlacedCells { get; init; } = Array.Empty<Cell>();
    public IReadOnlyList<Cell> ClearedCells { get; init; } = Array.Empty<Cell>();
    public string? ShapeId { get; init; }
}

/// <summary>
/// Coordinates atomic moves, score, progression, three-piece tray lifecycle, and game over.
/// A new tray is generated only after every piece in the current tray has been used.
/// Invalid moves leave board, tray, random state, statistics, and score unchanged.
/// </summary>
public sealed class GameEngine
{
    public const int MaximumUndos = 3;
    private static readonly IReadOnlyDictionary<string, BlockShape> ShapesById =
        ShapeCatalog.All.ToDictionary(shape => shape.Id, StringComparer.Ordinal);
    private static readonly string FallbackShapeId = ShapeCatalog.All.First(shape => shape.CellCount == 1).Id;
    private static readonly GameSaveValidator SaveValidator = new(ShapesById);
    private readonly BlockShape?[] tray = new BlockShape?[PieceGenerator.TraySize];
    private readonly string?[] cellShapeIds = new string?[Board.Size * Board.Size];
    private readonly List<GameStateSnapshot> undoHistory = [];
    private PieceGenerator generator = null!;
    private StatefulRandom random = null!;

    public Board Board { get; private set; } = new();
    public IReadOnlyList<BlockShape?> Tray { get; }
    public long Score { get; private set; }
    public int Level => CurrentDifficulty.Level;
    public int Combo { get; private set; }
    public int TotalLinesCleared { get; private set; }
    public int PiecesPlaced { get; private set; }
    public bool IsGameOver { get; private set; }
    public int Seed { get; private set; }
    public Guid GameId { get; private set; }
    public int UndosRemaining { get; private set; }
    public bool CanUndo => UndosRemaining > 0 && undoHistory.Count > 0;
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
        random = new StatefulRandom(Seed);
        generator = new PieceGenerator(random);
        GameId = Guid.NewGuid();
        Board = new Board();
        Score = 0;
        Combo = 0;
        TotalLinesCleared = 0;
        PiecesPlaced = 0;
        IsGameOver = false;
        UndosRemaining = MaximumUndos;
        undoHistory.Clear();
        Array.Clear(tray);
        Array.Clear(cellShapeIds);
        RefillTray();
    }

    public MoveResult TryPlace(int trayIndex, int row, int column)
    {
        if (IsGameOver) return Failed("Ván chơi đã kết thúc. Hãy bắt đầu ván mới.");
        if (trayIndex < 0 || trayIndex >= tray.Length) return Failed("Vị trí khối trong khay không hợp lệ.");
        if (tray[trayIndex] is not { } shape) return Failed("Khối này đã được sử dụng.");
        if (!Board.CanPlace(shape, row, column)) return Failed("Khối vượt khỏi bàn hoặc trùng với ô đã có.");

        if (UndosRemaining > 0)
        {
            undoHistory.Add(CaptureSnapshot());
            if (undoHistory.Count > UndosRemaining) undoHistory.RemoveAt(0);
        }
        var placedCells = shape.Cells.Select(cell => new Cell(row + cell.Row, column + cell.Column)).ToArray();
        ulong beforeClear = Board.Occupancy | Board.PlacementMask(shape, row, column);
        int levelBeforeMove = Level;
        var placement = Board.PlaceAndClear(shape, row, column);
        foreach (var cell in placedCells) cellShapeIds[cell.Row * Board.Size + cell.Column] = shape.Id;
        var clearedCells = new List<Cell>();
        for (int index = 0; index < cellShapeIds.Length; index++)
        {
            if ((Board.Occupancy & (1UL << index)) != 0) continue;
            cellShapeIds[index] = null;
            if ((beforeClear & (1UL << index)) != 0)
                clearedCells.Add(new Cell(index / Board.Size, index % Board.Size));
        }
        Combo = placement.LinesCleared > 0 ? Math.Min(int.MaxValue - 1, Combo) + 1 : 0;
        int points = ScoreCalculator.Calculate(shape.CellCount, placement.LinesCleared, Combo, levelBeforeMove);
        Score = Score > long.MaxValue - points ? long.MaxValue : Score + points;
        TotalLinesCleared = (int)Math.Min(int.MaxValue, (long)TotalLinesCleared + placement.LinesCleared);
        PiecesPlaced = Math.Min(int.MaxValue - 1, PiecesPlaced) + 1;
        tray[trayIndex] = null;

        bool refilled = tray.All(piece => piece is null);
        if (refilled) RefillTray();
        IsGameOver = !tray.Any(piece => piece is not null && Board.HasAnyPlacement(piece));
        return new MoveResult(true, null, points, placement.LinesCleared, Combo, refilled, IsGameOver)
        {
            PlacedCells = Array.AsReadOnly(placedCells),
            ClearedCells = clearedCells.AsReadOnly(),
            ShapeId = shape.Id
        };
    }

    /// <summary>Returns the original shape identity, even when only part of a piece remains.</summary>
    public string? GetCellShapeId(int row, int column)
        => Board.IsOccupied(row, column) ? cellShapeIds[row * Board.Size + column] ?? FallbackShapeId : null;

    public GameSaveData ExportSave() => new()
    {
        SchemaVersion = GameSaveData.CurrentSchemaVersion,
        State = CaptureSnapshot(),
        UndoHistory = undoHistory.Select(snapshot => snapshot.Copy()).ToList(),
        UndosRemaining = UndosRemaining
    };

    /// <summary>
    /// Restores a validated move boundary without generating a tray. Failure leaves the
    /// board, random stream, undo allowance and every other engine property unchanged.
    /// </summary>
    public bool TryRestoreSave(GameSaveData save, out string? error)
    {
        if (!SaveValidator.TryValidate(save, out error)) return false;
        var state = save.State.Copy();
        var history = save.UndoHistory.Select(snapshot => snapshot.Copy()).ToList();
        RestoreSnapshot(state);
        UndosRemaining = save.UndosRemaining;
        undoHistory.Clear();
        undoHistory.AddRange(history);
        return true;
    }

    /// <summary>Restores the previous move, including tray generation, then spends one undo.</summary>
    public bool TryUndo()
    {
        if (!CanUndo) return false;
        var previous = undoHistory[^1];
        RestoreSnapshot(previous);
        undoHistory.RemoveAt(undoHistory.Count - 1);
        UndosRemaining--;
        return true;
    }

    /// <summary>
    /// Solves the current remaining tray within a fixed budget. Slot indexes are mapped
    /// back to the real tray; inspecting a hint does not consume randomness or a move.
    /// </summary>
    public PlannedPlacement? FindHint()
    {
        if (IsGameOver) return null;
        var remaining = tray.Select((shape, index) => (Shape: shape, Index: index))
            .Where(entry => entry.Shape is not null).ToArray();
        var shapes = remaining.Select(entry => entry.Shape!).ToArray();
        if (new TraySolver(2048).TrySolve(Board, shapes, out var solution) && solution.Count > 0)
        {
            var first = solution[0];
            return new PlannedPlacement(remaining[first.TrayIndex].Index, first.Row, first.Column);
        }

        // A full tray solution may be impossible after the player's earlier choices.
        // Still offer a legal move, preferring clears and then the smaller resulting board.
        PlannedPlacement? best = null;
        int bestValue = int.MinValue;
        foreach (var entry in remaining)
        foreach (var placement in entry.Shape!.Placements)
        {
            if ((Board.Occupancy & placement.Mask) != 0) continue;
            ulong after = Board.ClearCompletedLines(Board.Occupancy | placement.Mask, out int rows, out int columns, out _);
            int lines = System.Numerics.BitOperations.PopCount((uint)rows) + System.Numerics.BitOperations.PopCount((uint)columns);
            int value = lines * 1000 - System.Numerics.BitOperations.PopCount(after);
            if (value <= bestValue) continue;
            bestValue = value;
            best = new PlannedPlacement(entry.Index, placement.Row, placement.Column);
        }
        return best;
    }

    private GameStateSnapshot CaptureSnapshot()
    {
        var identities = new string?[cellShapeIds.Length];
        for (int index = 0; index < identities.Length; index++)
            if ((Board.Occupancy & (1UL << index)) != 0)
                identities[index] = cellShapeIds[index] ?? FallbackShapeId;
        return new GameStateSnapshot
        {
            GameId = GameId,
            BoardBits = Board.Occupancy,
            TrayShapeIds = tray.Select(shape => shape?.Id).ToArray(),
            CellShapeIds = identities,
            Score = Score,
            Combo = Combo,
            TotalLinesCleared = TotalLinesCleared,
            PiecesPlaced = PiecesPlaced,
            Seed = Seed,
            RandomState = random.State
        };
    }

    private void RestoreSnapshot(GameStateSnapshot snapshot)
    {
        Board = new Board(snapshot.BoardBits);
        GameId = snapshot.GameId;
        Seed = snapshot.Seed;
        Score = snapshot.Score;
        Combo = snapshot.Combo;
        TotalLinesCleared = snapshot.TotalLinesCleared;
        PiecesPlaced = snapshot.PiecesPlaced;
        random = new StatefulRandom(snapshot.RandomState);
        generator = new PieceGenerator(random);
        for (int index = 0; index < tray.Length; index++)
            tray[index] = snapshot.TrayShapeIds[index] is { } id ? ShapesById[id] : null;
        snapshot.CellShapeIds.CopyTo(cellShapeIds, 0);
        IsGameOver = !tray.Any(piece => piece is not null && Board.HasAnyPlacement(piece));
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
