using BlockBlast.Core;

namespace BlockBlast.Application;

/// <summary>Versioned disk envelope. A missing version is deliberately invalid.</summary>
public sealed class SessionDocument
{
    public const int CurrentFormatVersion = 1;
    public int FormatVersion { get; set; }
    public DateTimeOffset SavedAtUtc { get; set; }
    public GameSaveData? Game { get; set; }
    public PlayerSettings? Settings { get; set; }
    public PlayerStatistics? Statistics { get; set; }
    public Guid? LastCompletedGameId { get; set; }
    public bool IsPaused { get; set; }
    public long ActivePlayRemainderTicks { get; set; }
}

public sealed record SessionLoadResult(SessionDocument? Document, string? Notice = null, long LegacyBestScore = 0);
public sealed record SessionSaveResult(bool Success, string? Notice = null);

/// <summary>Persistence is injectable; neither the game nor presentation layer needs to know about JSON or files.</summary>
public interface IGameRepository
{
    SessionLoadResult Load();
    SessionSaveResult Save(SessionDocument document);
}
