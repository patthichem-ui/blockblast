using System.Security;
using System.Text.Json;
using BlockBlast.Core;

namespace BlockBlast.Application;

/// <summary>
/// Writes a flushed temporary file before atomically replacing the primary save.
/// The previous valid save remains in session.json.bak; invalid files are preserved for diagnosis.
/// </summary>
public sealed class JsonGameRepository : IGameRepository
{
    public const string SaveFileName = "session.json";
    public const int MaximumFileBytes = 1024 * 1024;
    private readonly string directory;
    private readonly string primaryPath;
    private readonly string backupPath;
    private readonly object gate = new();
    private readonly GameEngine validator = new(0);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 32 };

    public JsonGameRepository(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("The save directory must be an absolute user-profile path.", nameof(directory));
        this.directory = Path.GetFullPath(directory);
        primaryPath = Path.Combine(this.directory, SaveFileName);
        backupPath = primaryPath + ".bak";
    }

    public SessionLoadResult Load()
    {
        lock (gate)
        {
            long legacyBest = ReadLegacyBestScore();
            var primary = ReadCandidate(primaryPath);
            if (primary.Document is not null)
                return new(primary.Document, LegacyBestScore: legacyBest);

            var backup = ReadCandidate(backupPath);
            if (primary.Status == ReadStatus.Invalid) TryQuarantine(primaryPath);
            if (backup.Document is not null)
                return new(backup.Document, "Đã khôi phục tiến độ từ bản sao lưu dự phòng.", legacyBest);

            if (backup.Status == ReadStatus.Invalid) TryQuarantine(backupPath);
            string? notice = primary.Status == ReadStatus.Inaccessible || backup.Status == ReadStatus.Inaccessible
                ? "Chưa đọc được dữ liệu đã lưu. Kiểm tra quyền truy cập thư mục dữ liệu của ứng dụng."
                : primary.Status == ReadStatus.Invalid || backup.Status == ReadStatus.Invalid
                    ? "Dữ liệu cũ bị hỏng hoặc khác phiên bản; đã giữ lại tệp để kiểm tra và tạo ván mới."
                    : null;
            return new(null, notice, legacyBest);
        }
    }

    public SessionSaveResult Save(SessionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (gate)
        {
            string? temporaryPath = null;
            try
            {
                if (!IsValidDocument(document))
                    return new(false, "Dữ liệu ván chơi không hợp lệ nên chưa được ghi vào tệp.");

                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
                if (payload.Length > MaximumFileBytes)
                    return new(false, "Dữ liệu ván chơi vượt giới hạn kích thước tệp lưu.");

                Directory.CreateDirectory(directory);
                temporaryPath = Path.Combine(directory, $"session-{Guid.NewGuid():N}.tmp");
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           4096, FileOptions.WriteThrough))
                {
                    stream.Write(payload);
                    stream.Flush(flushToDisk: true);
                }

                var existing = ReadCandidate(primaryPath);
                if (existing.Status == ReadStatus.Inaccessible)
                    return new(false, "Không thể truy cập bản lưu hiện tại. Tiến độ vẫn được giữ trong bộ nhớ.");
                if (existing.Status == ReadStatus.Invalid && !TryQuarantine(primaryPath))
                    return new(false, "Không thể giữ lại bản lưu cũ bị hỏng. Hãy kiểm tra quyền ghi thư mục dữ liệu.");

                if (existing.Document is not null)
                    File.Replace(temporaryPath, primaryPath, backupPath, ignoreMetadataErrors: false);
                else
                    File.Move(temporaryPath, primaryPath, overwrite: false);

                temporaryPath = null;
                return new(true);
            }
            catch (Exception error) when (IsStorageError(error))
            {
                return new(false, "Chưa lưu được tiến độ. Hãy kiểm tra quyền ghi và dung lượng đĩa; trò chơi vẫn tiếp tục.");
            }
            finally
            {
                if (temporaryPath is not null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception error) when (IsStorageError(error)) { }
                }
            }
        }
    }

    private Candidate ReadCandidate(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0 || stream.Length > MaximumFileBytes)
                return new(ReadStatus.Invalid);
            var document = JsonSerializer.Deserialize<SessionDocument>(stream, JsonOptions);
            return document is not null && IsValidDocument(document)
                ? new(ReadStatus.Valid, document)
                : new(ReadStatus.Invalid);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return new(ReadStatus.Missing);
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            return new(ReadStatus.Invalid);
        }
        catch (Exception error) when (IsStorageError(error))
        {
            return new(ReadStatus.Inaccessible);
        }
    }

    private bool IsValidDocument(SessionDocument document) =>
        document.FormatVersion == SessionDocument.CurrentFormatVersion && document.Game is not null &&
        validator.TryRestoreSave(document.Game, out _);

    private bool TryQuarantine(string path)
    {
        try
        {
            string preserved = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            File.Move(path, preserved, overwrite: false);
            return true;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return true; }
        catch (Exception error) when (IsStorageError(error)) { return false; }
    }

    private long ReadLegacyBestScore()
    {
        try
        {
            using var stream = new FileStream(Path.Combine(directory, "highscore.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 4096) return 0;
            return Math.Max(0, JsonSerializer.Deserialize<LegacyScore>(stream, JsonOptions)?.Score ?? 0);
        }
        catch (Exception error) when (IsStorageError(error)) { return 0; }
    }

    private static bool IsStorageError(Exception error) =>
        error is IOException or UnauthorizedAccessException or JsonException or SecurityException or NotSupportedException;

    private sealed record LegacyScore(long Score);
    private enum ReadStatus { Missing, Valid, Invalid, Inaccessible }
    private sealed record Candidate(ReadStatus Status, SessionDocument? Document = null);
}
