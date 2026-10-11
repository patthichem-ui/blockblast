using System.Security;
using System.Text.Json;

namespace BlockBlast.WinForms;

/// <summary>Stores only the personal best, outside the application's checkout.</summary>
internal sealed class HighScoreStore
{
    private readonly string? _directory;

    public HighScoreStore()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localData) && Path.IsPathFullyQualified(localData))
            _directory = Path.Combine(localData, "BlockBlast");
    }

    public long Load()
    {
        try
        {
            if (_directory is null)
                return 0;
            var path = Path.Combine(_directory, "highscore.json");
            if (!File.Exists(path))
                return 0;

            var saved = JsonSerializer.Deserialize<SavedScore>(File.ReadAllText(path));
            return Math.Max(0, saved?.Score ?? 0);
        }
        catch (Exception error) when (IsStorageError(error))
        {
            return 0;
        }
    }

    public bool TrySave(long score)
    {
        string? temporaryFile = null;
        try
        {
            if (_directory is null)
                return false;
            score = Math.Max(score, Load());
            Directory.CreateDirectory(_directory);
            temporaryFile = Path.Combine(_directory, $"highscore-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(new SavedScore(score)));
            File.Move(temporaryFile, Path.Combine(_directory, "highscore.json"), overwrite: true);
            return true;
        }
        catch (Exception error) when (IsStorageError(error))
        {
            return false;
        }
        finally
        {
            if (temporaryFile is not null)
            {
                try
                {
                    File.Delete(temporaryFile);
                }
                catch (Exception error) when (IsStorageError(error))
                {
                    // A storage failure must not interrupt a game.
                }
            }
        }
    }

    private static bool IsStorageError(Exception error) =>
        error is IOException or UnauthorizedAccessException or JsonException or SecurityException;

    private sealed record SavedScore(long Score);
}
