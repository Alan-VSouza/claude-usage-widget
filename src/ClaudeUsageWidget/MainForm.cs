namespace ClaudeUsageWidget;

public class MainForm : Form
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Label _percentLabel;
    private readonly Button _pinBtn;
    private readonly Button _closeBtn;
    private readonly Panel _progressOuter;
    private readonly Panel _progressInner;
    private readonly Label _resetLabel;
    private readonly Label _countdownLabel;
    private readonly System.Windows.Forms.Timer _fetchTimer;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly NotifyIcon _tray;

    private bool _isPinned = false;
    private int _secondsLeft = 0;
    private bool _dragging = false;
    private Point _dragLast;

    public MainForm()
    {
        Text = "Claude Usage Widget";
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        Size = new Size(300, 90);
        BackColor = Color.FromArgb(30, 30, 30);
        Opacity = 0.93;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;

        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - Width - 12, screen.Bottom - Height - 12);

        // ── Header ────────────────────────────────────────────────
        _header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(42, 42, 42)
        };

        _titleLabel = new Label
        {
            Text = "⬤ Claude Usage",
            ForeColor = Color.FromArgb(34, 197, 94),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.5f),
            Bounds = new Rectangle(8, 0, 145, 32),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _percentLabel = new Label
        {
            Text = "[—%]",
            ForeColor = Color.FromArgb(180, 180, 180),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Bounds = new Rectangle(148, 0, 68, 32),
            TextAlign = ContentAlignment.MiddleRight
        };

        _pinBtn = new Button
        {
            Text = "🔓",
            ForeColor = Color.FromArgb(120, 120, 120),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Bounds = new Rectangle(218, 4, 24, 24),
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = new Font("Segoe UI Emoji", 9f),
            Tag = false // false = unpinned
        };
        _pinBtn.FlatAppearance.BorderSize = 0;
        _pinBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
        _pinBtn.Click += (_, _) => TogglePin();

        _closeBtn = new Button
        {
            Text = "✕",
            ForeColor = Color.FromArgb(140, 140, 140),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Bounds = new Rectangle(246, 4, 24, 24),
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = new Font("Segoe UI", 9f)
        };
        _closeBtn.FlatAppearance.BorderSize = 0;
        _closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
        _closeBtn.Click += (_, _) => Application.Exit();

        _header.Controls.AddRange(new Control[] { _titleLabel, _percentLabel, _pinBtn, _closeBtn });

        // Arrastar pelo header e pelos labels (não pelos botões)
        AttachDrag(_header);
        AttachDrag(_titleLabel);
        AttachDrag(_percentLabel);

        // ── Barra de progresso ────────────────────────────────────
        _progressOuter = new Panel
        {
            Bounds = new Rectangle(10, 37, 280, 11),
            BackColor = Color.FromArgb(55, 55, 55)
        };

        _progressInner = new Panel
        {
            Bounds = new Rectangle(0, 0, 0, 11),
            BackColor = Color.FromArgb(34, 197, 94)
        };
        _progressOuter.Controls.Add(_progressInner);

        // ── Labels de info ────────────────────────────────────────
        _resetLabel = new Label
        {
            Text = "Conectando...",
            ForeColor = Color.FromArgb(130, 130, 130),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 7.5f),
            Bounds = new Rectangle(10, 53, 175, 16),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _countdownLabel = new Label
        {
            Text = "↻ —s",
            ForeColor = Color.FromArgb(85, 85, 85),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 7.5f),
            Bounds = new Rectangle(185, 53, 105, 16),
            TextAlign = ContentAlignment.MiddleRight
        };

        // Linha de info: linha separadora sutil
        var separator = new Panel
        {
            Bounds = new Rectangle(10, 52, 280, 1),
            BackColor = Color.FromArgb(45, 45, 45)
        };

        // Menu de contexto
        var ctxMenu = new ContextMenuStrip { BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
        var refreshItem = new ToolStripMenuItem("Atualizar agora");
        refreshItem.Click += async (_, _) => await RefreshAsync();
        ctxMenu.Items.Add(refreshItem);
        ctxMenu.Items.Add(new ToolStripSeparator());
        var pinMenuItem = new ToolStripMenuItem("Fixar janela");
        pinMenuItem.Click += (_, _) => TogglePin();
        ctxMenu.Items.Add(pinMenuItem);
        ctxMenu.Items.Add(new ToolStripSeparator());
        var closeItem = new ToolStripMenuItem("Fechar");
        closeItem.Click += (_, _) => Application.Exit();
        ctxMenu.Items.Add(closeItem);
        ContextMenuStrip = ctxMenu;

        Controls.AddRange(new Control[] { _header, _progressOuter, separator, _resetLabel, _countdownLabel });

        // ── System Tray ───────────────────────────────────────────
        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "Claude Usage Widget",
            Icon = SystemIcons.Application
        };
        var trayMenu = new ContextMenuStrip();
        var showHide = new ToolStripMenuItem("Mostrar/Ocultar");
        showHide.Click += (_, _) => { if (Visible) Hide(); else { Show(); BringToFront(); } };
        trayMenu.Items.Add(showHide);
        trayMenu.Items.Add(new ToolStripSeparator());
        var trayClose = new ToolStripMenuItem("Fechar");
        trayClose.Click += (_, _) => Application.Exit();
        trayMenu.Items.Add(trayClose);
        _tray.ContextMenuStrip = trayMenu;
        _tray.DoubleClick += (_, _) => { if (Visible) Hide(); else { Show(); BringToFront(); } };

        // ── Timers ────────────────────────────────────────────────
        _fetchTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _fetchTimer.Tick += async (_, _) =>
        {
            _secondsLeft = 60;
            await RefreshAsync();
        };

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1_000 };
        _clockTimer.Tick += (_, _) =>
        {
            if (_secondsLeft > 0) _secondsLeft--;
            UpdateCountdownLabel();
        };
    }

    private void TogglePin()
    {
        _isPinned = !_isPinned;
        _pinBtn.Text = _isPinned ? "🔒" : "🔓";
        _pinBtn.ForeColor = _isPinned
            ? Color.FromArgb(234, 179, 8)
            : Color.FromArgb(120, 120, 120);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _secondsLeft = 0;
        _fetchTimer.Start();
        _clockTimer.Start();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await UsageService.FetchAsync();
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) { Invoke(() => ApplyData(data)); return; }
        ApplyData(data);
        _secondsLeft = 60;
    }

    private void ApplyData(UsageData data)
    {
        _percentLabel.Text = data.ParseSuccess ? $"[{data.Percent}%]" : "[—%]";

        _progressInner.Width = (int)(_progressOuter.Width * (data.Percent / 100.0));

        var color = data.Percent switch
        {
            <= 60 => Color.FromArgb(34, 197, 94),
            <= 85 => Color.FromArgb(234, 179, 8),
            _     => Color.FromArgb(239, 68, 68)
        };
        _progressInner.BackColor = color;
        _titleLabel.ForeColor = color;

        if (!data.ParseSuccess)
        {
            var preview = data.RawText.Length > 35 ? data.RawText[..35] + "…" : data.RawText;
            _resetLabel.Text = $"⚠ {preview}";
        }
        else
        {
            _resetLabel.Text = data.ResetsIn.HasValue
                ? $"Reset: {FormatTs(data.ResetsIn.Value)}"
                : "Reset: —";
        }

        var tooltip = data.ParseSuccess
            ? $"Claude: {data.Percent}%{(data.ResetsIn.HasValue ? $" · Reset {FormatTs(data.ResetsIn.Value)}" : "")}"
            : "Claude Usage Widget";
        _tray.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        UpdateCountdownLabel();
    }

    private void UpdateCountdownLabel()
    {
        if (InvokeRequired) { Invoke(UpdateCountdownLabel); return; }
        _countdownLabel.Text = _secondsLeft > 0 ? $"↻ {_secondsLeft}s" : "↻ agora...";
    }

    private static string FormatTs(TimeSpan ts) =>
        ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m" : $"{(int)ts.TotalMinutes}m";

    private void AttachDrag(Control c)
    {
        c.MouseDown += (_, e) =>
        {
            if (_isPinned || e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragLast = c.PointToScreen(e.Location);
        };
        c.MouseMove += (_, e) =>
        {
            if (!_dragging || _isPinned) return;
            var cur = c.PointToScreen(e.Location);
            Location = new Point(Location.X + cur.X - _dragLast.X,
                                 Location.Y + cur.Y - _dragLast.Y);
            _dragLast = cur;
        };
        c.MouseUp += (_, _) => _dragging = false;
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _tray.Visible = false;
        _tray.Dispose();
        _fetchTimer.Stop();
        _fetchTimer.Dispose();
        _clockTimer.Stop();
        _clockTimer.Dispose();
        base.OnFormClosing(e);
    }
}
