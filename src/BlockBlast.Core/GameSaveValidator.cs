namespace BlockBlast.Core;

/// <summary>Rejects malformed data before any live engine state is replaced.</summary>
internal sealed class GameSaveValidator(IReadOnlyDictionary<string, BlockShape> shapes)
{
    public bool TryValidate(GameSaveData? save, out string? error)
    {
        error = null;
        if (save is null || save.SchemaVersion != GameSaveData.CurrentSchemaVersion)
            return Fail("Phiên bản dữ liệu lưu không được hỗ trợ.", out error);
        if (save.UndosRemaining < 0 || save.UndosRemaining > GameEngine.MaximumUndos)
            return Fail("Số lượt hoàn tác không hợp lệ.", out error);
        if (save.UndoHistory is null || save.UndoHistory.Count > save.UndosRemaining)
            return Fail("Lịch sử hoàn tác không hợp lệ.", out error);
        if (!TryValidateState(save.State, out error)) return false;

        GameStateSnapshot? previous = null;
        foreach (var snapshot in save.UndoHistory)
        {
            if (!TryValidateState(snapshot, out error)) return false;
            if (snapshot.GameId != save.State.GameId || snapshot.Seed != save.State.Seed)
                return Fail("Lịch sử hoàn tác thuộc một ván chơi khác.", out error);
            if (previous is not null && !IsNextMove(previous, snapshot))
                return Fail("Các bước hoàn tác không liên tiếp.", out error);
            previous = snapshot;
        }
        if (previous is not null && !IsNextMove(previous, save.State))
            return Fail("Lịch sử hoàn tác không khớp tiến độ hiện tại.", out error);
        return true;
    }

    private bool TryValidateState(GameStateSnapshot? state, out string? error)
    {
        error = null;
        if (state is null || state.GameId == Guid.Empty || state.RandomState == 0)
            return Fail("Trạng thái ván chơi hoặc bộ sinh khối không hợp lệ.", out error);
        if (state.Score < 0 || state.Combo < 0 || state.TotalLinesCleared < 0 || state.PiecesPlaced < 0
            || state.Combo > state.PiecesPlaced || state.Combo > state.TotalLinesCleared
            || state.TotalLinesCleared > 16L * state.PiecesPlaced
            || state.Score < 5L * state.PiecesPlaced || (state.PiecesPlaced == 0 && state.Score != 0))
            return Fail("Điểm hoặc thống kê ván chơi không hợp lệ.", out error);
        if (state.TrayShapeIds is null || state.TrayShapeIds.Length != PieceGenerator.TraySize
            || state.TrayShapeIds.All(id => id is null)
            || state.TrayShapeIds.Any(id => id is not null && !shapes.ContainsKey(id)))
            return Fail("Các khối trong khay không hợp lệ.", out error);
        int consumedSlots = state.TrayShapeIds.Count(id => id is null);
        if (state.PiecesPlaced < int.MaxValue && consumedSlots != state.PiecesPlaced % PieceGenerator.TraySize)
            return Fail("Khay khối không khớp số bước đã chơi.", out error);
        if (state.CellShapeIds is null || state.CellShapeIds.Length != Board.Size * Board.Size)
            return Fail("Dữ liệu màu của bàn chơi không hợp lệ.", out error);

        Board.ClearCompletedLines(state.BoardBits, out int fullRows, out int fullColumns, out _);
        if (fullRows != 0 || fullColumns != 0)
            return Fail("Bàn chơi chứa hàng hoặc cột đầy chưa được xóa.", out error);
        for (int index = 0; index < state.CellShapeIds.Length; index++)
        {
            bool occupied = (state.BoardBits & (1UL << index)) != 0;
            string? identity = state.CellShapeIds[index];
            if (occupied ? identity is null || !shapes.ContainsKey(identity) : identity is not null)
                return Fail("Danh tính khối không khớp các ô trên bàn.", out error);
        }
        return true;
    }

    private static bool IsNextMove(GameStateSnapshot before, GameStateSnapshot after)
    {
        if (after.PiecesPlaced != Math.Min(int.MaxValue - 1, before.PiecesPlaced) + 1
            || after.Score < before.Score || after.Score - before.Score > int.MaxValue
            || (after.Score == before.Score && before.Score != long.MaxValue)
            || after.TotalLinesCleared < before.TotalLinesCleared
            || (long)after.TotalLinesCleared - before.TotalLinesCleared > Board.Size * 2)
            return false;

        // Statistics can saturate; at that boundary the board remains authoritative.
        if (before.TotalLinesCleared < int.MaxValue)
        {
            int expectedCombo = after.TotalLinesCleared > before.TotalLinesCleared
                ? Math.Min(int.MaxValue - 1, before.Combo) + 1 : 0;
            if (after.Combo != expectedCombo) return false;
        }

        // Consuming a slot without refilling must not consume generator randomness.
        int remainingBefore = before.TrayShapeIds.Count(id => id is not null);
        if (remainingBefore > 1)
        {
            if (after.RandomState != before.RandomState) return false;
            int consumed = 0;
            for (int index = 0; index < PieceGenerator.TraySize; index++)
            {
                if (before.TrayShapeIds[index] == after.TrayShapeIds[index]) continue;
                if (before.TrayShapeIds[index] is null || after.TrayShapeIds[index] is not null) return false;
                consumed++;
            }
            if (consumed != 1) return false;
        }
        else if (after.TrayShapeIds.Any(id => id is null)) return false;
        return true;
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }
}
