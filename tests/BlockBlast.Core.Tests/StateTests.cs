using System.Text.Json;
using BlockBlast.Core;

internal static class StateTests
{
    private static readonly string SingleId = ShapeCatalog.All.Single(shape => shape.CellCount == 1).Id;

    public static IEnumerable<(string Name, Action Test)> Cases => new (string, Action)[]
    {
        ("JSON saves preserve consumed tray slots and deterministic future generations", SaveContinuation),
        ("Undo restores a refill and its random stream, including after reopening", UndoAcrossRefill),
        ("Undo allowance is capped, persisted, and renewed only by a new game", UndoAllowance),
        ("Hints use current tray indexes without changing board, history, or randomness", HintPurity),
        ("A legal fallback hint and undo work when the remaining tray becomes blocked", UndoGameOver),
        ("Move effects report distinct simultaneous clear cells and stable shape identities", EffectCells),
        ("Malformed saves and undo histories are rejected without partially restoring state", InvalidSaves),
        ("Exported and imported save arrays never alias live game state", SnapshotIsolation),
        ("Direct diagnostic board placements receive stable fallback identities on save", DiagnosticIdentity)
    };

    private static void SaveContinuation()
    {
        var source = new GameEngine(1605);
        for (int step = 0; step < 4; step++) PlayHint(source);
        var encoded = JsonSerializer.Serialize(source.ExportSave());
        var restored = new GameEngine(99);
        Check(restored.TryRestoreSave(JsonSerializer.Deserialize<GameSaveData>(encoded)!, out var error), error ?? "Restore failed.");
        Equal(encoded, Signature(restored), "JSON round trip");
        Equal(2, restored.Tray.Count(shape => shape is not null), "Consumed tray slot should remain consumed");
        Check(!restored.LastGeneration.HasCompleteSolution, "Restore invented a certificate for the current tray.");

        int refills = 0;
        for (int step = 0; step < 60; step++)
        {
            var planned = source.FindHint();
            Check(planned is not null, "Generated game unexpectedly ran out of a certified continuation.");
            Equal(planned, restored.FindHint(), "Hint after restore");
            var move = planned!.Value;
            var left = source.TryPlace(move.TrayIndex, move.Row, move.Column);
            var right = restored.TryPlace(move.TrayIndex, move.Row, move.Column);
            Check(left.Success && right.Success, "Continuation rejected a legal move.");
            if (left.TrayRefilled) refills++;
            Equal(Signature(source), Signature(restored), "Continuation state after refill or move");
        }
        Check(refills >= 15, "Did not test enough generated trays after restoration.");
    }

    private static void UndoAcrossRefill()
    {
        var game = new GameEngine(542);
        PlayHint(game);
        PlayHint(game);
        string before = StateSignature(game);
        var planned = game.FindHint()!.Value;
        var result = game.TryPlace(planned.TrayIndex, planned.Row, planned.Column);
        Check(result.Success && result.TrayRefilled, "Third move did not refill.");
        string after = StateSignature(game);
        var reopened = new GameEngine(3);
        Check(reopened.TryRestoreSave(Clone(game.ExportSave()), out _), "Cannot reopen a game with undo history.");
        Check(reopened.TryUndo(), "Reopened game forgot its undo history.");
        Equal(before, StateSignature(reopened), "Undo must restore the old tray, score, identities and RNG");
        Equal(2, reopened.UndosRemaining, "Undo must consume one allowance");
        result = reopened.TryPlace(planned.TrayIndex, planned.Row, planned.Column);
        Check(result.Success && result.TrayRefilled, "Replaying an undone refill failed.");
        Equal(after, StateSignature(reopened), "Replaying an undone move must regenerate the same tray");
    }

    private static void UndoAllowance()
    {
        var game = new GameEngine(671);
        Guid id = game.GameId;
        Check(!game.CanUndo && !game.TryUndo(), "A new game can undo.");
        for (int step = 0; step < 7; step++) PlayHint(game);
        Equal(3, game.ExportSave().UndoHistory.Count, "History memory bound");
        for (int count = 2; count >= 0; count--)
        {
            Check(game.TryUndo(), "Available undo failed.");
            Equal(count, game.UndosRemaining, "Remaining allowance");
            var next = new GameEngine(0);
            Check(next.TryRestoreSave(Clone(game.ExportSave()), out _), "Undo state failed to reopen.");
            game = next;
        }
        string exhausted = Signature(game);
        Check(!game.CanUndo && !game.TryUndo(), "The fourth undo was accepted.");
        Equal(exhausted, Signature(game), "A rejected undo mutated state");
        PlayHint(game);
        Check(!game.CanUndo && game.ExportSave().UndoHistory.Count == 0, "A new move replenished spent undos.");
        game.Reset(671);
        Check(game.GameId != id && game.GameId != Guid.Empty, "New game retained its old ID.");
        Equal(3, game.UndosRemaining, "New game undo allowance");
        Check(!game.CanUndo, "Reset retained old history.");
    }

