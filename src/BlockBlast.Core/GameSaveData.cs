namespace BlockBlast.Core;

/// <summary>
/// Versioned, data-only save format. Undo snapshots are ordered oldest to newest.
/// The engine exports detached copies and validates all fields before importing a save.
/// </summary>
public sealed class GameSaveData
{
    public const int CurrentSchemaVersion = 1;

    // An absent version stays zero so incomplete JSON cannot masquerade as a valid save.
    public int SchemaVersion { get; set; }
    public GameStateSnapshot State { get; set; } = new();
    public List<GameStateSnapshot> UndoHistory { get; set; } = [];
    public int UndosRemaining { get; set; }
}

/// <summary>
/// One complete move boundary. Null tray entries are consumed slots; null cell identities
/// are empty board cells. RandomState stores the PRNG state, not a count to replay.
/// </summary>
public sealed class GameStateSnapshot
{
    public Guid GameId { get; set; }
    public ulong BoardBits { get; set; }
    public string?[] TrayShapeIds { get; set; } = [];
    public string?[] CellShapeIds { get; set; } = [];
    public long Score { get; set; }
    public int Combo { get; set; }
    public int TotalLinesCleared { get; set; }
    public int PiecesPlaced { get; set; }
    public int Seed { get; set; }
    public ulong RandomState { get; set; }

    internal GameStateSnapshot Copy() => new()
    {
        GameId = GameId,
        BoardBits = BoardBits,
        TrayShapeIds = (string?[])TrayShapeIds.Clone(),
        CellShapeIds = (string?[])CellShapeIds.Clone(),
        Score = Score,
        Combo = Combo,
        TotalLinesCleared = TotalLinesCleared,
        PiecesPlaced = PiecesPlaced,
        Seed = Seed,
        RandomState = RandomState
    };
}
