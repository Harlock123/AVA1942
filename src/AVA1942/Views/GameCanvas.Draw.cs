using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVA1942.Engine;

namespace AVA1942.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVA1942/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width);
        return p;
    }

    private const uint White = 0xFFF2F2F2, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Cyan = 0xFF4FD7FF, Dim = 0xFF9AA2B8, Orange = 0xFFFF9A2A, Green = 0xFF70E070;
    private const uint PlayerColour = 0xFFD8DCE0, FighterColour = 0xFF6A8A4A, RedColour = 0xFFC83A2A, GreyColour = 0xFF8A8C98, BomberColour = 0xFF5A6A48;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xA0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    /// <summary>Paints a drawing centred on a point, turned to a heading (0 = up the screen), stretched across by <paramref name="squash"/>.</summary>
    private static void Sprite(DrawingContext dc, string name, uint tint, Point centre, double heading, double scale = 3, double squash = 1, double alpha = 1)
    {
        var rows = Sprites.All[name];
        double half = 8 * scale;
        using (dc.PushTransform(Matrix.CreateTranslation(-half, -half) * Matrix.CreateScale(squash, 1) * Matrix.CreateRotation(heading) * Matrix.CreateTranslation(centre.X, centre.Y)))
        {
            for (int r = 0; r < 16; r++)
            {
                string row = rows[r];
                int c = 0;
                while (c < 16)
                {
                    uint col = Sprites.Colour(row[c], tint);
                    int start = c;
                    while (c < 16 && Sprites.Colour(row[c], tint) == col) c++;
                    if (col == 0) continue;
                    if (alpha < 1) col = (col & 0x00FFFFFF) | ((uint)(alpha * 255) << 24);
                    dc.FillRectangle(Brush(col), new Rect(start * scale, r * scale, (c - start) * scale + 0.3, scale + 0.3));
                }
            }
        }
    }

    /// <summary>A plane's shadow on the sea below it.</summary>
    private static void Shadow(DrawingContext dc, string name, Point centre, double heading, double scale) =>
        Sprite(dc, name, 0xFF000000, new Point(centre.X + 14, centre.Y + 20), heading, scale * 0.85, 1, 0.28);

    private static Point P(Vec v) => new(v.X, v.Y);

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(Field.Left, 0, Field.Width, Field.Height)))
        using (dc.PushTransform(Matrix.CreateTranslation(Field.Left, 0)))
        {
            DrawSea(dc);
            bool flying = g.State is GameState.Playing or GameState.Paused or GameState.Attract or GameState.GameOver;
            if (g.CarrierY is { } cy && flying) DrawCarrier(dc, cy);
            if (flying) DrawPlay(dc);
            DrawCloudsAbove(dc);
            foreach (var e in g.Effects) DrawEffect(dc, e);
            switch (g.State)
            {
                case GameState.Title: DrawTitle(dc); break;
                case GameState.HighScores: DrawHighScores(dc); break;
                case GameState.EnterInitials: DrawInitials(dc); break;
                default: DrawMessages(dc); break;
            }
            if (g.State == GameState.Paused)
            {
                dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
                Text(dc, "PAUSED", new Point(Field.Width / 2, 340), 80, Yellow, center: true);
                Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(Field.Width / 2, 400), 26, White, center: true);
            }
            if (g.State == GameState.GameOver) Text(dc, "GAME OVER", new Point(Field.Width / 2, 330), 90, Red, center: true);
            if (g.State == GameState.Attract)
            {
                Text(dc, "DEMONSTRATION", new Point(Field.Width / 2, 300), 40, Yellow, center: true);
                Text(dc, "PRESS ENTER TO PLAY", new Point(Field.Width / 2, 336), 26, White, center: true);
            }
        }
        DrawPanels(dc);
    }

    // ───────────────────────────── the sea ─────────────────────────────

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Mix(uint a, uint b, double t)
    {
        uint C(int s) => (uint)Math.Round(((a >> s) & 0xFF) * (1 - t) + ((b >> s) & 0xFF) * t);
        return 0xFF000000 | (C(16) << 16) | (C(8) << 8) | C(0);
    }

    /// <summary>The ocean, its swell, and the islands and ships far below, all scrolling down the screen.</summary>
    private void DrawSea(DrawingContext dc)
    {
        var st = Engine.Stage;
        double scroll = Engine.Scroll;
        for (int i = 0; i < 16; i++)
            dc.FillRectangle(Brush(Mix(st.SeaDeep, st.Sea, i / 15.0)), new Rect(0, i * 48, Field.Width, 49));
        // The swell: short white strokes in rows.
        const double row = 36;
        int r0 = (int)Math.Floor(scroll / row);
        for (int r = r0; r < r0 + 24; r++)
        {
            double y = Field.Height - (r * row - scroll);
            for (int i = 0; i < 6; i++)
            {
                uint h = Hash(i, r, 5);
                double x = (h % 600) - 12;
                double w = 10 + (h >> 10) % 20;
                dc.DrawLine(Pen(0x30FFFFFF, 2), new Point(x, y), new Point(x + w, y));
            }
        }
        // Islands and ships, one per stretch of sea.
        const double stretch = 520;
        int s0 = (int)Math.Floor(scroll / stretch);
        for (int s = s0 - 1; s < s0 + 3; s++)
        {
            uint h = Hash(s, Engine.StageNumber, 17);
            double y = Field.Height - (s * stretch - scroll) - (h % 200);
            double x = 60 + (h >> 8) % 460;
            if (h % 3 != 0) DrawIsland(dc, new Point(x, y), h, st.Land);
            else DrawShip(dc, new Point(x, y), h);
        }
    }

    private static void DrawIsland(DrawingContext dc, Point at, uint h, uint land)
    {
        int lumps = 3 + (int)(h % 4);
        double size = 40 + (h >> 4) % 50;
        for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i < lumps; i++)
            {
                uint lh = Hash((int)h, i, 3);
                var c = new Point(at.X + ((lh % 100) - 50) * size / 50, at.Y + (((lh >> 8) % 60) - 30) * size / 50);
                double r = size * (0.5 + ((lh >> 16) % 40) / 100.0);
                uint col = pass switch { 0 => 0x4080D0E0, 1 => 0xFFE8D8A0, _ => land };
                double grow = pass switch { 0 => 14, 1 => 6, _ => 0 };
                dc.DrawEllipse(Brush(col), null, c, r + grow, (r + grow) * 0.8);
            }
        for (int i = 0; i < 5; i++)
        {
            uint ph = Hash((int)h, i, 9);
            var c = new Point(at.X + ((ph % 80) - 40) * size / 50, at.Y + (((ph >> 8) % 50) - 25) * size / 50);
            dc.DrawEllipse(Brush(Sprites.Shade(land, 0.6)), null, c, 7, 7);
            dc.DrawEllipse(Brush(Sprites.Shade(land, 1.25)), null, new Point(c.X - 2, c.Y - 2), 4, 4);
        }
    }

    private static void DrawShip(DrawingContext dc, Point at, uint h)
    {
        double len = 70 + h % 50;
        // The wake.
        var wake = new StreamGeometry();
        using (var s = wake.Open())
        {
            s.BeginFigure(new Point(at.X - 8, at.Y + len / 2), true);
            s.LineTo(new Point(at.X - 30, at.Y + len / 2 + 120));
            s.LineTo(new Point(at.X + 30, at.Y + len / 2 + 120));
            s.LineTo(new Point(at.X + 8, at.Y + len / 2));
            s.EndFigure(true);
        }
        dc.DrawGeometry(Brush(0x40FFFFFF), null, wake);
        var hull = new StreamGeometry();
        using (var s = hull.Open())
        {
            s.BeginFigure(new Point(at.X, at.Y - len / 2), true);
            s.LineTo(new Point(at.X + 11, at.Y - len / 4));
            s.LineTo(new Point(at.X + 10, at.Y + len / 2));
            s.LineTo(new Point(at.X - 10, at.Y + len / 2));
            s.LineTo(new Point(at.X - 11, at.Y - len / 4));
            s.EndFigure(true);
        }
        dc.DrawGeometry(Brush(0xFF6A6E78), Pen(0xFF3A3E48), hull);
        dc.FillRectangle(Brush(0xFF4A4E58), new Rect(at.X - 5, at.Y - 6, 10, 18));
        dc.FillRectangle(Brush(0xFF4A4E58), new Rect(at.X - 3, at.Y - len / 3, 6, 10));
    }

    /// <summary>The carrier the plane takes off from and lands on.</summary>
    private void DrawCarrier(DrawingContext dc, double deckY)
    {
        double x = Field.Width / 2;
        var deck = new Rect(x - 62, deckY - 260, 124, 440);
        dc.FillRectangle(Brush(0x50FFFFFF), new Rect(deck.X + 10, deck.Bottom, deck.Width - 20, 140));
        var hull = new StreamGeometry();
        using (var s = hull.Open())
        {
            s.BeginFigure(new Point(x, deck.Y - 50), true);
            s.LineTo(new Point(deck.Right, deck.Y + 30));
            s.LineTo(new Point(deck.Right, deck.Bottom));
            s.LineTo(new Point(deck.X, deck.Bottom));
            s.LineTo(new Point(deck.X, deck.Y + 30));
            s.EndFigure(true);
        }
        dc.DrawGeometry(Brush(0xFF5A5E66), Pen(0xFF2A2E36, 2), hull);
        dc.FillRectangle(Brush(0xFF6A6458), deck.Deflate(8));
        for (double y = deck.Y + 20; y < deck.Bottom - 20; y += 40)
            dc.FillRectangle(Brush(0xFFE8E8E0), new Rect(x - 2, y, 4, 20));
        dc.DrawLine(Pen(0xFFE8E0A0, 2), new Point(deck.X + 16, deck.Y + 20), new Point(deck.X + 16, deck.Bottom - 16));
        dc.DrawLine(Pen(0xFFE8E0A0, 2), new Point(deck.Right - 16, deck.Y + 20), new Point(deck.Right - 16, deck.Bottom - 16));
        // The island, on the starboard side.
        dc.FillRectangle(Brush(0xFF4A4E56), new Rect(deck.Right - 8, deckY - 60, 22, 80));
        dc.FillRectangle(Brush(0xFF30343C), new Rect(deck.Right - 2, deckY - 40, 10, 26));
    }

    private void DrawCloudsAbove(DrawingContext dc)
    {
        double scroll = Engine.Scroll * 1.6;
        const double stretch = 700;
        int s0 = (int)Math.Floor(scroll / stretch);
        for (int s = s0 - 1; s < s0 + 2; s++)
        {
            uint h = Hash(s, 3, 23);
            if (h % 2 == 0) continue;
            double y = Field.Height - (s * stretch - scroll);
            double x = (h >> 8) % 576;
            for (int i = 0; i < 6; i++)
            {
                uint ph = Hash(s, i, 29);
                dc.DrawEllipse(Brush(0x70F4F4F8), null, new Point(x + ((ph % 180) - 90), y + (((ph >> 8) % 60) - 30)), 50 + (ph >> 16) % 30, 34);
            }
        }
    }

    // ───────────────────────────── the planes ─────────────────────────────

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        foreach (var p in g.Pows)
        {
            var at = P(p.Pos);
            bool blink = (int)(_clock * 6) % 2 == 0;
            dc.DrawEllipse(Brush(blink ? Red : 0xFFF0F0F0), Pen(0xFF202020, 2), at, 18, 18);
            Text(dc, "POW", new Point(at.X, at.Y - 1), 20, blink ? White : Red, center: true);
        }
        foreach (var e in g.Enemies.Where(e => e.Active))
        {
            var at = P(e.Pos);
            double heading = e.Vel.LengthSq > 1 ? e.Vel.Angle : Math.PI;
            if (e.Kind == EnemyKind.Boss) { DrawBoss(dc, e); continue; }
            string sprite = e.Kind == EnemyKind.Bomber ? "bomber" : "fighter";
            double scale = e.Kind == EnemyKind.Bomber ? 5 : 2.6;
            uint tint = e.Flash > 0 ? 0xFFFFFFFF : e.Kind switch
            {
                EnemyKind.Red => RedColour, EnemyKind.Grey => GreyColour, EnemyKind.Bomber => BomberColour, _ => FighterColour,
            };
            Shadow(dc, sprite, at, heading, scale);
            Sprite(dc, sprite, tint, at, heading, scale);
        }
        foreach (var s in g.Shots)
        {
            var at = P(s.Pos);
            if (s.Hostile)
            {
                dc.DrawEllipse(Brush(0xFFFFC040), Pen(0xFF803000, 1.5), at, 5, 5);
                dc.DrawEllipse(Brush(0xFFFFF8D0), null, at, 2, 2);
            }
            else dc.FillRectangle(Brush(Yellow), new Rect(at.X - 2, at.Y - 9, 4, 14));
        }
        if (g.Phase != Phase.Dying) DrawPlayer(dc);
    }

    private void DrawPlayer(DrawingContext dc)
    {
        var g = Engine;
        var me = g.Player;
        if (me.Invulnerable > 0 && !me.Rolling && (int)(_clock * 12) % 2 == 0) return;
        double scale = 3, squash = 1, lift = 0;
        if (me.Rolling)
        {
            // A loop: up and over, the plane growing as it climbs and flipping as it turns over.
            double t = 1 - me.RollTime / GameEngine.RollSeconds;
            scale = 3 * (1 + 0.45 * Math.Sin(Math.PI * t));
            squash = Math.Cos(2 * Math.PI * t);
            lift = -40 * Math.Sin(Math.PI * t);
        }
        else if (g.Phase == Phase.Takeoff) scale = 3 * (0.8 + 0.2 * Math.Min(1, g.PhaseTime / 2.4));
        else if (g.Phase is Phase.Landing or Phase.Tally && g.CarrierY is { } cy && (me.Pos - new Vec(Field.Width / 2, cy)).Length < 40) scale = 2.4;
        var at = new Point(me.Pos.X, me.Pos.Y + lift);
        bool onDeck = g.Phase is Phase.Takeoff or Phase.Tally;
        if (!onDeck) Shadow(dc, "player", new Point(me.Pos.X, me.Pos.Y), 0, 3);
        Sprite(dc, "player", PlayerColour, at, 0, scale, Math.Abs(squash) < 0.15 ? 0.15 * Math.Sign(squash + 1e-9) : squash);
        foreach (var w in me.Wingmen.Where(w => w.Alive))
        {
            var wp = me.WingmanPos(w);
            Shadow(dc, "player", P(wp), 0, 2);
            Sprite(dc, "player", 0xFFB8C8D8, new Point(wp.X, wp.Y + lift * 0.6), 0, 2, squash);
        }
    }

    /// <summary>The great bomber: a four-engined giant, nose down the screen.</summary>
    private void DrawBoss(DrawingContext dc, Enemy b)
    {
        var at = P(b.Pos);
        uint C(uint col) => b.Flash > 0 ? Mix(col, 0xFFFFFFFF, 0.6) : col;
        foreach (var (dx, dy, alpha) in new[] { (16.0, 24.0, 0x48u), (0.0, 0.0, 0u) })
        {
            bool shadow = alpha != 0;
            uint S(uint col) => shadow ? alpha << 24 : C(col);
            using (dc.PushTransform(Matrix.CreateTranslation(at.X + dx, at.Y + dy)))
            {
                var wing = new StreamGeometry();
                using (var s = wing.Open())
                {
                    s.BeginFigure(new Point(-160, -6), true);
                    s.LineTo(new Point(-20, -26));
                    s.LineTo(new Point(20, -26));
                    s.LineTo(new Point(160, -6));
                    s.LineTo(new Point(160, 14));
                    s.LineTo(new Point(-160, 14));
                    s.EndFigure(true);
                }
                dc.DrawGeometry(Brush(S(0xFF6A7062)), shadow ? null : Pen(0xFF30362A, 2), wing);
                dc.FillRectangle(Brush(S(0xFF5A6052)), new Rect(-18, -100, 36, 200));
                dc.FillRectangle(Brush(S(0xFF6A7062)), new Rect(-60, -96, 120, 22));
                if (shadow) continue;
                foreach (double x in new[] { -120.0, -66, 66, 120 })
                {
                    dc.FillRectangle(Brush(C(0xFF3A3A3A)), new Rect(x - 10, -6, 20, 46));
                    double a = _clock * 30 + x;
                    dc.DrawLine(Pen(0xA0202020, 3), new Point(x - Math.Cos(a) * 20, 42), new Point(x + Math.Cos(a) * 20, 42));
                }
                dc.DrawEllipse(Brush(C(0xFF60C0F0)), null, new Point(0, 88), 10, 12);
                foreach (var (tx, ty) in new[] { (-110.0, 10.0), (110.0, 10.0), (-55.0, 30.0), (55.0, 30.0) })
                    dc.DrawEllipse(Brush(C(0xFF2A2A2A)), Pen(C(0xFF8A8A8A), 2), new Point(tx, ty), 7, 7);
                dc.DrawEllipse(Brush(0xFFF0F0F0), Pen(0xFFC03020, 4), new Point(-100, -4), 11, 11);
                dc.DrawEllipse(Brush(0xFFF0F0F0), Pen(0xFFC03020, 4), new Point(100, -4), 11, 11);
            }
        }
        double frac = (double)b.Hits / (Engine.Stage.BossHits + Engine.Loop * 30);
        var bar = new Rect(at.X - 90, at.Y - 124, 180, 8);
        dc.FillRectangle(Brush(0xC0000000), bar.Inflate(2));
        dc.FillRectangle(Brush(frac > 0.5 ? Green : frac > 0.25 ? Yellow : Red), new Rect(bar.X, bar.Y, bar.Width * Math.Clamp(frac, 0, 1), bar.Height));
    }

    private void DrawEffect(DrawingContext dc, Engine.Effect e)
    {
        var at = P(e.Pos);
        double t = e.Age / e.Life;
        switch (e.Kind)
        {
            case EffectKind.Explosion or EffectKind.BigExplosion:
            {
                double r = (e.Kind == EffectKind.BigExplosion ? 80 : 28) * Math.Sqrt(t);
                uint a = (uint)((1 - t) * 255) << 24;
                dc.DrawEllipse(Brush(a | 0x00FF6020), null, at, r, r);
                dc.DrawEllipse(Brush(a | 0x00FFE060), null, at, r * 0.55, r * 0.55);
                for (int i = 0; i < 8; i++)
                {
                    double ang = i * Math.PI / 4 + e.Pos.X;
                    var q = new Point(at.X + Math.Cos(ang) * r * 1.4, at.Y + Math.Sin(ang) * r * 1.4);
                    dc.FillRectangle(Brush(a | 0x00402010), new Rect(q.X - 3, q.Y - 3, 6, 6));
                }
                break;
            }
            case EffectKind.Score:
                Text(dc, e.Text, new Point(at.X, at.Y - t * 30), 28, Yellow, center: true);
                break;
        }
    }

    // ───────────────────────────── words ─────────────────────────────

    private void DrawMessages(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        if (g.Message is { } msg && (msg != "WARNING" || (int)(_clock * 4) % 2 == 0))
        {
            Text(dc, msg, new Point(cx, 230), msg == "WARNING" ? 72 : 64, msg == "WARNING" ? Red : Yellow, center: true);
            if (msg.StartsWith("STAGE ") && g.Phase == Phase.Takeoff) Text(dc, g.Stage.Name, new Point(cx, 286), 36, White, center: true);
        }
        if (g.Phase == Phase.Tally)
        {
            dc.FillRectangle(Brush(0xA0000010), new Rect(40, 170, Field.Width - 80, 300));
            Text(dc, $"STAGE {g.StageNumber} CLEAR", new Point(cx, 210), 56, Yellow, center: true);
            int pct = g.Spawned == 0 ? 100 : (int)Math.Round(100.0 * g.Downed / g.Spawned);
            Text(dc, $"SHOOTING DOWN  {pct}%", new Point(cx, 280), 40, White, center: true);
            Text(dc, $"({g.Downed} OF {g.Spawned})", new Point(cx, 318), 28, Dim, center: true);
            Text(dc, $"BONUS  {g.TallyBonus}", new Point(cx, 370), 44, Cyan, center: true);
            if (g.Perfect && (int)(_clock * 4) % 2 == 0) Text(dc, "PERFECT!", new Point(cx, 426), 48, Red, center: true);
        }
    }

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0x80000010), new Rect(0, 0, Field.Width, Field.Height));
        Text(dc, "AVA 1942", new Point(cx, 100), 110, Yellow, center: true);
        Text(dc, "AFTER THE 1984 ARCADE CLASSIC", new Point(cx, 162), 26, Dim, center: true);
        Sprite(dc, "player", PlayerColour, new Point(cx, 240), 0, 4);
        string[] lines =
        [
            "FLY CARRIER TO CARRIER ACROSS EIGHT STAGES",
            "DOWN THE GREAT BOMBER TO LAND SAFE",
            "",
            "ROLL (X OR SHIFT) TO LOOP OUT OF TROUBLE",
            "SHOOT A WHOLE RED SQUADRON FOR A POW:",
            "WIDE SHOT - WINGMEN - EXTRA ROLL",
            "BLAST - POINTS - EXTRA PLANE",
        ];
        for (int i = 0; i < lines.Length; i++) Text(dc, lines[i], new Point(cx, 320 + i * 32), 26, White, center: true);
        Sprite(dc, "fighter", RedColour, new Point(cx - 120, 600), Math.PI, 2.4);
        Sprite(dc, "fighter", RedColour, new Point(cx - 60, 600), Math.PI, 2.4);
        dc.DrawEllipse(Brush(Red), Pen(0xFF202020, 2), new Point(cx + 80, 600), 18, 18);
        Text(dc, "POW", new Point(cx + 80, 599), 20, White, center: true);
        if ((int)(_clock * 2) % 2 == 0) Text(dc, "PRESS ENTER OR START", new Point(cx, 680), 40, Yellow, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0x80000010), new Rect(0, 0, Field.Width, Field.Height));
        Text(dc, "BEST PILOTS", new Point(cx, 90), 72, Yellow, center: true);
        Text(dc, "RANK NAME    SCORE  STAGE", new Point(cx, 160), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,2}", new Point(cx, 210 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 700), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0x90000010), new Rect(0, 0, Field.Width, Field.Height));
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 180), 60, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 250), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 330), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 420), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 460, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 540), 24, White, center: true);
    }

    // ───────────────────────────── the panels either side ─────────────────────────────

    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        foreach (var x in new[] { 0.0, Field.Left + Field.Width })
        {
            dc.FillRectangle(Brush(0xFF10141C), new Rect(x, 0, Field.Left, Field.Height));
            dc.FillRectangle(Brush(0xFF3A4A5A), new Rect(x == 0 ? Field.Left - 3 : x, 0, 3, Field.Height));
        }
        double lx = Field.Left / 2, rx = Field.Left + Field.Width + Field.Left / 2;
        Text(dc, "AVA", new Point(lx, 50), 56, Orange, center: true);
        Text(dc, "1942", new Point(lx, 104), 72, Yellow, center: true);
        Text(dc, "SCORE", new Point(lx, 190), 28, Dim, center: true);
        Text(dc, g.Score.ToString(), new Point(lx, 222), 40, White, center: true);
        Text(dc, "HI-SCORE", new Point(lx, 276), 28, Dim, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 308), 40, Yellow, center: true);
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.Attract or GameState.GameOver;
        if (playing)
        {
            Text(dc, "STAGE", new Point(lx, 370), 28, Dim, center: true);
            Text(dc, g.StageNumber.ToString(), new Point(lx, 404), 44, White, center: true);
            Text(dc, g.Stage.Name, new Point(lx, 440), 24, Cyan, center: true);
            int pct = g.Spawned == 0 ? 0 : (int)Math.Round(100.0 * g.Downed / g.Spawned);
            Text(dc, "SHOT DOWN", new Point(lx, 500), 28, Dim, center: true);
            Text(dc, $"{pct}%", new Point(lx, 532), 40, White, center: true);
            double frac = Math.Min(1, g.StageTime / g.Stage.Length);
            var bar = new Rect(30, 580, Field.Left - 60, 14);
            dc.DrawRectangle(Pen(Dim, 2), bar);
            dc.FillRectangle(Brush(g.BossArrived ? Red : Cyan), new Rect(bar.X + 2, bar.Y + 2, (bar.Width - 4) * frac, bar.Height - 4));
            Text(dc, g.BossArrived ? "THE GREAT BOMBER" : "TO THE NEXT CARRIER", new Point(lx, 612), 20, Dim, center: true);

            Text(dc, "PLANES", new Point(rx, 50), 28, Dim, center: true);
            for (int i = 0; i < Math.Min(g.Lives, 6); i++) Sprite(dc, "player", PlayerColour, new Point(rx - 75 + (i % 4) * 50, 92 + i / 4 * 46), 0, 2);
            Text(dc, "ROLLS", new Point(rx, 180), 28, Dim, center: true);
            for (int i = 0; i < Math.Min(g.Player.Rolls, 8); i++)
            {
                var c = new Point(rx - 75 + (i % 4) * 50, 220 + i / 4 * 40);
                dc.DrawEllipse(Brush(0xFF2A3A6A), Pen(Cyan, 2), c, 15, 15);
                Text(dc, "R", new Point(c.X, c.Y - 1), 24, White, center: true);
            }
            Text(dc, "POWER", new Point(rx, 320), 28, Dim, center: true);
            Text(dc, g.Player.Wide ? "WIDE SHOT" : "-", new Point(rx, 352), 26, g.Player.Wide ? Yellow : Dim, center: true);
            int wing = g.Player.Wingmen.Count(w => w.Alive);
            Text(dc, wing > 0 ? $"WINGMEN x{wing}" : "-", new Point(rx, 382), 26, wing > 0 ? Yellow : Dim, center: true);
        }
        string[] help = ["ARROWS / WASD: FLY", "Z / SPACE: FIRE", "X / SHIFT: ROLL", "P PAUSE   M MUTE", "CTRL+S SCREENSHOT"];
        for (int i = 0; i < help.Length; i++) Text(dc, help[i], new Point(rx, 610 + i * 26), 21, Dim, center: true);
    }
}
