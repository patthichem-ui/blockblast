using System.Diagnostics;
using BlockBlast.Core;

internal static class Program
{
    private static int Main()
    {
        // A dependency-free executable suite: a failed assertion makes the process exit nonzero.
        var suite = new (string Name, Action Test)[]
        {
            ("Shape catalog is normalized, unique, connected, and fits the board", Tests.ShapeCatalogIntegrity),
            ("Custom shapes normalize coordinates and reject invalid definitions", Tests.ShapeConstruction),
            ("Placement respects edges, collisions, cloning, and atomic failures", Tests.PlacementRules),
            ("Rows and columns clear simultaneously without counting intersections twice", Tests.SimultaneousClearing),
            ("Scoring rewards placement, multiple lines, combo, and difficulty", Tests.Scoring),
            ("Difficulty progresses monotonically and stays within its limits", Tests.Difficulty),
            ("Tray solver proves a full sequence and rejects impossible trays", Tests.Solver),
            ("Generated trays have replayable solutions on fragmented boards", Tests.GeneratedTrayCertificates),
            ("Exhausting a tiny search budget still produces a playable fallback tray", Tests.FallbackCertificates),
            ("Generation is reproducible with a fixed seed", Tests.SeededGeneration),
            ("Tray slots are consumed once and refill after the third piece", Tests.TrayLifecycle),
            ("Invalid moves preserve every part of game state", Tests.InvalidMoves),
            ("Clearing moves extend combo and a quiet move resets it", Tests.ComboLifecycle),
            ("The move crossing a difficulty threshold uses its previous level", Tests.ScoreThreshold),
            ("A blocked remaining tray ends the game and reset starts a fresh game", Tests.GameOver),
            ("Seeded gameplay reproduces board, tray, score, and progression", Tests.SeededReplay)
        }.Concat(StateTests.Cases).ToArray();

        int passed = 0;
        var elapsed = Stopwatch.StartNew();
        foreach (var (name, test) in suite)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"FAIL {name}: {exception}");
            }
        }
        Console.WriteLine($"{passed}/{suite.Length} tests passed in {elapsed.Elapsed.TotalSeconds:F2}s.");
        return passed == suite.Length ? 0 : 1;
    }
}

internal static class Tests
{
    private static readonly BlockShape Single = new("test-single", "Single", [new Cell(0, 0)]);

