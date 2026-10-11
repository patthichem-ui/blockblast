using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BlockBlast.Application;
using BlockBlast.Core;

namespace BlockBlast.Application.Tests;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        if (arguments is ["--resume-worker", var directory, var output])
            return ResumeWorker(directory, output);

        (string Name, Action Run)[] tests =
        [
            ("Restart restores board, tray, random continuation and undo in a fresh process", FreshProcessResume),
            ("Every successful move and undo persists without closing the window", AutomaticMoveSave),
            ("Settings survive restart, are normalized and have no mutable aliases", SettingsRoundTrip),
            ("Truncated primary recovers a valid backup and preserves corrupt evidence", CorruptPrimaryRecovery),
            ("Semantically invalid game recovers backup", InvalidGameRecovery),
            ("Unsupported and oversized save files fail safely", InvalidEnvelopeRecovery),
            ("First launch migrates previous highscore.json", LegacyMigration),
            ("Failed writes preserve a playable session and report failure", WriteFailure),
            ("Invalid snapshots never replace a known-good save", RejectInvalidWrite),
            ("Lifetime records survive undo and new games", LifetimeStatistics),
            ("Completion is counted once even after undo and repeat game over", CountCompletionOnce),
            ("Pause excludes elapsed time and prevents game actions", PauseAndElapsed),
            ("Subsecond active time survives restart; offline time is excluded", FractionalTimeResume),
            ("Memory fallback is isolated and reports lack of durable storage", MemoryFallback),
            ("Failed moves leave the last durable snapshot unchanged", FailedMoveDoesNotSave)
        ];

        int failures = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
            catch (Exception error) { failures++; Console.WriteLine($"FAIL {test.Name}: {error}"); }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} application test groups passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void FreshProcessResume()
    {
        using var directory = new TemporaryDirectory();
        var clock = new ManualClock();
        using var original = new GameSessionController(new JsonGameRepository(directory.Path), clock);
        original.Engine.Reset(72);
        Play(original, 4);
        original.UpdateSettings(new() { Theme = ThemeId.Candy, MusicEnabled = false, Volume = 31 });
        original.Save();

        string resultFile = System.IO.Path.Combine(directory.Path, "worker-result.json");
        using var process = new Process();
        process.StartInfo.FileName = Environment.ProcessPath ?? throw new InvalidOperationException("Process host is unavailable.");
        if (System.IO.Path.GetFileNameWithoutExtension(process.StartInfo.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            process.StartInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        process.StartInfo.ArgumentList.Add("--resume-worker");
        process.StartInfo.ArgumentList.Add(directory.Path);
        process.StartInfo.ArgumentList.Add(resultFile);
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        process.Start();
        True(process.WaitForExit(30_000), "Resume worker timed out.");
        Equal(0, process.ExitCode, process.StandardError.ReadToEnd());
        ContinueAfterResume(original);
        var resumed = JsonSerializer.Deserialize<WorkerResult>(File.ReadAllText(resultFile))!;
        Equal(Json(original.Engine.ExportSave()), Json(resumed.Game));
        Equal(Json(original.Statistics), Json(resumed.Statistics));
        Equal(Json(original.Settings), Json(resumed.Settings));
    }

    private static int ResumeWorker(string directory, string output)
    {
        try
        {
            using var controller = new GameSessionController(new JsonGameRepository(directory), new ManualClock());
            True(controller.HasRestoredGame);
            ContinueAfterResume(controller);
            File.WriteAllText(output, Json(new WorkerResult(controller.Engine.ExportSave(), controller.Statistics, controller.Settings)));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void ContinueAfterResume(GameSessionController controller)
    {
        Play(controller, 9);
        True(controller.Undo());
        Play(controller, 4);
    }

    private static void AutomaticMoveSave()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonGameRepository(directory.Path);
        using var session = new GameSessionController(repository, new ManualClock());
        Play(session, 2);
        Equal(Json(session.Engine.ExportSave()), Json(repository.Load().Document!.Game));
        True(session.Undo());
        Equal(Json(session.Engine.ExportSave()), Json(new JsonGameRepository(directory.Path).Load().Document!.Game));
        Equal(2, session.Engine.UndosRemaining);
        True(session.LastSaveSucceeded);
    }

    private static void SettingsRoundTrip()
    {
        using var directory = new TemporaryDirectory();
        using var session = new GameSessionController(new JsonGameRepository(directory.Path), new ManualClock());
        var choices = new PlayerSettings
        {
            Theme = ThemeId.Sunset, StickerPack = StickerPack.Garden, Volume = 63,
            MusicEnabled = false, SoundEnabled = false, EffectsEnabled = false, StickersEnabled = false
        };
        session.UpdateSettings(choices);
        choices.Volume = 1;
        var alias = session.Settings;
        alias.Theme = ThemeId.Aurora;
        Equal(63, session.Settings.Volume);
        Equal(ThemeId.Sunset, session.Settings.Theme);
        using var restored = new GameSessionController(new JsonGameRepository(directory.Path), new ManualClock());
        Equal(Json(session.Settings), Json(restored.Settings));
        restored.UpdateSettings(new() { Theme = (ThemeId)900, StickerPack = (StickerPack)(-1), Volume = 400 });
        Equal(ThemeId.Aurora, restored.Settings.Theme);
        Equal(StickerPack.Kawaii, restored.Settings.StickerPack);
        Equal(100, restored.Settings.Volume);
        restored.UpdateSettings(new() { Volume = -1 });
        Equal(0, restored.Settings.Volume);
    }

    private static void CorruptPrimaryRecovery()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonGameRepository(directory.Path);
        var engine = new GameEngine(4);
        var first = Document(engine);
        True(repository.Save(first).Success);
        PlaceFirst(engine);
        True(repository.Save(Document(engine)).Success);
        File.WriteAllText(directory.SavePath, "{\"FormatVersion\":1,\"Game\":");
        var recovered = repository.Load();
        Equal(Json(first.Game), Json(recovered.Document!.Game));
        True(recovered.Notice is not null);
        Equal(1, Directory.GetFiles(directory.Path, "session.json.corrupt-*").Length);
        True(repository.Save(recovered.Document).Success);
        True(File.Exists(directory.SavePath));
        True(File.Exists(directory.SavePath + ".bak"));
    }

    private static void InvalidGameRecovery()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonGameRepository(directory.Path);
        var engine = new GameEngine(5);
        var valid = Document(engine);
        True(repository.Save(valid).Success);
        PlaceFirst(engine);
        True(repository.Save(Document(engine)).Success);
        var invalid = Document(engine);
        invalid.Game!.State.TrayShapeIds[0] = "unknown-shape";
        File.WriteAllText(directory.SavePath, Json(invalid));
        Equal(Json(valid.Game), Json(repository.Load().Document!.Game));
    }

    private static void InvalidEnvelopeRecovery()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonGameRepository(directory.Path);
        var future = Document(new GameEngine(6));
        future.FormatVersion = 999;
        File.WriteAllText(directory.SavePath, Json(future));
        True(repository.Load().Document is null);
        using (var stream = new FileStream(directory.SavePath, FileMode.Create))
            stream.SetLength(JsonGameRepository.MaximumFileBytes + 1L);
        True(repository.Load().Document is null);
        Equal(2, Directory.GetFiles(directory.Path, "session.json.corrupt-*").Length);
        File.WriteAllText(directory.SavePath, "{\"Game\":{}}");
        True(repository.Load().Document is null);
    }

    private static void LegacyMigration()
    {
        using var directory = new TemporaryDirectory();
        string oldPath = System.IO.Path.Combine(directory.Path, "highscore.json");
        File.WriteAllText(oldPath, "{\"Score\":987654}");
        using var session = new GameSessionController(new JsonGameRepository(directory.Path), new ManualClock());
        Equal(987654L, session.Statistics.BestScore);
        True(!session.HasRestoredGame);
        session.NewGame();
        using var restored = new GameSessionController(new JsonGameRepository(directory.Path), new ManualClock());
        Equal(987654L, restored.Statistics.BestScore);
        True(File.Exists(oldPath));
    }

    private static void WriteFailure()
    {
        using var directory = new TemporaryDirectory();
        string blockedPath = System.IO.Path.Combine(directory.Path, "ordinary-file");
        File.WriteAllText(blockedPath, "preserve me");
        using var session = new GameSessionController(new JsonGameRepository(blockedPath), new ManualClock());
        True(!session.LastSaveSucceeded);
        Play(session, 1);
        True(session.Engine.Score > 0);
        True(!session.LastSaveSucceeded && session.SaveNotice is not null);
        Equal("preserve me", File.ReadAllText(blockedPath));
        Equal(0, Directory.GetFiles(directory.Path, "*.tmp").Length);
    }

    private static void RejectInvalidWrite()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonGameRepository(directory.Path);
        var valid = Document(new GameEngine(10));
        True(repository.Save(valid).Success);
        string before = File.ReadAllText(directory.SavePath);
        valid.Game!.State.Score = -1;
        True(!repository.Save(valid).Success);
        Equal(before, File.ReadAllText(directory.SavePath));
    }

    private static void LifetimeStatistics()
    {
        var repository = new SuccessfulMemoryRepository();
        using var session = new GameSessionController(repository, new ManualClock());
        Play(session, 2);
        var statistics = session.Statistics;
        Equal(2L, statistics.TotalPiecesPlaced);
        True(statistics.BestScore > 0);
        True(session.Undo());
        Equal(Json(statistics), Json(session.Statistics));
        session.Statistics.BestScore = 0;
        True(session.Statistics.BestScore > 0);
        session.NewGame();
        Equal(0L, session.Engine.Score);
        Equal(statistics.BestScore, session.Statistics.BestScore);
        Equal(2, session.Statistics.GamesPlayed);
        Equal(2L, session.Statistics.TotalPiecesPlaced);
    }

    private static void CountCompletionOnce()
    {
        var repository = new SuccessfulMemoryRepository();
        using var session = new GameSessionController(repository, new ManualClock());
        session.Engine.Reset(927);
        PlannedPlacement last = default;
        for (int move = 0; move < 500 && !session.Engine.IsGameOver; move++)
        {
            last = FirstPlacement(session.Engine);
            True(session.Place(last.TrayIndex, last.Row, last.Column).Success);
        }
        True(session.Engine.IsGameOver, "Expected deterministic first-fit game to end within 500 moves.");
        Equal(1, session.Statistics.GamesCompleted);
        True(session.Undo());
        True(session.Place(last.TrayIndex, last.Row, last.Column).IsGameOver);
        Equal(1, session.Statistics.GamesCompleted);
        using var restored = new GameSessionController(repository, new ManualClock());
        Equal(1, restored.Statistics.GamesCompleted);
    }

    private static void PauseAndElapsed()
    {
        var clock = new ManualClock();
        using var session = new GameSessionController(new SuccessfulMemoryRepository(), clock);
        clock.Advance(TimeSpan.FromSeconds(5.25));
        session.SetPaused(true);
        Equal(5L, session.Statistics.TotalPlaySeconds);
        string before = Json(session.Engine.ExportSave());
        clock.Advance(TimeSpan.FromHours(1));
        True(!session.Place(0, 0, 0).Success);
        True(!session.Undo());
        True(session.Hint() is null);
        session.Save();
        Equal(before, Json(session.Engine.ExportSave()));
        Equal(5L, session.Statistics.TotalPlaySeconds);
        session.SetPaused(false);
        clock.Advance(TimeSpan.FromSeconds(2.75));
        session.Save();
        Equal(8L, session.Statistics.TotalPlaySeconds);
    }

    private static void FractionalTimeResume()
    {
        using var directory = new TemporaryDirectory();
        var clock = new ManualClock();
        using (var session = new GameSessionController(new JsonGameRepository(directory.Path), clock))
        {
            clock.Advance(TimeSpan.FromMilliseconds(750));
            session.Save();
            Equal(0L, session.Statistics.TotalPlaySeconds);
        }
        clock.Advance(TimeSpan.FromDays(5));
        using var restored = new GameSessionController(new JsonGameRepository(directory.Path), clock);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        restored.Save();
        Equal(1L, restored.Statistics.TotalPlaySeconds);
        restored.SetPaused(true);
        using var paused = new GameSessionController(new JsonGameRepository(directory.Path), clock);
        True(paused.IsPaused);
    }

    private static void MemoryFallback()
    {
        var repository = new InMemoryGameRepository();
        var document = Document(new GameEngine(8));
        True(!repository.Save(document).Success);
        document.Game!.State.Score = 99;
        var firstRead = repository.Load();
        Equal(0L, firstRead.Document!.Game!.State.Score);
        firstRead.Document.Game.State.Score = 123;
        Equal(0L, repository.Load().Document!.Game!.State.Score);
        True(repository.Load().Notice is not null);
    }

    private static void FailedMoveDoesNotSave()
    {
        using var directory = new TemporaryDirectory();
        using var session = new GameSessionController(new JsonGameRepository(directory.Path), new ManualClock());
        string before = File.ReadAllText(directory.SavePath);
        True(!session.Place(100, 100, 100).Success);
        Equal(before, File.ReadAllText(directory.SavePath));
    }

    private static SessionDocument Document(GameEngine engine) => new()
    {
        FormatVersion = SessionDocument.CurrentFormatVersion,
        Game = engine.ExportSave(), Settings = new(), Statistics = new() { GamesPlayed = 1 },
        SavedAtUtc = DateTimeOffset.UtcNow
    };

    private static PlannedPlacement FirstPlacement(GameEngine engine)
    {
        for (int index = 0; index < engine.Tray.Count; index++)
        {
            if (engine.Tray[index] is not { } shape) continue;
            for (int row = 0; row < Board.Size; row++)
            for (int column = 0; column < Board.Size; column++)
                if (engine.Board.CanPlace(shape, row, column)) return new(index, row, column);
        }
        throw new InvalidOperationException("No legal move exists.");
    }

    private static void PlaceFirst(GameEngine engine)
    {
        var move = FirstPlacement(engine);
        True(engine.TryPlace(move.TrayIndex, move.Row, move.Column).Success);
    }

    private static void Play(GameSessionController session, int count)
    {
        for (int index = 0; index < count; index++)
        {
            var move = FirstPlacement(session.Engine);
            True(session.Place(move.TrayIndex, move.Row, move.Column).Success);
        }
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static void True(bool condition, string? message = null)
    {
        if (!condition) throw new InvalidOperationException(message ?? "Expected true.");
    }

    private static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message ?? $"Expected {expected}, actual {actual}.");
    }

    private sealed record WorkerResult(GameSaveData Game, PlayerStatistics Statistics, PlayerSettings Settings);

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(ticks);
        public void Advance(TimeSpan elapsed) => ticks += elapsed.Ticks;
    }

    private sealed class SuccessfulMemoryRepository : IGameRepository
    {
        private byte[]? snapshot;
        public SessionLoadResult Load() => new(snapshot is null ? null : JsonSerializer.Deserialize<SessionDocument>(snapshot));
        public SessionSaveResult Save(SessionDocument document)
        {
            snapshot = JsonSerializer.SerializeToUtf8Bytes(document);
            return new(true);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"blockblast-tests-{Guid.NewGuid():N}");
        public string SavePath => System.IO.Path.Combine(Path, JsonGameRepository.SaveFileName);
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
