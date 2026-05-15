using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace WolfIsland
{
    public class MainForm : Form
    {
        const int CELL = 26;
        const int GRID_PX = SimulationGrid.Size * CELL;
        const int CHART_H = 160;
        const int PANEL_W = 220;
        const int PAD = 12;

        SimulationGrid _grid;
        Random _rng = new(42);
        int _step = 0;
        System.Windows.Forms.Timer _timer = new();

        List<int> _histRabbits = new();
        List<int> _histWolves = new();
        List<int> _histWolfesses = new();

        Panel _gridPanel;
        Panel _chartPanel;
        Label _lblStep, _lblRabbits, _lblWolves, _lblWolfesses;
        Button _btnStart, _btnStep, _btnReset;
        TrackBar _tbSpeed;
        Label _lblSpeed;

        static readonly Color C_BG      = Color.FromArgb(24, 36, 24);
        static readonly Color C_CELL    = Color.FromArgb(36, 52, 34);
        static readonly Color C_GRID    = Color.FromArgb(44, 62, 42);
        static readonly Color C_RABBIT  = Color.FromArgb(106, 188, 60);
        static readonly Color C_WOLFM   = Color.FromArgb(110, 100, 220);
        static readonly Color C_WOLFF   = Color.FromArgb(210, 80, 40);
        static readonly Color C_PANEL   = Color.FromArgb(30, 30, 38);
        static readonly Color C_TEXT    = Color.FromArgb(220, 220, 220);
        static readonly Color C_SUBTEXT = Color.FromArgb(140, 140, 155);
        static readonly Color C_ACCENT  = Color.FromArgb(80, 74, 180);

        public MainForm()
        {
            InitializeUI();
            ResetSim();
        }

        void InitializeUI()
        {
            Text = "Вовчий острів — екологічна симуляція";
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            int formW = GRID_PX + PANEL_W + PAD * 3 + 16;
            int formH = GRID_PX + CHART_H + PAD * 3 + 60;
            ClientSize = new Size(formW, formH);

            _gridPanel = new Panel
            {
                Location = new Point(PAD, PAD),
                Size = new Size(GRID_PX, GRID_PX),
                BackColor = C_BG
            };
            _gridPanel.Paint += GridPanel_Paint;
            Controls.Add(_gridPanel);

            _chartPanel = new Panel
            {
                Location = new Point(PAD, GRID_PX + PAD * 2),
                Size = new Size(GRID_PX, CHART_H),
                BackColor = C_PANEL
            };
            _chartPanel.Paint += ChartPanel_Paint;
            Controls.Add(_chartPanel);

            int px = GRID_PX + PAD * 2;
            int py = PAD;

            var lblTitle = MakeLabel("🐺  ВОВЧИЙ ОСТРІВ", px, py, PANEL_W - PAD, 28, true);
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(180, 170, 255);
            Controls.Add(lblTitle); py += 36;

            var sep = new Panel { Location = new Point(px, py), Size = new Size(PANEL_W - PAD, 1), BackColor = C_ACCENT };
            Controls.Add(sep); py += 12;

            _lblStep      = MakeStatLabel("Крок: 0",     px, py, C_TEXT);   py += 34;
            _lblRabbits   = MakeStatLabel("Кролики: 0",  px, py, C_RABBIT); py += 34;
            _lblWolves    = MakeStatLabel("Вовки: 0",    px, py, C_WOLFM);  py += 34;
            _lblWolfesses = MakeStatLabel("Вовчиці: 0",  px, py, C_WOLFF);  py += 44;

            var sep2 = new Panel { Location = new Point(px, py), Size = new Size(PANEL_W - PAD, 1), BackColor = Color.FromArgb(60, 60, 70) };
            Controls.Add(sep2); py += 14;

            _btnStart = MakeButton("▶  Старт", px, py, C_ACCENT);
            _btnStart.Click += (s, e) => ToggleStart();
            Controls.Add(_btnStart); py += 44;

            _btnStep = MakeButton("  Один крок", px, py);
            _btnStep.Click += (s, e) => DoStep();
            Controls.Add(_btnStep); py += 44;

            _btnReset = MakeButton("↺  Скинути", px, py);
            _btnReset.Click += (s, e) => ResetSim();
            Controls.Add(_btnReset); py += 54;

            var lblSp = MakeLabel("Швидкість:", px, py, PANEL_W - PAD, 20);
            lblSp.ForeColor = C_SUBTEXT;
            Controls.Add(lblSp); py += 22;

            _tbSpeed = new TrackBar
            {
                Minimum = 1, Maximum = 30, Value = 5,
                Location = new Point(px, py),
                Size = new Size(PANEL_W - PAD - 36, 28),
                TickFrequency = 5,
                BackColor = C_PANEL
            };
            _tbSpeed.ValueChanged += (s, e) => { _timer.Interval = SpeedToMs(_tbSpeed.Value); UpdateSpeedLabel(); };
            Controls.Add(_tbSpeed);

            _lblSpeed = MakeLabel("5", px + PANEL_W - PAD - 30, py, 30, 24);
            _lblSpeed.ForeColor = C_RABBIT;
            _lblSpeed.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            Controls.Add(_lblSpeed); py += 36;

            py += 6;
            AddLegend(px, ref py, C_RABBIT, "Кролик");
            AddLegend(px, ref py, C_WOLFM,  "Вовк (самець)");
            AddLegend(px, ref py, C_WOLFF,  "Вовчиця (самиця)");

            _timer.Interval = SpeedToMs(5);
            _timer.Tick += (s, e) => DoStep();
        }

        Label MakeLabel(string text, int x, int y, int w, int h, bool bold = false)
        {
            return new Label
            {
                Text = text, Location = new Point(x, y), Size = new Size(w, h),
                ForeColor = C_TEXT, BackColor = Color.Transparent,
                Font = bold ? new Font("Segoe UI", 9.5f, FontStyle.Bold) : Font
            };
        }

        Label MakeStatLabel(string text, int x, int y, Color color)
        {
            var lbl = new Label
            {
                Text = text, Location = new Point(x, y),
                Size = new Size(PANEL_W - PAD, 28),
                ForeColor = color,
                BackColor = Color.FromArgb(40, 40, 50),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };
            Controls.Add(lbl);
            return lbl;
        }

        Button MakeButton(string text, int x, int y, Color? bg = null)
        {
            return new Button
            {
                Text = text, Location = new Point(x, y),
                Size = new Size(PANEL_W - PAD, 36),
                BackColor = bg ?? Color.FromArgb(50, 50, 62),
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                Cursor = Cursors.Hand
            };
        }

        void AddLegend(int x, ref int y, Color color, string text)
        {
            var dot = new Panel
            {
                Location = new Point(x + 2, y + 4),
                Size = new Size(12, 12),
                BackColor = color
            };
            dot.Region = new Region(new RectangleF(0, 0, 12, 12));
            Controls.Add(dot);
            var lbl = MakeLabel(text, x + 18, y, PANEL_W - 30, 20);
            lbl.ForeColor = C_SUBTEXT;
            Controls.Add(lbl);
            y += 22;
        }

        int SpeedToMs(int v) => Math.Max(50, 1100 - v * 35);

        void UpdateSpeedLabel() => _lblSpeed.Text = _tbSpeed.Value.ToString();

        void ResetSim()
        {
            _timer.Stop();
            _btnStart.Text = "▶  Старт";
            _rng = new Random(42);
            _grid = SimulationGrid.CreateDefault(_rng);
            _step = 0;
            _histRabbits.Clear(); _histWolves.Clear(); _histWolfesses.Clear();
            RecordHistory();
            UpdateStats();
            _gridPanel.Invalidate();
            _chartPanel.Invalidate();
        }

        void DoStep()
        {
            if (_grid.RabbitCount == 0 && _grid.WolfCount == 0 && _grid.WolfessCount == 0)
            {
                _timer.Stop();
                _btnStart.Text = "▶  Старт";
                MessageBox.Show("Популяція вимерла!", "Симуляція завершена",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _grid.Step();
            _step++;
            RecordHistory();
            UpdateStats();
            _gridPanel.Invalidate();
            _chartPanel.Invalidate();
        }

        void ToggleStart()
        {
            if (_timer.Enabled)
            {
                _timer.Stop();
                _btnStart.Text = "▶  Старт";
            }
            else
            {
                _timer.Start();
                _btnStart.Text = "⏸  Пауза";
            }
        }

        void RecordHistory()
        {
            _histRabbits.Add(_grid.RabbitCount);
            _histWolves.Add(_grid.WolfCount);
            _histWolfesses.Add(_grid.WolfessCount);
        }

        void UpdateStats()
        {
            _lblStep.Text      = $"  Крок: {_step}";
            _lblRabbits.Text   = $"  Кролики: {_grid.RabbitCount}";
            _lblWolves.Text    = $"  Вовки: {_grid.WolfCount}";
            _lblWolfesses.Text = $"  Вовчиці: {_grid.WolfessCount}";
        }

        void GridPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int x = 0; x < SimulationGrid.Size; x++)
                for (int y = 0; y < SimulationGrid.Size; y++)
                {
                    var rect = new Rectangle(x * CELL, y * CELL, CELL, CELL);
                    g.FillRectangle(new SolidBrush(C_CELL), rect);
                    g.DrawRectangle(new Pen(C_GRID, 0.5f), rect);
                }

            for (int x = 0; x < SimulationGrid.Size; x++)
                for (int y = 0; y < SimulationGrid.Size; y++)
                    if (_grid.HasRabbit(x, y))
                        DrawEntity(g, x, y, C_RABBIT, 7);

            foreach (var wolf in _grid.Wolves)
                DrawEntity(g, wolf.X, wolf.Y, wolf.Sex == Sex.Male ? C_WOLFM : C_WOLFF, 9);
        }

        void DrawEntity(Graphics g, int x, int y, Color color, int radius)
        {
            int cx = x * CELL + CELL / 2;
            int cy = y * CELL + CELL / 2;
            using var brush = new SolidBrush(color);
            using var glowBrush = new SolidBrush(Color.FromArgb(60, color));
            g.FillEllipse(glowBrush, cx - radius - 2, cy - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
            g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);
        }

        void ChartPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = _chartPanel.Width, h = _chartPanel.Height;
            int padL = 40, padR = 10, padT = 14, padB = 24;
            int cw = w - padL - padR, ch = h - padT - padB;

            g.FillRectangle(new SolidBrush(C_PANEL), 0, 0, w, h);

            if (_histRabbits.Count < 2) return;

            int maxVal = Math.Max(1,
                _histRabbits.Concat(_histWolves).Concat(_histWolfesses).Max());

            using var axisPen = new Pen(Color.FromArgb(70, 70, 85), 1);
            g.DrawLine(axisPen, padL, padT, padL, padT + ch);
            g.DrawLine(axisPen, padL, padT + ch, padL + cw, padT + ch);

            using var lblFont = new Font("Segoe UI", 7.5f);
            for (int i = 0; i <= 4; i++)
            {
                int yv = maxVal * i / 4;
                float yp = padT + ch - (float)ch * i / 4;
                g.DrawString(yv.ToString(), lblFont, Brushes.Gray, 2, yp - 7);
                g.DrawLine(axisPen, padL, yp, padL + cw, yp);
            }

            DrawLine(g, _histRabbits,   C_RABBIT, padL, padT, cw, ch, maxVal);
            DrawLine(g, _histWolves,    C_WOLFM,  padL, padT, cw, ch, maxVal);
            DrawLine(g, _histWolfesses, C_WOLFF,  padL, padT, cw, ch, maxVal);

            using var titleFont = new Font("Segoe UI", 8f, FontStyle.Bold);
            g.DrawString("Динаміка популяцій", titleFont, new SolidBrush(C_SUBTEXT), padL + 4, 2);
        }

        void DrawLine(Graphics g, List<int> data, Color color, int padL, int padT, int cw, int ch, int maxVal)
        {
            if (data.Count < 2) return;
            int n = data.Count;
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
                pts[i] = new PointF(
                    padL + (float)cw * i / Math.Max(1, n - 1),
                    padT + ch - (float)ch * data[i] / maxVal);

            using var pen = new Pen(color, 2f) { LineJoin = LineJoin.Round };
            g.DrawLines(pen, pts);
        }
    }
}