    public static void ShapeCatalogIntegrity()
    {
        Check(ShapeCatalog.All.Count >= 30, "Catalog should include a useful set of rotated pieces.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var geometries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shape in ShapeCatalog.All)
        {
            Check(identities.Add(shape.Id), $"Duplicate shape ID {shape.Id}.");
            Check(geometries.Add(Signature(shape)), $"Duplicate geometry {shape.Id}.");
            Equal(0, shape.Cells.Min(cell => cell.Row), "Normalized minimum row");
            Equal(0, shape.Cells.Min(cell => cell.Column), "Normalized minimum column");
            Equal(shape.Width, shape.Cells.Max(cell => cell.Column) + 1, "Shape width");
            Equal(shape.Height, shape.Cells.Max(cell => cell.Row) + 1, "Shape height");
            Equal(shape.CellCount, shape.Cells.Distinct().Count(), "Distinct shape cells");
            Check(shape.Width <= Board.Size && shape.Height <= Board.Size, "Shape must fit.");
            Check(IsConnected(shape), $"Disconnected catalog shape {shape.Id}.");
            var board = new Board();
            int row = Board.Size - shape.Height;
            int column = Board.Size - shape.Width;
            Check(board.CanPlace(shape, row, column), $"Cannot place {shape.Id} at its last legal anchor.");
            Check(!board.CanPlace(shape, row + 1, column), "Bottom overflow accepted.");
            Check(!board.CanPlace(shape, row, column + 1), "Right overflow accepted.");
        }
    }

    public static void ShapeConstruction()
    {
        var translated = new BlockShape("translated", "Translated", [new Cell(-4, 9), new Cell(-3, 9), new Cell(-3, 10)]);
        Equal("0,0;1,0;1,1", Signature(translated), "Translated shape geometry");
        Equal(2, translated.Width, "Translated width");
        Equal(2, translated.Height, "Translated height");
        Throws<ArgumentException>(() => new BlockShape("empty", "Empty", []));
        Throws<ArgumentException>(() => new BlockShape("duplicate", "Duplicate", [new Cell(0, 0), new Cell(0, 0)]));
        Throws<ArgumentException>(() => new BlockShape("wide", "Wide", [new Cell(0, 0), new Cell(0, 8)]));
        Throws<ArgumentException>(() => new BlockShape("huge", "Huge", [new Cell(int.MinValue, 0), new Cell(int.MaxValue, 0)]));
    }

    public static void PlacementRules()
    {
        var board = new Board();
        var square = ShapeCatalog.All.Single(shape => shape.Id == "square-2-r0");
        Check(!board.CanPlace(square, -1, 0) && !board.CanPlace(square, 0, -1), "Negative anchor accepted.");
        Check(board.CanPlace(square, 6, 6), "Bottom-right square should fit.");
        var placed = board.PlaceAndClear(square, 6, 6);
        Equal(4, placed.CellsPlaced, "Placed cells");
        Equal(4, board.OccupiedCount, "Occupied cells");
        Equal(0, placed.LinesCleared, "Unexpected clear");
        Check(!board.CanPlace(Single, 7, 7), "Overlap accepted.");
        string before = BoardSignature(board);
        Throws<InvalidOperationException>(() => board.PlaceAndClear(Single, 7, 7));
        Equal(before, BoardSignature(board), "Failed overlap mutated board");
        Throws<InvalidOperationException>(() => board.PlaceAndClear(square, 7, 7));
        Equal(before, BoardSignature(board), "Failed out-of-bounds move mutated board");
        var clone = board.Clone();
        clone.PlaceAndClear(Single, 0, 0);
        Check(!board.IsOccupied(0, 0) && clone.IsOccupied(0, 0), "Clone shares mutable board state.");
        Throws<ArgumentOutOfRangeException>(() => board.IsOccupied(8, 0));
    }

    public static void SimultaneousClearing()
    {
        var board = new Board();
        board.PlaceAndClear(Single, 4, 4);
        for (int index = 1; index < Board.Size; index++)
        {
            board.PlaceAndClear(Single, 0, index);
            board.PlaceAndClear(Single, index, 0);
        }
        var cleared = board.PlaceAndClear(Single, 0, 0);
        Equal(2, cleared.LinesCleared, "Simultaneous row/column count");
        Equal(15, cleared.CellsCleared, "The intersection must count only once");
        Check(cleared.RowsCleared.SequenceEqual([0]), "Wrong cleared row.");
        Check(cleared.ColumnsCleared.SequenceEqual([0]), "Wrong cleared column.");
        Equal(1, board.OccupiedCount, "Unrelated cell was lost");
        Check(board.IsOccupied(4, 4), "Unrelated cell must survive.");

        var fullBoard = new BlockShape("full-board", "Full board", AllCells());
        var fullClear = new Board().PlaceAndClear(fullBoard, 0, 0);
        Equal(16, fullClear.LinesCleared, "Eight rows plus eight columns");
        Equal(64, fullClear.CellsCleared, "Every intersection should count once");
    }

    public static void Scoring()
    {
        Equal(20, ScoreCalculator.Calculate(4, 0, 0, 1), "Placement points");
        Equal(120, ScoreCalculator.Calculate(4, 1, 1, 1), "Single-line clear");
        Equal(420, ScoreCalculator.Calculate(4, 2, 1, 1), "Two-line clear bonus");
        Equal(420, ScoreCalculator.Calculate(4, 1, 3, 2), "Combo and level bonus");
        Equal(20, ScoreCalculator.Calculate(4, 0, 12, 20), "Quiet placement has no bonus");
        Equal(int.MaxValue, ScoreCalculator.Calculate(64, 16, int.MaxValue, 20), "Points must saturate rather than wrap");
        Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.Calculate(1, 1, 0, 1));
        Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.Calculate(0, 0, 0, 1));
    }

    public static void Difficulty()
    {
        Equal(1, DifficultyProfile.ForProgress(0, 0, 0).Level, "Initial level");
        Equal(1, DifficultyProfile.ForProgress(2499, 9, 44).Level, "Before all first thresholds");
        Equal(2, DifficultyProfile.ForProgress(2500, 0, 0).Level, "Score progression");
        Equal(2, DifficultyProfile.ForProgress(0, 10, 0).Level, "Line progression");
        Equal(2, DifficultyProfile.ForProgress(0, 0, 45).Level, "Piece progression");
        Equal(20, DifficultyProfile.ForProgress(long.MaxValue, int.MaxValue, int.MaxValue).Level, "Maximum level");
        var previous = DifficultyProfile.ForProgress(0, 0, 0);
        for (int step = 1; step <= 100; step++)
        {
            var current = DifficultyProfile.ForProgress(step * 1000L, step * 2, step * 9);
            Check(current.Level >= previous.Level && current.Level <= 20, "Level regressed or exceeded cap.");
            Check(current.TargetCellCount >= previous.TargetCellCount, "Difficulty reduced the size target.");
            Check(current.CandidateTrays > 0 && current.SolverNodeBudget > 0 && current.TotalSearchBudget > 0, "Search has no finite budget.");
            previous = current;
        }
        Throws<ArgumentOutOfRangeException>(() => DifficultyProfile.ForProgress(-1, 0, 0));
    }

    public static void Solver()
    {
        var board = new Board();
        var fullBoard = new BlockShape("full-board", "Full board", AllCells());
        BlockShape[] tray = [fullBoard, fullBoard, Single];
        var solver = new TraySolver();
        string before = BoardSignature(board);
        Check(solver.TrySolve(board, tray, out var solution, out var statistics), "A clear-and-reuse sequence was missed.");
        ReplayCertificate(board, tray, solution);
        Equal(before, BoardSignature(board), "Solver mutated its input");
        Check(statistics.VisitedStates > 0, "Solver did no search.");

        board.PlaceAndClear(Single, 0, 0);
        Check(!solver.TrySolve(board, [fullBoard, fullBoard, fullBoard], out var impossible, out _), "Impossible tray was marked solvable.");
        Equal(0, impossible.Count, "A failed search must not expose a partial certificate");
        Equal(1, board.OccupiedCount, "Failed search mutated its input");

        board = new Board();
        for (int column = 1; column < Board.Size; column++) board.PlaceAndClear(Single, 0, column);
        Check(!board.HasAnyPlacement(fullBoard), "Large shape should start blocked.");
        BlockShape[] clearFirstTray = [fullBoard, Single];
        Check(solver.TrySolve(board, clearFirstTray, out var clearFirst), "Solver pruned a shape that becomes available after a clear.");
        Equal(1, clearFirst[0].TrayIndex, "The singleton must clear space first");
        ReplayCertificate(board, clearFirstTray, clearFirst);

        var bounded = new TraySolver(1);
        Check(!bounded.TrySolve(new Board(), [Single, Single, Single], out var partial, out var limit), "One state cannot prove a three-move solution.");
        Check(limit.HitLimit && limit.VisitedStates == 1, "Search must honor its node limit.");
        Equal(0, partial.Count, "Limited search must not expose a partial certificate");
    }

    public static void GeneratedTrayCertificates()
    {
        var random = new Random(809);
        int cases = 0;
        foreach (double density in new[] { 0.25, 0.55, 0.80 })
        foreach (int level in new[] { 1, 10, 20 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            var board = FragmentedBoard(random, density);
            string before = BoardSignature(board);
            var profile = DifficultyProfile.ForProgress(0, (level - 1) * 10, 0);
            var generator = new PieceGenerator(1000 + cases++);
            var tray = generator.Generate(board, profile);
            Equal(3, tray.Count, "Tray size on a legal board");
            Check(generator.LastReport.HasCompleteSolution, "Generated tray lacks a complete solution certificate.");
            ReplayCertificate(board, tray, generator.LastReport.Solution);
            Equal(before, BoardSignature(board), "Generator mutated its input");
            Check(generator.LastReport.Attempts <= profile.CandidateTrays, "Candidate search exceeded its budget.");
            Check(generator.LastReport.SolverStatesVisited <= profile.TotalSearchBudget, "Solver exceeded the total search budget.");
        }
        Equal(18, cases, "Fragmented-board case count");
    }

    public static void FallbackCertificates()
    {
        var profile = DifficultyProfile.ForProgress(0, 0, 0) with
        {
            CandidateTrays = 1,
            SolverNodeBudget = 1,
            TotalSearchBudget = 1
        };
        foreach (int seed in new[] { 7, 101, 812 })
        {
            var board = FragmentedBoard(new Random(seed), 0.78);
            string before = BoardSignature(board);
            var generator = new PieceGenerator(seed);
            var tray = generator.Generate(board, profile);
            Check(generator.LastReport.UsedFallback, "Tiny budget did not exercise fallback.");
            Check(generator.LastReport.HasCompleteSolution, "Fallback lacks a solution.");
            Equal(3, tray.Count, "Fallback tray size");
            Equal(1, generator.LastReport.Attempts, "Fallback attempt budget");
            Equal(1, generator.LastReport.SolverStatesVisited, "Fallback state budget");
            ReplayCertificate(board, tray, generator.LastReport.Solution);
            Equal(before, BoardSignature(board), "Fallback mutated the input board");
        }
    }

    public static void SeededGeneration()
    {
        var board = FragmentedBoard(new Random(511), 0.6);
        var profile = DifficultyProfile.ForProgress(9000, 22, 120);
        var first = new PieceGenerator(73);
        var second = new PieceGenerator(73);
        for (int iteration = 0; iteration < 12; iteration++)
        {
            var a = first.Generate(board, profile);
            var b = second.Generate(board, profile);
            Equal(string.Join('|', a.Select(piece => piece.Id)), string.Join('|', b.Select(piece => piece.Id)), "Seeded tray");
            Equal(string.Join('|', first.LastReport.Solution), string.Join('|', second.LastReport.Solution), "Seeded certificate");
        }
    }

    public static void TrayLifecycle()
    {
        var engine = new GameEngine(47);
        var tray = engine.Tray.Select(piece => piece!).ToArray();
        var solver = new TraySolver();
        Check(solver.TrySolve(engine.Board, tray, out var solution), "Initial tray should have a full solution.");
        for (int index = 0; index < solution.Count; index++)
        {
            var planned = solution[index];
            var result = engine.TryPlace(planned.TrayIndex, planned.Row, planned.Column);
            Check(result.Success, "Certified move was rejected.");
            Equal(index == 2, result.TrayRefilled, "Refill timing");
            Equal(index == 2 ? 3 : 2 - index, engine.Tray.Count(piece => piece is not null), "Remaining pieces");
            if (index < 2)
            {
                Check(engine.Tray[planned.TrayIndex] is null, "Consumed slot remains available.");
                var before = StateSignature(engine);
                Check(!engine.TryPlace(planned.TrayIndex, 0, 0).Success, "Consumed slot can be reused.");
                Equal(before, StateSignature(engine), "Reusing a consumed slot mutated game state");
            }
        }
        Equal(3, engine.PiecesPlaced, "Piece counter");
        Check(!engine.IsGameOver, "A refilled tray should be playable.");
    }

    public static void InvalidMoves()
    {
        var engine = new GameEngine(12);
        string before = StateSignature(engine);
        Check(!engine.TryPlace(-1, 0, 0).Success, "Negative slot accepted.");
        Equal(before, StateSignature(engine), "Invalid slot mutated state");
        Check(!engine.TryPlace(3, 0, 0).Success, "Out-of-range slot accepted.");
        Equal(before, StateSignature(engine), "Invalid slot mutated state");
        Check(!engine.TryPlace(0, -1, 0).Success, "Negative position accepted.");
        Equal(before, StateSignature(engine), "Invalid anchor mutated state");
        Check(!engine.TryPlace(0, 8, 8).Success, "Overflow accepted.");
        Equal(before, StateSignature(engine), "Overflow mutated state");
        var shape = engine.Tray[0]!;
        var firstCell = shape.Cells[0];
        engine.Board.PlaceAndClear(Single, firstCell.Row, firstCell.Column);
        before = StateSignature(engine);
        Check(!engine.TryPlace(0, 0, 0).Success, "Collision accepted.");
        Equal(before, StateSignature(engine), "Collision mutated game state");
    }

    public static void ComboLifecycle()
    {
        var engine = new GameEngine(82);
        long expectedScore = 0;
        for (int move = 0; move < 3; move++)
        {
            ClearBoard(engine.Board);
            int slot = engine.Tray.ToList().FindIndex(piece => piece is not null);
            var shape = engine.Tray[slot]!;
            bool shouldClear = move < 2;
            if (shouldClear)
            {
                var topCells = shape.Cells.Where(cell => cell.Row == 0).Select(cell => cell.Column).ToHashSet();
                for (int column = 0; column < Board.Size; column++)
                    if (!topCells.Contains(column)) engine.Board.PlaceAndClear(Single, 0, column);
            }
            int previousLevel = engine.Level;
            var result = engine.TryPlace(slot, 0, 0);
            Check(result.Success, "Prepared combo move was rejected.");
            Equal(shouldClear ? 1 : 0, result.LinesCleared, "Prepared line count");
            Equal(shouldClear ? move + 1 : 0, engine.Combo, "Combo transition");
            Equal(engine.Combo, result.Combo, "Result combo");
            int expected = ScoreCalculator.Calculate(shape.CellCount, result.LinesCleared, engine.Combo, previousLevel);
            Equal(expected, result.PointsEarned, "Move points must use the pre-move level");
            expectedScore += expected;
            Equal(expectedScore, engine.Score, "Accumulated score");
        }
        Equal(2, engine.TotalLinesCleared, "Total cleared lines");
    }

    public static void ScoreThreshold()
    {
        var engine = new GameEngine(33);
        bool crossed = false;
        for (int move = 0; move < 12; move++)
        {
            ClearBoard(engine.Board);
            int slot = engine.Tray.ToList().FindIndex(piece => piece is not null);
            var shape = engine.Tray[slot]!;
            var topCells = shape.Cells.Where(cell => cell.Row == 0).Select(cell => cell.Column).ToHashSet();
            for (int column = 0; column < Board.Size; column++)
                if (!topCells.Contains(column)) engine.Board.PlaceAndClear(Single, 0, column);
            int previousLevel = engine.Level;
            long previousScore = engine.Score;
            var result = engine.TryPlace(slot, 0, 0);
            Check(result.Success, "Threshold move failed.");
            Equal(ScoreCalculator.Calculate(shape.CellCount, 1, move + 1, previousLevel), result.PointsEarned, "Threshold move price");
            Equal(previousScore + result.PointsEarned, engine.Score, "Threshold move accumulated score");
            if (engine.Level > previousLevel)
            {
                crossed = true;
                Check(result.PointsEarned < ScoreCalculator.Calculate(shape.CellCount, 1, move + 1, engine.Level), "Crossing move was charged at the new level.");
                break;
            }
        }
        Check(crossed, "Forced clear sequence failed to cross a level threshold.");
    }

    public static void GameOver()
    {
        GameEngine? engine = null;
        for (int seed = 0; seed < 50; seed++)
        {
            var candidate = new GameEngine(seed);
            if (candidate.Tray.All(piece => piece is { CellCount: > 1, Width: <= 5, Height: <= 5 }))
            {
                engine = candidate;
                break;
            }
        }
        Check(engine is not null, "Could not find a normal three-piece tray for the blocked-tray scenario.");
        var game = engine!;
        var selected = game.Tray[0]!;
        var reserved = selected.Cells.ToHashSet();
        foreach (var cell in AllCells())
            if ((cell.Row + cell.Column) % 2 == 0 && !reserved.Contains(cell))
                game.Board.PlaceAndClear(Single, cell.Row, cell.Column);
        var move = game.TryPlace(0, 0, 0);
        Check(move.Success, "Reserved shape could not be placed.");
        Equal(0, move.LinesCleared, "Checkerboard scenario unexpectedly cleared a line");
        Check(game.Tray.Where(piece => piece is not null).All(piece => !game.Board.HasAnyPlacement(piece!)), "Remaining tray is not actually blocked.");
        Check(game.IsGameOver && move.IsGameOver, "Blocked tray did not end the game.");
        var before = StateSignature(game);
        Check(!game.TryPlace(1, 0, 0).Success, "A finished game accepted another move.");
        Equal(before, StateSignature(game), "Finished game mutated state");
        game.Reset(77);
        Equal(0L, game.Score, "Reset score");
        Equal(0, game.Board.OccupiedCount, "Reset board");
        Equal(0, game.Combo, "Reset combo");
        Equal(0, game.PiecesPlaced, "Reset moves");
        Equal(0, game.TotalLinesCleared, "Reset lines");
        Equal(1, game.Level, "Reset level");
        Check(!game.IsGameOver && game.Tray.All(piece => piece is not null), "Reset did not create a playable game.");
    }

    public static void SeededReplay()
    {
        for (int seed = 0; seed < 4; seed++)
        {
            var first = new GameEngine(seed);
            var second = new GameEngine(seed);
            Equal(StateSignature(first), StateSignature(second), "Initial seeded state");
            for (int step = 0; step < 80 && !first.IsGameOver; step++)
            {
                var next = FindFirstMove(first);
                Check(next is not null, "An ongoing game has no legal move.");
                Check(!first.TryPlace(-1, 0, 0).Success, "Invalid replay move accepted.");
                var placement = next!.Value;
                var a = first.TryPlace(placement.Slot, placement.Row, placement.Column);
                var b = second.TryPlace(placement.Slot, placement.Row, placement.Column);
                Check(a.Success && b.Success, "Legal seeded move failed.");
                Equal(a.PointsEarned, b.PointsEarned, "Replay move points");
                Equal(StateSignature(first), StateSignature(second), "Seeded replay diverged");
            }
            first.Reset(seed);
            Equal(StateSignature(new GameEngine(seed)), StateSignature(first), "Reset should reproduce the initial seed");
        }
    }

    private static void ReplayCertificate(Board original, IReadOnlyList<BlockShape> tray, IReadOnlyList<PlannedPlacement> solution)
    {
        Equal(tray.Count, solution.Count, "Certificate must consume the complete tray");
        var board = original.Clone();
        var used = new HashSet<int>();
        foreach (var planned in solution)
        {
            Check(planned.TrayIndex >= 0 && planned.TrayIndex < tray.Count, "Certificate references an invalid slot.");
            Check(used.Add(planned.TrayIndex), "Certificate reuses a piece.");
            Check(board.CanPlace(tray[planned.TrayIndex], planned.Row, planned.Column), "Certificate contains an illegal move.");
            board.PlaceAndClear(tray[planned.TrayIndex], planned.Row, planned.Column);
        }
    }

    private static Board FragmentedBoard(Random random, double density)
    {
        var board = new Board();
        foreach (var cell in AllCells())
            if (random.NextDouble() < density) board.PlaceAndClear(Single, cell.Row, cell.Column);
        return board;
    }

    private static void ClearBoard(Board board)
    {
        // Fill the missing cells of each occupied row atomically, so a column clear
        // cannot erase an earlier cell before the row becomes complete.
        for (int row = 0; row < Board.Size; row++)
        {
            if (!Enumerable.Range(0, Board.Size).Any(column => board.IsOccupied(row, column))) continue;
            var missing = Enumerable.Range(0, Board.Size).Where(column => !board.IsOccupied(row, column)).ToArray();
            Check(missing.Length > 0, "A full row should already have been cleared.");
            int anchor = missing[0];
            var filler = new BlockShape("row-filler", "Row filler", missing.Select(column => new Cell(0, column - anchor)));
            board.PlaceAndClear(filler, row, anchor);
        }
        Equal(0, board.OccupiedCount, "Row cleanup");
    }

    private static (int Slot, int Row, int Column)? FindFirstMove(GameEngine engine)
    {
        for (int slot = 0; slot < engine.Tray.Count; slot++)
        {
            var shape = engine.Tray[slot];
            if (shape is null) continue;
            for (int row = 0; row < Board.Size; row++)
            for (int column = 0; column < Board.Size; column++)
                if (engine.Board.CanPlace(shape, row, column)) return (slot, row, column);
        }
        return null;
    }

    private static bool IsConnected(BlockShape shape)
    {
        var cells = shape.Cells.ToHashSet();
        var reached = new HashSet<Cell> { shape.Cells[0] };
        var queue = new Queue<Cell>(reached);
        while (queue.TryDequeue(out var current))
        {
            Cell[] neighbors = [new(current.Row - 1, current.Column), new(current.Row + 1, current.Column),
                new(current.Row, current.Column - 1), new(current.Row, current.Column + 1)];
            foreach (var neighbor in neighbors)
                if (cells.Contains(neighbor) && reached.Add(neighbor)) queue.Enqueue(neighbor);
        }
        return reached.Count == cells.Count;
    }

    private static IEnumerable<Cell> AllCells()
    {
        for (int row = 0; row < Board.Size; row++)
        for (int column = 0; column < Board.Size; column++) yield return new Cell(row, column);
    }

    private static string Signature(BlockShape shape) => string.Join(';', shape.Cells.Select(cell => $"{cell.Row},{cell.Column}"));
    private static string BoardSignature(Board board) => string.Concat(AllCells().Select(cell => board.IsOccupied(cell.Row, cell.Column) ? '1' : '0'));
    private static string StateSignature(GameEngine engine) => $"{BoardSignature(engine.Board)}:{engine.Score}:{engine.Level}:{engine.Combo}:{engine.TotalLinesCleared}:{engine.PiecesPlaced}:{engine.IsGameOver}:{string.Join('|', engine.Tray.Select(piece => piece?.Id ?? "-"))}";

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string context) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{context}: expected {expected}, got {actual}.");
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