    private static void HintPurity()
    {
        var game = new GameEngine(317);
        PlayHint(game);
        string before = Signature(game);
        PlannedPlacement? first = null;
        for (int index = 0; index < 6; index++)
        {
            var hint = game.FindHint();
            Check(hint is not null, "Partly consumed tray has no hint.");
            var planned = hint!.Value;
            Check(game.Tray[planned.TrayIndex] is { } shape && game.Board.CanPlace(shape, planned.Row, planned.Column),
                "Hint referred to a consumed slot or illegal anchor.");
            if (index > 0) Equal(first, hint, "Repeated hint");
            first = hint;
        }
        Equal(before, Signature(game), "Hints mutated the game or generator state");
    }

    private static void UndoGameOver()
    {
        var game = MakeBlockedTrayScenario();
        string before = StateSignature(game);
        var hint = game.FindHint();
        Check(hint is { TrayIndex: 2 }, "Fallback must choose the only legal singleton slot.");
        var planned = hint!.Value;
        var result = game.TryPlace(planned.TrayIndex, planned.Row, planned.Column);
        Check(result.Success && result.IsGameOver && game.IsGameOver, "Expected remaining large shapes to be blocked.");
        Check(game.FindHint() is null, "Game over returned an illegal hint.");
        var restored = new GameEngine();
        Check(restored.TryRestoreSave(Clone(game.ExportSave()), out _), "Game-over save failed to reopen.");
        Check(restored.IsGameOver, "Restore failed to recompute game over.");
        Check(restored.TryUndo() && !restored.IsGameOver, "Undo did not revive a game-over move.");
        Equal(before, StateSignature(restored), "Game-over undo state");
    }

    private static void EffectCells()
    {
        var game = new GameEngine(58);
        var save = game.ExportSave();
        save.State.TrayShapeIds = [SingleId, SingleId, SingleId];
        for (int index = 0; index < Board.Size - 1; index++)
        {
            SetCell(save.State, 7, index, SingleId);
            SetCell(save.State, index, 7, SingleId);
        }
        Check(game.TryRestoreSave(save, out var error), error ?? "Prepared board failed to restore.");
        var result = game.TryPlace(0, 7, 7);
        Check(result.Success, "Intersection move failed.");
        Equal(2, result.LinesCleared, "Simultaneous line count");
        Equal(1, result.PlacedCells.Count, "Placed effect cells");
        Equal(new Cell(7, 7), result.PlacedCells[0], "Placed anchor");
        Equal(15, result.ClearedCells.Count, "Intersection counted once in effect cells");
        Equal(15, result.ClearedCells.Distinct().Count(), "Repeated cleared effect cell");
        Equal(SingleId, result.ShapeId, "Moved shape identity");
        Check(result.ClearedCells.All(cell => cell.Row == 7 || cell.Column == 7), "Clear animation includes an unrelated cell.");
        Check(result.ClearedCells.All(cell => game.GetCellShapeId(cell.Row, cell.Column) is null), "Cleared cells retained stickers.");
        Check(game.TryUndo(), "Effect move undo failed.");
        Equal(SingleId, game.GetCellShapeId(7, 0), "Undo restored cell identity");

        game.Reset(812);
        var hint = game.FindHint()!.Value;
        var shape = game.Tray[hint.TrayIndex]!;
        result = game.TryPlace(hint.TrayIndex, hint.Row, hint.Column);
        foreach (var cell in result.PlacedCells)
            Equal(shape.Id, game.GetCellShapeId(cell.Row, cell.Column), "Placed cell identity");
        var reopened = new GameEngine(1);
        Check(reopened.TryRestoreSave(Clone(game.ExportSave()), out _), "Identity save failed.");
        foreach (var cell in result.PlacedCells)
            Equal(shape.Id, reopened.GetCellShapeId(cell.Row, cell.Column), "Reopened cell identity");
    }

