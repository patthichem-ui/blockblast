using BlockBlast.Core;

namespace BlockBlast.WinForms;

public sealed class MainForm : Form
{
    private readonly GameEngine _engine = new();
    private readonly HighScoreStore _highScores = new();
    private readonly GameSurface _surface;
    private readonly Font _bodyFont = new("Segoe UI", 10f);
    private readonly Font _headingFont = new("Segoe UI", 21f, FontStyle.Bold);
    private readonly Font _smallFont = new("Segoe UI", 9f);
    private readonly Font _buttonFont = new("Segoe UI", 10f, FontStyle.Bold);
    private long _bestScore;

    public MainForm()
    {
        Text = "Block Blast • C# WinForms";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 900);
        MinimumSize = new Size(670, 800);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = _bodyFont;
        BackColor = GameSurface.BackgroundColor;
        ForeColor = Color.FromArgb(237, 245, 253);
        KeyPreview = true;

        _bestScore = _highScores.Load();
        _surface = new GameSurface(_engine)
        {
            Dock = DockStyle.Fill,
            BestScore = _bestScore,
            TabIndex = 0
        };
        _surface.MoveCompleted += OnMoveCompleted;
        _surface.NewGameRequested += StartNewGame;

        var header = new Panel
        {
            Dock = DockStyle.Top, Width = ClientSize.Width, Height = 86,
            Padding = new Padding(24)
        };
        var title = new Label
        {
            Text = "BLOCK BLAST",
            AutoSize = true,
            Font = _headingFont,
            ForeColor = Color.FromArgb(107, 239, 207),
            Location = new Point(22, 12)
        };
        var subtitle = new Label
        {
            Text = "Đặt khối. Xóa hàng. Giữ nhịp combo.",
            AutoSize = true,
            Font = _smallFont,
            ForeColor = GameSurface.MutedColor,
            Location = new Point(25, 51)
        };
        var newGame = new Button
        {
            Text = "Ván mới   R",
            AccessibleName = "Bắt đầu ván mới",
            Font = _buttonFont,
            BackColor = Color.FromArgb(107, 239, 207),
            ForeColor = Color.FromArgb(14, 41, 40),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(136, 42),
            Location = new Point(ClientSize.Width - 160, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand,
            TabIndex = 1
        };
        newGame.FlatAppearance.BorderSize = 0;
        newGame.FlatAppearance.MouseOverBackColor = Color.FromArgb(147, 250, 222);
        newGame.Click += (_, _) => StartNewGame();
        header.Controls.AddRange([title, subtitle, newGame]);

        var footer = new Panel { Dock = DockStyle.Bottom, Width = ClientSize.Width, Height = 62 };
        footer.Controls.Add(new Label
        {
            Text = "Mỗi ô: 5 điểm • Xóa nhiều hàng/cột, combo và cấp độ tăng điểm",
            Font = _smallFont,
            ForeColor = GameSurface.MutedColor,
            AutoEllipsis = true,
            Location = new Point(24, 5),
            Size = new Size(ClientSize.Width - 48, 22),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        });
        footer.Controls.Add(new Label
        {
            Text = "1–3: chọn khối · ↑↓←→: di chuyển · Enter: đặt · Esc: hủy · R: ván mới",
            Font = _smallFont,
            ForeColor = GameSurface.MutedColor,
            AutoEllipsis = true,
            Location = new Point(24, 29),
            Size = new Size(ClientSize.Width - 48, 22),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        });

        Controls.Add(_surface);
        Controls.Add(header);
        Controls.Add(footer);
        Shown += (_, _) => _surface.Focus();
    }

    private void OnMoveCompleted(MoveResult move)
    {
        if (_engine.Score <= _bestScore)
            return;

        _bestScore = _engine.Score;
        _surface.BestScore = _bestScore;
        if (!_highScores.TrySave(_bestScore))
            _surface.ShowStorageNotice();
    }

    private void StartNewGame()
    {
        if (_engine.Score > 0 && !_engine.IsGameOver &&
            MessageBox.Show(this,
                "Bắt đầu ván mới? Kỷ lục của bạn vẫn được giữ lại.",
                "Ván mới", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        _engine.Reset();
        _surface.ResetInteraction();
        _surface.Focus();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.R)
        {
            StartNewGame();
            return true;
        }
        if (_surface.HandleGameKey(keyData))
            return true;

        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _bodyFont.Dispose();
            _headingFont.Dispose();
            _smallFont.Dispose();
            _buttonFont.Dispose();
        }
    }
}
