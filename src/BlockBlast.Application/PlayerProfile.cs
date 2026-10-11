namespace BlockBlast.Application;

public enum ThemeId { Aurora, Candy, Ocean, Sunset }
public enum StickerPack { Kawaii, Cosmic, Garden }

/// <summary>Preference values are copied at the session boundary to avoid unsaved external mutations.</summary>
public sealed record PlayerSettings
{
    public ThemeId Theme { get; set; } = ThemeId.Aurora;
    public StickerPack StickerPack { get; set; } = StickerPack.Kawaii;
    public bool MusicEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool EffectsEnabled { get; set; } = true;
    public bool StickersEnabled { get; set; } = true;
    public int Volume { get; set; } = 45;

    internal PlayerSettings Normalize() => this with
    {
        Theme = Enum.IsDefined(Theme) ? Theme : ThemeId.Aurora,
        StickerPack = Enum.IsDefined(StickerPack) ? StickerPack : StickerPack.Kawaii,
        Volume = Math.Clamp(Volume, 0, 100)
    };
}

/// <summary>Lifetime activity counts include successful moves later undone; personal records never decrease.</summary>
public sealed record PlayerStatistics
{
    public long BestScore { get; set; }
    public long TotalPiecesPlaced { get; set; }
    public long TotalLinesCleared { get; set; }
    public int BestCombo { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesCompleted { get; set; }
    public long TotalPlaySeconds { get; set; }

    internal PlayerStatistics Normalize() => this with
    {
        BestScore = Math.Max(0, BestScore),
        TotalPiecesPlaced = Math.Max(0, TotalPiecesPlaced),
        TotalLinesCleared = Math.Max(0, TotalLinesCleared),
        BestCombo = Math.Max(0, BestCombo),
        GamesPlayed = Math.Max(0, GamesPlayed),
        GamesCompleted = Math.Clamp(GamesCompleted, 0, Math.Max(0, GamesPlayed)),
        TotalPlaySeconds = Math.Max(0, TotalPlaySeconds)
    };
}
