using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ClaudeUsageWidget;

public class MainForm : Form
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Label _percentLabel;
    private readonly Button _pinBtn;
    private readonly Button _minBtn;
    private readonly Button _closeBtn;
    private readonly Panel _progressOuter;
    private readonly Panel _progressInner;
    private readonly Label _resetLabel;
    private readonly Label _countdownLabel;
    private readonly Panel _separator;
    private readonly System.Windows.Forms.Timer _fetchTimer;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly NotifyIcon _tray;

    private bool  _isPinned    = false;
    private int   _secondsLeft = 0;
    private bool  _dragging    = false;
    private Point _dragLast;
    private Point _dragStart;
    private bool  _wasDragged  = false;
    private bool  _isMinimized = false;
    private const int GRIP = 16;

    // Cor-chave para a transparência da bolinha (nunca usada no design do círculo)
    private static readonly Color KeyColor = Color.FromArgb(1, 1, 1);
    private int   _currentPct  = 0;
    private bool  _parseOk     = false;
    private Size  _normalSize  = new(300, 90);
    private const int MIN_SIZE = 80;

    public MainForm()
    {
        Text            = "Claude Usage Widget";
        FormBorderStyle = FormBorderStyle.None;
        TopMost         = true;
        Size            = new Size(300, 90);
        BackColor       = Color.FromArgb(30, 30, 30);
        Opacity         = 0.92;
        ShowInTaskbar   = false;
        StartPosition   = FormStartPosition.Manual;

        MinimumSize = new Size(240, 80);

        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - Width - 12, screen.Bottom - Height - 12);

        SizeChanged += (_, _) => UpdateLayout();

        // ── Header ────────────────────────────────────────────────
        _header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 32,
            BackColor = Color.FromArgb(42, 42, 42)
        };

        _titleLabel = new Label
        {
            Text      = "⬤ Claude Usage",
            ForeColor = Color.FromArgb(34, 197, 94),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI Semibold", 9f),
            Bounds    = new Rectangle(8, 0, 130, 32),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _percentLabel = new Label
        {
            Text      = "[—%]",
            ForeColor = Color.FromArgb(180, 180, 180),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            Bounds    = new Rectangle(138, 0, 48, 32),
            TextAlign = ContentAlignment.MiddleRight
        };

        _pinBtn = new Button
        {
            Text      = "🔓",
            ForeColor = Color.FromArgb(120, 120, 120),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Bounds    = new Rectangle(190, 4, 24, 24),
            Cursor    = Cursors.Hand,
            TabStop   = false,
            Font      = new Font("Segoe UI Emoji", 9f)
        };
        _pinBtn.FlatAppearance.BorderSize         = 0;
        _pinBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
        _pinBtn.Click += (_, _) => TogglePin();

        _minBtn = new Button
        {
            Text      = "○",
            ForeColor = Color.FromArgb(140, 140, 140),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Bounds    = new Rectangle(218, 4, 24, 24),
            Cursor    = Cursors.Hand,
            TabStop   = false,
            Font      = new Font("Segoe UI", 11f)
        };
        _minBtn.FlatAppearance.BorderSize         = 0;
        _minBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
        _minBtn.Click += (_, _) => ToggleMinimize();
        new ToolTip().SetToolTip(_minBtn, "Minimizar em círculo");

        _closeBtn = new Button
        {
            Text      = "✕",
            ForeColor = Color.FromArgb(140, 140, 140),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Bounds    = new Rectangle(246, 4, 24, 24),
            Cursor    = Cursors.Hand,
            TabStop   = false,
            Font      = new Font("Segoe UI", 9f)
        };
        _closeBtn.FlatAppearance.BorderSize         = 0;
        _closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
        _closeBtn.Click += (_, _) => Application.Exit();

        _header.Controls.AddRange(new Control[] { _titleLabel, _percentLabel, _pinBtn, _minBtn, _closeBtn });

        // Arrastar pelo header e pelos labels
        AttachDrag(_header);
        AttachDrag(_titleLabel);
        AttachDrag(_percentLabel);

        // ── Barra de progresso ────────────────────────────────────
        _progressOuter = new Panel
        {
            Bounds    = new Rectangle(10, 37, 280, 12),
            BackColor = Color.FromArgb(55, 55, 55)
        };

        _progressInner = new Panel
        {
            Bounds    = new Rectangle(0, 0, 0, 12),
            BackColor = Color.FromArgb(34, 197, 94)
        };
        _progressOuter.Controls.Add(_progressInner);

        // ── Labels de informação ──────────────────────────────────
        _resetLabel = new Label
        {
            Text      = "Conectando...",
            ForeColor = Color.FromArgb(180, 180, 180),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI Semibold", 8.5f),
            Bounds    = new Rectangle(10, 54, 175, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _countdownLabel = new Label
        {
            Text      = "↻ —s",
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Bounds    = new Rectangle(185, 54, 105, 18),
            TextAlign = ContentAlignment.MiddleRight
        };

        _separator = new Panel
        {
            Bounds    = new Rectangle(10, 53, 280, 1),
            BackColor = Color.FromArgb(45, 45, 45)
        };

        // ── Menu de contexto ──────────────────────────────────────
        var ctxMenu = new ContextMenuStrip { BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
        var refreshItem = new ToolStripMenuItem("Atualizar agora");
        refreshItem.Click += async (_, _) => await RefreshAsync();
        ctxMenu.Items.Add(refreshItem);
        ctxMenu.Items.Add(new ToolStripSeparator());
        var pinItem = new ToolStripMenuItem("Fixar janela");
        pinItem.Click += (_, _) => TogglePin();
        ctxMenu.Items.Add(pinItem);
        ctxMenu.Items.Add(new ToolStripSeparator());
        var closeItem = new ToolStripMenuItem("Fechar");
        closeItem.Click += (_, _) => Application.Exit();
        ctxMenu.Items.Add(closeItem);
        ContextMenuStrip = ctxMenu;

        Controls.AddRange(new Control[] { _header, _progressOuter, _separator, _resetLabel, _countdownLabel });

        // Habilita custom painting para o modo minimizado
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        // Arrastar pelo form (necessário no modo minimizado)
        AttachDrag(this);
        MouseClick += (_, e) =>
        {
            if (_isMinimized && e.Button == MouseButtons.Left && !_wasDragged)
                ToggleMinimize();
        };

        // ── System Tray ───────────────────────────────────────────
        _tray = new NotifyIcon { Visible = true, Text = "Claude Usage Widget", Icon = SystemIcons.Application };
        var trayMenu = new ContextMenuStrip();
        var showHide = new ToolStripMenuItem("Mostrar/Ocultar");
        showHide.Click += (_, _) => { if (Visible) Hide(); else { Show(); BringToFront(); } };
        trayMenu.Items.Add(showHide);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Fechar").Click += (_, _) => Application.Exit();
        _tray.ContextMenuStrip = trayMenu;
        _tray.DoubleClick += (_, _) => { if (Visible) Hide(); else { Show(); BringToFront(); } };

        // ── Timers ────────────────────────────────────────────────
        _fetchTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _fetchTimer.Tick += async (_, _) => { _secondsLeft = 60; await RefreshAsync(); };

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1_000 };
        _clockTimer.Tick += (_, _) => { if (_secondsLeft > 0) _secondsLeft--; UpdateCountdownLabel(); };
    }

    private void TogglePin()
    {
        _isPinned         = !_isPinned;
        _pinBtn.Text      = _isPinned ? "🔒" : "🔓";
        _pinBtn.ForeColor = _isPinned ? Color.FromArgb(234, 179, 8) : Color.FromArgb(120, 120, 120);
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
    }

    private void ApplyData(UsageData data)
    {
        _currentPct = data.Percent;
        _parseOk    = data.ParseSuccess;

        _percentLabel.Text = data.ParseSuccess ? $"[{data.Percent}%]" : "[—%]";

        _progressInner.Width = (int)(_progressOuter.Width * (data.Percent / 100.0));

        var color = data.Percent switch
        {
            <= 60 => Color.FromArgb(34, 197, 94),
            <= 85 => Color.FromArgb(234, 179, 8),
            _     => Color.FromArgb(239, 68, 68)
        };
        _progressInner.BackColor = color;
        _titleLabel.ForeColor    = color;

        if (!data.ParseSuccess)
        {
            var preview = data.RawText.Length > 35 ? data.RawText[..35] + "…" : data.RawText;
            _resetLabel.Text = $"⚠ {preview}";
        }
        else
        {
            _resetLabel.Text = data.ResetsIn.HasValue
                ? $"Reset em: {FormatTs(data.ResetsIn.Value)}"
                : "Reset: —";
        }

        var tooltip = data.ParseSuccess
            ? $"Claude: {data.Percent}%{(data.ResetsIn.HasValue ? $" · Reset {FormatTs(data.ResetsIn.Value)}" : "")}"
            : "Claude Usage Widget";
        _tray.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        _secondsLeft = 60;
        UpdateCountdownLabel();

        if (_isMinimized) Invalidate();
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
            if (e.Button != MouseButtons.Left) return;
            _wasDragged = false; // sempre reseta, independente do estado de pin
            if (_isPinned) return;
            _dragging  = true;
            _dragStart = c.PointToScreen(e.Location);
            _dragLast  = _dragStart;
        };
        c.MouseMove += (_, e) =>
        {
            if (!_dragging || _isPinned) return;
            var cur = c.PointToScreen(e.Location);
            if (!_wasDragged)
            {
                // Só considera arrasto se mover mais de 5px do ponto inicial
                var dx = cur.X - _dragStart.X;
                var dy = cur.Y - _dragStart.Y;
                if (Math.Abs(dx) > 5 || Math.Abs(dy) > 5)
                    _wasDragged = true;
            }
            if (_wasDragged)
            {
                Location  = new Point(Location.X + cur.X - _dragLast.X,
                                      Location.Y + cur.Y - _dragLast.Y);
                _dragLast = cur;
            }
        };
        c.MouseUp += (_, _) => _dragging = false;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (_isMinimized)
            e.Graphics.Clear(KeyColor); // fundo transparente na bolinha
        else
            base.OnPaintBackground(e);
    }

    // Resize nativo via HTBOTTOMRIGHT — Windows cuida do cursor e do drag
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST  = 0x84;
        const int HTBOTTOMRIGHT = 17;

        if (m.Msg == WM_NCHITTEST && !_isMinimized && !_isPinned)
        {
            int x  = (short)(m.LParam.ToInt32() & 0xFFFF);
            int y  = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
            var pt = PointToClient(new Point(x, y));
            if (pt.X >= Width - GRIP && pt.Y >= Height - GRIP)
            {
                m.Result = (IntPtr)HTBOTTOMRIGHT;
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_isMinimized)
        {
            DrawMinimizedCircle(e.Graphics);
            return;
        }
        // Grip visual no canto inferior direito
        if (!_isPinned)
        {
            var g = e.Graphics;
            using var pen = new Pen(Color.FromArgb(68, 68, 68), 1f);
            for (int i = 1; i <= 3; i++)
            {
                int d = i * 4;
                g.DrawLine(pen, Width - 2, Height - d, Width - d, Height - 2);
            }
        }
    }

    private void UpdateLayout()
    {
        if (_isMinimized) return;

        int w = Width;
        int h = Height;

        // Barra de progresso: largura total menos margens
        _progressOuter.Bounds = new Rectangle(10, 37, w - 20, 12);
        _progressInner.Width  = (int)(_progressOuter.Width * (_currentPct / 100.0));

        // Rodapé ancorado ao fundo
        int footY = h - 24;
        int sepY  = h - 30;
        _separator.Bounds      = new Rectangle(10, sepY, w - 20, 1);
        _resetLabel.Bounds     = new Rectangle(10, footY, w - 130, 18);
        _countdownLabel.Bounds = new Rectangle(w - 120, footY, 110, 18);

        // Botões do header: ancoragem à direita
        _closeBtn.Location     = new Point(w - 28,  4);
        _minBtn.Location       = new Point(w - 56,  4);
        _pinBtn.Location       = new Point(w - 84,  4);
        _percentLabel.Bounds   = new Rectangle(w - 134, 0, 46, 32);

        Invalidate();
    }

    private void ToggleMinimize()
    {
        if (_isMinimized)
        {
            // Centro da bolinha antes de qualquer mudança
            int cx = Left + MIN_SIZE / 2;
            int cy = Top  + MIN_SIZE / 2;

            // Calcula posição ideal: widget centralizado na bolinha, dentro da tela
            var wa = Screen.FromPoint(new Point(cx, cy)).WorkingArea;
            int newLeft = Math.Max(wa.Left, Math.Min(cx - _normalSize.Width  / 2, wa.Right  - _normalSize.Width));
            int newTop  = Math.Max(wa.Top,  Math.Min(cy - _normalSize.Height / 2, wa.Bottom - _normalSize.Height));

            SuspendLayout();
            _isMinimized    = false;
            Cursor          = Cursors.Default;
            TransparencyKey = Color.Empty;  // remove transparência por cor-chave
            Opacity         = 0.92;         // restaura opacidade original
            Region          = null;
            Location        = new Point(newLeft, newTop);
            Size            = _normalSize;
            SetWidgetControlsVisible(true);
            ResumeLayout(true);
            Invalidate();
        }
        else
        {
            _normalSize = Size;

            int cx = Left + Width  / 2;
            int cy = Top  + Height / 2;

            SuspendLayout();
            _isMinimized    = true;
            Opacity         = 1.0;          // desativa opacidade parcial (evita conflito)
            TransparencyKey = KeyColor;     // fundo transparente por cor-chave
            Region          = null;         // sem Region — anti-aliasing real
            SetWidgetControlsVisible(false);
            Size     = new Size(MIN_SIZE, MIN_SIZE);
            Location = new Point(cx - MIN_SIZE / 2, cy - MIN_SIZE / 2);
            Cursor   = Cursors.Hand;
            ResumeLayout(false);
            Invalidate();
        }
    }

    private void SetWidgetControlsVisible(bool visible)
    {
        _header.Visible         = visible;
        _progressOuter.Visible  = visible;
        _separator.Visible      = visible;
        _resetLabel.Visible     = visible;
        _countdownLabel.Visible = visible;
    }

    private void DrawMinimizedCircle(Graphics g)
    {
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        // Limpa com a cor-chave → área fora do círculo vira transparente
        g.Clear(KeyColor);

        // Círculo de fundo com anti-aliasing real (sem Region)
        using (var bg = new SolidBrush(Color.FromArgb(30, 30, 30)))
            g.FillEllipse(bg, 1, 1, MIN_SIZE - 2, MIN_SIZE - 2);

        int stroke  = 6;
        int padding = 6;
        var arcRect = new Rectangle(padding, padding, MIN_SIZE - padding * 2, MIN_SIZE - padding * 2);

        using (var trackPen = new Pen(Color.FromArgb(55, 55, 55), stroke))
            g.DrawArc(trackPen, arcRect, 0, 360);

        var accent = AccentColor(_currentPct);
        if (_currentPct > 0)
        {
            using var pen = new Pen(accent, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, arcRect, -90f, _currentPct / 100f * 360f);
        }

        var text = _parseOk ? $"{_currentPct}%" : "—";
        using var font  = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(230, 230, 230));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, new RectangleF(0, 0, MIN_SIZE, MIN_SIZE), sf);
    }

    private static Color AccentColor(int pct) => pct switch
    {
        <= 60 => Color.FromArgb(34, 197, 94),
        <= 85 => Color.FromArgb(234, 179, 8),
        _     => Color.FromArgb(239, 68, 68)
    };

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _tray.Visible = false;
        _tray.Dispose();
        _fetchTimer.Stop(); _fetchTimer.Dispose();
        _clockTimer.Stop(); _clockTimer.Dispose();
        base.OnFormClosing(e);
    }
}
