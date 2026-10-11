using BlockBlast.Core;

namespace BlockBlast.Application;

/// <summary>Owns the play session, preferences, lifetime statistics and automatic saves.</summary>
public sealed class GameSessionController : IDisposable
{
    private readonly IGameRepository repository;
    private readonly TimeProvider clock;
    private PlayerSettings settings = new();
    private PlayerStatistics statistics = new();
    private Guid? lastCompletedGameId;
    private long lastTimestamp;
    private long remainderTicks;
    private bool disposed;
    private readonly string? startupNotice;

    public GameEngine Engine { get; } = new();
    public PlayerSettings Settings => settings with { };
    public PlayerStatistics Statistics => statistics with { };
    public bool IsPaused { get; private set; }
    public bool HasRestoredGame { get; }
    public bool LastSaveSucceeded { get; private set; }
    public string? SaveNotice { get; private set; }

    public GameSessionController(IGameRepository repository, TimeProvider? timeProvider = null)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        clock = timeProvider ?? TimeProvider.System;
        var loaded = repository.Load();
        startupNotice = loaded.Notice;
        if (loaded.Document is { FormatVersion: SessionDocument.CurrentFormatVersion, Game: { } game } document &&
            Engine.TryRestoreSave(game, out _))
        {
            settings = (document.Settings ?? new()).Normalize();
            statistics = (document.Statistics ?? new()).Normalize();
            lastCompletedGameId = document.LastCompletedGameId;
            remainderTicks = Math.Clamp(document.ActivePlayRemainderTicks, 0, TimeSpan.TicksPerSecond - 1);
            IsPaused = document.IsPaused;
            HasRestoredGame = true;
            statistics.BestScore = Math.Max(statistics.BestScore, Engine.Score);
            statistics.BestCombo = Math.Max(statistics.BestCombo, Engine.Combo);
            statistics.GamesPlayed = Math.Max(1, statistics.GamesPlayed);
            RecordCompletion();
        }
        else
        {
            statistics.GamesPlayed = 1;
            if (loaded.Document is not null)
                startupNotice = "Bản lưu không hợp lệ. Đã tạo ván mới.";
        }
        statistics.BestScore = Math.Max(statistics.BestScore, Math.Max(0, loaded.LegacyBestScore));
        lastTimestamp = clock.GetTimestamp();
        Save();
    }

    public MoveResult Place(int trayIndex, int row, int column)
    {
        ThrowIfDisposed();
        CaptureElapsed();
        if (IsPaused)
            return new(false, "Ván chơi đang tạm dừng.", 0, 0, Engine.Combo, false, Engine.IsGameOver);
        var result = Engine.TryPlace(trayIndex, row, column);
        if (!result.Success) return result;
        statistics.TotalPiecesPlaced = AddSaturated(statistics.TotalPiecesPlaced, 1);
        statistics.TotalLinesCleared = AddSaturated(statistics.TotalLinesCleared, result.LinesCleared);
        statistics.BestScore = Math.Max(statistics.BestScore, Engine.Score);
        statistics.BestCombo = Math.Max(statistics.BestCombo, result.Combo);
        RecordCompletion();
        Save();
        return result;
    }

    public bool Undo()
    {
        ThrowIfDisposed();
        CaptureElapsed();
        if (IsPaused || !Engine.TryUndo()) return false;
        Save();
        return true;
    }

    public PlannedPlacement? Hint()
    {
        ThrowIfDisposed();
        return IsPaused || Engine.IsGameOver ? null : Engine.FindHint();
    }

    public void NewGame()
    {
        ThrowIfDisposed();
        CaptureElapsed();
        Engine.Reset();
        IsPaused = false;
        statistics.GamesPlayed = (int)Math.Min(int.MaxValue, (long)statistics.GamesPlayed + 1);
        Save();
    }

    public void SetPaused(bool paused)
    {
        ThrowIfDisposed();
        if (IsPaused == paused) return;
        CaptureElapsed();
        IsPaused = paused;
        Save();
    }

    public void UpdateSettings(PlayerSettings value)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(value);
        settings = value.Normalize();
        Save();
    }

    public bool Save()
    {
        ThrowIfDisposed();
        CaptureElapsed();
        var result = repository.Save(new SessionDocument
        {
            FormatVersion = SessionDocument.CurrentFormatVersion,
            SavedAtUtc = clock.GetUtcNow(),
            Game = Engine.ExportSave(),
            Settings = Settings,
            Statistics = Statistics,
            LastCompletedGameId = lastCompletedGameId,
            IsPaused = IsPaused,
            ActivePlayRemainderTicks = remainderTicks
        });
        LastSaveSucceeded = result.Success;
        SaveNotice = result.Notice ?? (result.Success ? startupNotice : "Không thể lưu tiến độ. Vui lòng kiểm tra quyền ghi và dung lượng đĩa.");
        return result.Success;
    }

    public void Dispose()
    {
        if (disposed) return;
        Save();
        disposed = true;
    }

    private void RecordCompletion()
    {
        if (!Engine.IsGameOver || lastCompletedGameId == Engine.GameId) return;
        statistics.GamesCompleted = (int)Math.Min(int.MaxValue, (long)statistics.GamesCompleted + 1);
        lastCompletedGameId = Engine.GameId;
    }

    private void CaptureElapsed()
    {
        long currentTimestamp = clock.GetTimestamp();
        TimeSpan elapsed = clock.GetElapsedTime(lastTimestamp, currentTimestamp);
        lastTimestamp = currentTimestamp;
        if (IsPaused || Engine.IsGameOver || elapsed <= TimeSpan.Zero) return;
        long totalTicks = AddSaturated(remainderTicks, elapsed.Ticks);
        statistics.TotalPlaySeconds = AddSaturated(statistics.TotalPlaySeconds, totalTicks / TimeSpan.TicksPerSecond);
        remainderTicks = totalTicks % TimeSpan.TicksPerSecond;
    }

    private static long AddSaturated(long value, long increment) =>
        value > long.MaxValue - increment ? long.MaxValue : value + increment;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