    private static void InvalidSaves()
    {
        var game = new GameEngine(44);
        for (int step = 0; step < 4; step++) PlayHint(game);
        string before = Signature(game);
        var original = game.ExportSave();
        Action<GameSaveData>[] corruptions =
        [
            data => data.SchemaVersion = 0,
            data => data.SchemaVersion = 200,
            data => data.State = null!,
            data => data.State.GameId = Guid.Empty,
            data => data.State.RandomState = 0,
            data => data.State.Score = -1,
            data => data.State.Combo = -1,
            data => data.State.TotalLinesCleared = -1,
            data => data.State.PiecesPlaced = -1,
            data => data.State.Combo = int.MaxValue,
            data => data.State.TrayShapeIds = null!,
            data => data.State.TrayShapeIds = [],
            data => data.State.TrayShapeIds = [SingleId, SingleId, SingleId, SingleId],
            data => data.State.TrayShapeIds = [null, null, null],
            data => data.State.TrayShapeIds = ["not-a-shape", SingleId, null],
            data => data.State.TrayShapeIds = [SingleId, SingleId, SingleId],
            data => data.State.CellShapeIds = null!,
            data => data.State.CellShapeIds = new string?[63],
            data => data.State.CellShapeIds[0] = "not-a-shape",
            data => { data.State.BoardBits = 0xff; data.State.CellShapeIds = Enumerable.Range(0, 64).Select(i => i < 8 ? SingleId : null).ToArray(); },
            data => { data.State.BoardBits = 0x0101010101010101; data.State.CellShapeIds = Enumerable.Range(0, 64).Select(i => i % 8 == 0 ? SingleId : null).ToArray(); },
            data => data.UndosRemaining = -1,
            data => data.UndosRemaining = 4,
            data => data.UndosRemaining = 2,
            data => data.UndoHistory = null!,
            data => data.UndoHistory.Add(Clone(original).State),
            data => data.UndoHistory[0] = null!,
            data => data.UndoHistory[0].GameId = Guid.NewGuid(),
            data => data.UndoHistory[0].Seed++,
            data => data.UndoHistory[0].RandomState = 0,
            data => data.UndoHistory[0].Score = data.State.Score + 100,
            data => data.UndoHistory[0].CellShapeIds = [],
            data => data.UndoHistory.Reverse()
        ];
        foreach (var corrupt in corruptions)
        {
            var save = Clone(original);
            corrupt(save);
            Check(!game.TryRestoreSave(save, out var error), "Malformed save was accepted.");
            Check(!string.IsNullOrWhiteSpace(error), "Rejected save did not explain the invalid data.");
            Equal(before, Signature(game), "Malformed restore partially replaced live state");
        }
        Check(!game.TryRestoreSave(null!, out _), "Null save was accepted.");
        Equal(before, Signature(game), "Null restore mutated state");
    }

    private static void SnapshotIsolation()
    {
        var game = new GameEngine(205);
        for (int step = 0; step < 4; step++) PlayHint(game);
        string before = Signature(game);
        var exported = game.ExportSave();
        exported.State.TrayShapeIds[0] = "invalid";
        exported.State.CellShapeIds[0] = "invalid";
        exported.UndoHistory[0].CellShapeIds[0] = "invalid";
        exported.UndoHistory[0].TrayShapeIds[0] = "invalid";
        exported.UndoHistory.Clear();
        Equal(before, Signature(game), "Exported lists alias engine storage");

        var incoming = game.ExportSave();
        var restored = new GameEngine(501);
        Check(restored.TryRestoreSave(incoming, out _), "Valid save failed to restore.");
        incoming.State.CellShapeIds[0] = "invalid";
        incoming.State.TrayShapeIds[0] = "invalid";
        incoming.UndoHistory[0].CellShapeIds[0] = "invalid";
        incoming.UndoHistory[0].TrayShapeIds[0] = "invalid";
        incoming.UndoHistory.Clear();
        Equal(before, Signature(restored), "Imported lists alias caller storage");
        Check(restored.TryUndo(), "Caller mutated imported undo history.");
    }

    private static void DiagnosticIdentity()
    {
        var game = new GameEngine(44);
        var single = new BlockShape("diagnostic-cell", "Diagnostic", [new Cell(0, 0)]);
        game.Board.PlaceAndClear(single, 7, 7);
        Equal(SingleId, game.GetCellShapeId(7, 7), "Direct placement fallback");
        var save = game.ExportSave();
        Equal(SingleId, save.State.CellShapeIds[63], "Save fallback");
        var restored = new GameEngine(42);
        Check(restored.TryRestoreSave(save, out var error), error ?? "Direct board edits should export a usable snapshot.");
        Equal(SingleId, restored.GetCellShapeId(7, 7), "Restored fallback");
    }

    private static GameEngine MakeBlockedTrayScenario()
    {
        var game = new GameEngine(118);
        var save = game.ExportSave();
        save.State.TrayShapeIds = ["square-3-r0", "square-3-r0", SingleId];
        for (int row = 0; row < Board.Size; row++)
        for (int column = 0; column < Board.Size; column++)
            if ((row + column) % 2 == 0) SetCell(save.State, row, column, SingleId);
        Check(game.TryRestoreSave(save, out var error), error ?? "Scenario failed to restore.");
        return game;
    }

    private static void SetCell(GameStateSnapshot state, int row, int column, string id)
    {
        int index = row * Board.Size + column;
        state.BoardBits |= 1UL << index;
        state.CellShapeIds[index] = id;
    }

    private static void PlayHint(GameEngine game)
    {
        var hint = game.FindHint();
        Check(hint is not null, "No legal move for scenario.");
        var planned = hint!.Value;
        Check(game.TryPlace(planned.TrayIndex, planned.Row, planned.Column).Success, "Hint could not be played.");
    }

    private static GameSaveData Clone(GameSaveData save) => JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(save))!;
    private static string Signature(GameEngine game) => JsonSerializer.Serialize(game.ExportSave());
    private static string StateSignature(GameEngine game) => JsonSerializer.Serialize(game.ExportSave().State);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string context)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{context}: expected {expected}, got {actual}.");
    }
}
