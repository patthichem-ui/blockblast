using System.Text.Json;

namespace BlockBlast.Application;

/// <summary>
/// Safe fallback when no writable user profile exists. Never redirects a save to the executable's directory.
/// Save reports that persistence is unavailable even though the current process retains the snapshot.
/// </summary>
public sealed class InMemoryGameRepository : IGameRepository
{
    private byte[]? snapshot;
    private const string Notice = "Chưa có thư mục dữ liệu người dùng. Tiến độ chỉ được giữ trong lần mở ứng dụng này.";

    public SessionLoadResult Load() => new(
        snapshot is null ? null : JsonSerializer.Deserialize<SessionDocument>(snapshot), Notice);

    public SessionSaveResult Save(SessionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        snapshot = JsonSerializer.SerializeToUtf8Bytes(document);
        return new(false, Notice);
    }
}
