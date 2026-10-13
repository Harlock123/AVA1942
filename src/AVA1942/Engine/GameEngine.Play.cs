namespace AVA1942.Engine;

/// <summary>The player's fighter.</summary>
public sealed class PlayerPlane
{
    public Vec Pos = new(Field.Width / 2, 640);
    public double FireTimer;
    public bool Wide;
    public List<Wingman> Wingmen { get; } = [];
    public int Rolls = GameEngine.RollsPerStage;
    /// <summary>Seconds left in a loop-the-loop: while it lasts nothing can touch the plane, and it can't fire.</summary>
    public double RollTime;
    public double Invulnerable;
    public bool RollPressed;
    public bool Rolling => RollTime > 0;
    public bool Safe => RollTime > 0 || Invulnerable > 0;
    /// <summary>Where a wingman flies, beside the plane.</summary>
    public Vec WingmanPos(Wingman w) => Pos + new Vec(w.Side * 44, 10);
}

public sealed partial class GameEngine
{
    public const double PlayerSpeed = 270;
    public const double FireInterval = 0.12;
    public const double ShotSpeed = 900;
    public const double PlayerRadius = 10;
    public const double RollSeconds = 1.6;
    public const int RollsPerStage = 3;
    public const int FirstExtraLife = 20000, ExtraLifeEvery = 70000;
    public const double CarrierDeckY = 600;

    public PlayerPlane Player { get; } = new();
    public List<Enemy> Enemies { get; } = [];
    public List<Shot> Shots { get; } = [];
    public List<Pow> Pows { get; } = [];
    public int StageNumber { get; private set; } = 1;
    public Stage Stage => Stage.Of(StageNumber);
    public int Loop => (StageNumber - 1) / Stage.All.Length;
    /// <summary>Seconds into the stage's flight; when it reaches the stage's length the great bomber comes.</summary>
    public double StageTime { get; private set; }
    /// <summary>How far the sea has scrolled: the islands and ships below are placed by it.</summary>
    public double Scroll { get; private set; }
    public int NextExtraLife { get; private set; } = FirstExtraLife;
    public bool BossArrived { get; private set; }
    public Enemy? Boss => Enemies.FirstOrDefault(e => e.Kind == EnemyKind.Boss && !e.Dead);
    /// <summary>For the stage's tally: every enemy that came, and every one shot down.</summary>
    public int Spawned { get; private set; }
    public int Downed { get; private set; }
    public int TallyBonus { get; private set; }
    public bool Perfect { get; private set; }
    /// <summary>Where the carrier is on the screen (its deck's middle), or null while it's out of sight.</summary>
    public double? CarrierY { get; private set; }
    public string? LastDeath { get; private set; }

    private double _waveTimer;
    private int _waves;
    private int _nextSquadron;
    private readonly Dictionary<int, (int Left, int Downed)> _squadrons = [];

    /// <summary>Test hook: no waves arrive unless a test puts them there.</summary>
    internal bool Calm { get; set; }

    private double Difficulty => 1 + Loop * 0.15;
    private double Rand(double lo, double hi) => lo + _rng.NextDouble() * (hi - lo);

    private void StartStage(int number)
    {
        StageNumber = number;
        StageTime = 0;
        BossArrived = false;
        Spawned = Downed = 0;
        TallyBonus = 0;
        Perfect = false;
        Enemies.Clear();
        Shots.Clear();
        Pows.Clear();
        _squadrons.Clear();
        _waves = 0;
        _waveTimer = 2.5;
        Player.Pos = new Vec(Field.Width / 2, CarrierDeckY);
        Player.Rolls = RollsPerStage;
        Player.RollTime = 0;
        Player.Invulnerable = 0;
        CarrierY = CarrierDeckY;
        SetPhase(Phase.Takeoff);
        Say($"STAGE {StageNumber}", 2.5);
    }

    internal void AddScore(int points)
    {
        int before = Score;
        Score += points;
        if (Score >= NextExtraLife && before < NextExtraLife)
        {
            Lives++;
            Play(Sfx.ExtraLife);
            Popup(Player.Pos + new Vec(0, -50), "1UP", 1.5);
            NextExtraLife = NextExtraLife == FirstExtraLife ? ExtraLifeEvery : NextExtraLife + ExtraLifeEvery;
        }
    }

    private void Popup(Vec at, string text, double life = 1) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = life, Text = text });

    private void Explode(Vec at, bool big = false) =>
        Effects.Add(new Effect { Kind = big ? EffectKind.BigExplosion : EffectKind.Explosion, Pos = at, Life = big ? 1.3 : 0.55 });

    /// <summary>One step of play.</summary>
    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        Scroll += Field.ScrollSpeed * dt;
        switch (Phase)
        {
            case Phase.Takeoff:
                // The carrier slides away beneath as the plane climbs off its deck.
                CarrierY = CarrierDeckY + PhaseTime * PhaseTime * 60;
                if (PhaseTime > 2.4)
                {
                    CarrierY = null;
                    SetPhase(Phase.Playing);
                    Music(true);
                }
                return;
            case Phase.Dying:
                MoveThings(dt);
                if (PhaseTime > 2.2)
                {
                    if (Lives <= 0) { GameOver(); return; }
                    Shots.RemoveAll(s => s.Hostile);
                    Player.Pos = new Vec(Field.Width / 2, 640);
                    Player.Invulnerable = 2.5;
                    SetPhase(Phase.Playing);
                    Music(true);
                }
                return;
            case Phase.Landing:
                Land(dt);
                return;
            case Phase.Tally:
                if (PhaseTime > 4.5) StartStage(StageNumber + 1);
                return;
        }

        StageTime += dt;
        FlyPlayer(dt, c);
        if (!Calm) Spawn(dt);
        MoveThings(dt);
        Collide();
    }

    private void FlyPlayer(double dt, Controls c)
    {
        var p = Player;
        double k = (c.Dx != 0 && c.Dy != 0) ? 0.7071 : 1;
        p.Pos = new Vec(Math.Clamp(p.Pos.X + c.Dx * PlayerSpeed * k * dt, 26, Field.Width - 26),
                        Math.Clamp(p.Pos.Y + c.Dy * PlayerSpeed * k * dt, 60, Field.Height - 40));
        if (p.Invulnerable > 0) p.Invulnerable -= dt;
        if (p.RollTime > 0) p.RollTime -= dt;
        if (c.Roll && !p.RollPressed && p.Rolls > 0 && !p.Rolling)
        {
            p.Rolls--;
            p.RollTime = RollSeconds;
            Play(Sfx.Roll);
        }
        p.RollPressed = c.Roll;
        p.FireTimer -= dt;
        if (c.Fire && p.FireTimer <= 0 && !p.Rolling)
        {
            p.FireTimer = FireInterval;
            Fire(p.Pos + new Vec(-7, -20), 0);
            Fire(p.Pos + new Vec(7, -20), 0);
            if (p.Wide)
            {
                Fire(p.Pos + new Vec(-12, -14), -0.22);
                Fire(p.Pos + new Vec(12, -14), 0.22);
            }
            foreach (var w in p.Wingmen.Where(w => w.Alive)) Fire(p.WingmanPos(w) + new Vec(0, -14), 0);
            Play(Sfx.Shoot);
        }
    }

    private void Fire(Vec at, double angle) =>
        Shots.Add(new Shot { Pos = at, Vel = Vec.FromAngle(angle) * ShotSpeed });

    // ───────────────────────────── waves ─────────────────────────────

    private void Spawn(double dt)
    {
        if (StageTime >= Stage.Length)
        {
            if (!BossArrived && Enemies.All(e => e.Kind != EnemyKind.Bomber || e.Gone || e.Dead)) SpawnBoss();
            return;
        }
        _waveTimer -= dt;
        if (_waveTimer > 0) return;
        _waveTimer = Stage.WaveGap * Rand(0.8, 1.2) / Difficulty;
        _waves++;
        int s = StageNumber;
        if (_waves % 4 == 0) SpawnSquadron();
        else if ((s >= 2 && _rng.NextDouble() < 0.14) || (s == 1 && _waves == 12)) SpawnBomber();
        else if (_rng.NextDouble() < 0.16) SpawnRise();
        else
            switch (_rng.Next(3))
            {
                case 0: SpawnSwoop(); break;
                case 1: SpawnCross(); break;
                default: SpawnDive(); break;
            }
    }

    private Enemy Add(EnemyKind kind, List<Vec> path, double speed, double delay, int hits = 1, int squadron = -1)
    {
        var e = new Enemy
        {
            Kind = kind, Path = path, Pos = path[0], Next = 1, Speed = speed * Difficulty, Delay = delay, Hits = hits,
            Squadron = squadron, FireCooldown = Rand(0.8, 2.5),
        };
        Enemies.Add(e);
        if (kind != EnemyKind.Boss) Spawned++;
        return e;
    }

    /// <summary>A line of fighters dives down, loops round and climbs away.</summary>
    private void SpawnSwoop()
    {
        int side = _rng.Next(2) == 0 ? -1 : 1;
        double x = side < 0 ? Rand(80, 200) : Rand(376, 496);
        double r = 70;
        var path = new List<Vec> { new(x, -40), new(x, 300) };
        var centre = new Vec(x - side * r, 300);
        for (int i = 1; i <= 9; i++)
        {
            double a = i * Math.PI * 1.5 / 9;
            path.Add(centre + new Vec(side * Math.Cos(a) * r, Math.Sin(a) * r));
        }
        path.Add(new Vec(x - side * 340, -80));
        int n = 4 + _rng.Next(3);
        for (int i = 0; i < n; i++) Add(EnemyKind.Fighter, path, Stage.EnemySpeed, i * 0.28);
    }

    /// <summary>A file of fighters crosses from one side, dipping down and rising out the other.</summary>
    private void SpawnCross()
    {
        int side = _rng.Next(2) == 0 ? -1 : 1;
        double y = Rand(90, 260);
        double x0 = side < 0 ? -40 : Field.Width + 40, x1 = Field.Width - x0;
        var path = new List<Vec> { new(x0, y), new((x0 * 3 + x1) / 4, y + 120), new((x0 + x1 * 3) / 4, y + 120), new(x1, y - 40) };
        for (int i = 0; i < 5; i++) Add(EnemyKind.Fighter, path, Stage.EnemySpeed * 1.05, i * 0.25);
    }

    /// <summary>A few fighters dive straight at where the plane is now.</summary>
    private void SpawnDive()
    {
        int n = 2 + _rng.Next(2);
        for (int i = 0; i < n; i++)
        {
            double x = Rand(60, Field.Width - 60);
            var target = new Vec(Math.Clamp(Player.Pos.X + Rand(-80, 80), 30, Field.Width - 30), Field.Height + 60);
            Add(EnemyKind.Fighter, [new Vec(x, -40), target], Stage.EnemySpeed * 1.25, i * 0.45);
        }
    }

    /// <summary>Fast grey fighters climb up past the plane from behind.</summary>
    private void SpawnRise()
    {
        double x = Rand(80, Field.Width - 80);
        int side = x < Field.Width / 2 ? 1 : -1;
        for (int i = 0; i < 3; i++)
            Add(EnemyKind.Grey, [new Vec(x + i * side * 36, Field.Height + 40), new Vec(x + i * side * 36 + side * 60, 400), new Vec(x + i * side * 36 - side * 40, -60)],
                Stage.EnemySpeed * 1.3, i * 0.2);
    }

    /// <summary>A red squadron snaking down the screen: shoot all five and a power-up drops.</summary>
    private void SpawnSquadron()
    {
        int side = _rng.Next(2) == 0 ? -1 : 1;
        double x = Field.Width / 2 - side * 120;
        var path = new List<Vec> { new(x, -40), new(x + side * 220, 180), new(x - side * 40, 360), new(x + side * 220, 540), new(x, Field.Height + 60) };
        int id = _nextSquadron++;
        _squadrons[id] = (5, 0);
        for (int i = 0; i < 5; i++) Add(EnemyKind.Red, path, Stage.EnemySpeed * 0.95, i * 0.3, squadron: id);
    }

    /// <summary>A medium bomber lumbers down the screen, spraying shot.</summary>
    private void SpawnBomber()
    {
        double x = Rand(100, Field.Width - 100);
        Add(EnemyKind.Bomber, [new Vec(x, -60), new Vec(x + Rand(-80, 80), 300), new Vec(x + Rand(-120, 120), Field.Height + 80)],
            70, 0, hits: 8 + StageNumber);
    }

    private void SpawnBoss()
    {
        BossArrived = true;
        var b = Add(EnemyKind.Boss, [new Vec(Field.Width / 2, -140), new Vec(Field.Width / 2, 150)], 60, 0, hits: Stage.BossHits + Loop * 30);
        b.FireCooldown = 2;
        Play(Sfx.BossAlarm);
        Say("WARNING", 2.5);
    }

    // ───────────────────────────── moving ─────────────────────────────

    private void MoveThings(double dt)
    {
        foreach (var e in Enemies)
        {
            if (e.Delay > 0) { e.Delay -= dt; continue; }
            if (e.Dead || e.Gone) continue;
            e.Age += dt;
            if (e.Flash > 0) e.Flash -= dt;
            if (e.Kind == EnemyKind.Boss && e.Next >= e.Path.Count)
            {
                // The great bomber sways across the top of the screen.
                var want = new Vec(Field.Width / 2 + Math.Sin(e.Age * 0.45) * 150, 150 + Math.Sin(e.Age * 0.7) * 30);
                e.Vel = (want - e.Pos) * 2;
                e.Pos += e.Vel * dt;
            }
            else
            {
                var target = e.Path[Math.Min(e.Next, e.Path.Count - 1)];
                var to = target - e.Pos;
                double step = e.Speed * dt;
                if (to.Length <= step)
                {
                    e.Pos = target;
                    e.Next++;
                    if (e.Next >= e.Path.Count && e.Kind != EnemyKind.Boss) Escape(e);
                }
                else
                {
                    e.Vel = to.Unit * e.Speed;
                    e.Pos += e.Vel * dt;
                }
            }
            if (Phase == Phase.Playing) EnemyFire(e, dt);
        }
        Enemies.RemoveAll(e => e.Dead || e.Gone);

        foreach (var s in Shots)
        {
            s.Pos += s.Vel * dt;
            if (!Field.Inside(s.Pos, 30)) s.Dead = true;
        }
        Shots.RemoveAll(s => s.Dead);

        foreach (var p in Pows)
        {
            p.Age += dt;
            p.Pos += new Vec(Math.Sin(p.Age * 2) * 40, 45) * dt;
            if (p.Pos.Y > Field.Height + 30) p.Dead = true;
        }
        Pows.RemoveAll(p => p.Dead);
    }

    /// <summary>It got away: off the screen, unshot.</summary>
    private void Escape(Enemy e)
    {
        e.Gone = true;
        if (e.Squadron >= 0 && _squadrons.TryGetValue(e.Squadron, out var sq)) _squadrons[e.Squadron] = (sq.Left - 1, sq.Downed);
    }

    private void EnemyFire(Enemy e, double dt)
    {
        e.FireCooldown -= dt;
        if (e.FireCooldown > 0 || !Field.Inside(e.Pos, -20)) return;
        var toPlayer = Player.Pos - e.Pos;
        double speed = (230 + StageNumber * 6) * Difficulty;
        switch (e.Kind)
        {
            case EnemyKind.Bomber:
                e.FireCooldown = Rand(1.2, 1.8) / Difficulty;
                for (int i = -1; i <= 1; i++)
                    Shots.Add(new Shot { Pos = e.Pos + new Vec(0, 20), Vel = Vec.FromAngle(toPlayer.Angle + i * 0.25) * speed * 0.85, Hostile = true });
                break;
            case EnemyKind.Boss:
            {
                e.FireCooldown = Rand(0.6, 1.0) / Difficulty;
                // Turrets along the wings fire in turn; now and then the whole bomber lets go a fan.
                Vec[] turrets = [new(-110, 10), new(110, 10), new(-55, 30), new(55, 30)];
                if (_rng.NextDouble() < 0.25)
                    for (int i = -3; i <= 3; i++)
                        Shots.Add(new Shot { Pos = e.Pos + new Vec(0, 40), Vel = Vec.FromAngle(Math.PI + i * 0.2) * speed * 0.75, Hostile = true });
                else
                {
                    var t = e.Pos + turrets[_rng.Next(turrets.Length)];
                    Shots.Add(new Shot { Pos = t, Vel = (Player.Pos - t).Unit * speed, Hostile = true });
                }
                break;
            }
            default:
                e.FireCooldown = Rand(1.6, 3.4) / Difficulty;
                if (e.Pos.Y < Player.Pos.Y - 80 && e.Pos.Y > 20 && _rng.NextDouble() < Stage.FireRate * Difficulty)
                    Shots.Add(new Shot { Pos = e.Pos, Vel = toPlayer.Unit * speed, Hostile = true });
                break;
        }
    }

    // ───────────────────────────── hits ─────────────────────────────

    private void Collide()
    {
        foreach (var s in Shots.Where(s => !s.Hostile).ToList())
        {
            foreach (var e in Enemies)
            {
                if (!e.Active || (s.Pos - e.Pos).Length > e.Radius + 4) continue;
                s.Dead = true;
                Damage(e);
                break;
            }
            if (Phase != Phase.Playing) return;
        }

        foreach (var p in Pows.Where(p => (p.Pos - Player.Pos).Length < 32).ToList()) Collect(p);

        var me = Player;
        foreach (var w in me.Wingmen.Where(w => w.Alive))
        {
            var at = me.WingmanPos(w);
            var hitShot = Shots.FirstOrDefault(s => s.Hostile && (s.Pos - at).Length < 12);
            var hitPlane = Enemies.FirstOrDefault(e => e.Active && e.Kind != EnemyKind.Boss && (e.Pos - at).Length < e.Radius + 10);
            if (me.Safe || (hitShot is null && hitPlane is null)) continue;
            if (hitShot is not null) hitShot.Dead = true;
            if (hitPlane is not null) Kill(hitPlane);
            w.Alive = false;
            Explode(at);
            Play(Sfx.Hit);
        }

        if (me.Safe) return;
        string? cause = Shots.Any(s => s.Hostile && (s.Pos - me.Pos).Length < PlayerRadius + 3) ? "shot" : null;
        var rammed = Enemies.FirstOrDefault(e => e.Active && (e.Pos - me.Pos).Length < e.Radius + PlayerRadius - 2);
        if (rammed is not null)
        {
            if (rammed.Kind != EnemyKind.Boss) Kill(rammed);
            cause ??= "collision";
        }
        if (cause is not null)
        {
            LastDeath = cause;
            PlayerDown();
        }
    }

    private void Damage(Enemy e)
    {
        e.Hits--;
        if (e.Hits > 0)
        {
            e.Flash = 0.06;
            Play(e.Kind == EnemyKind.Boss ? Sfx.BossHit : Sfx.Hit);
            return;
        }
        Kill(e);
    }

    private void Kill(Enemy e)
    {
        e.Dead = true;
        int points = e.Kind switch
        {
            EnemyKind.Fighter => 50, EnemyKind.Red or EnemyKind.Grey => 100, EnemyKind.Bomber => 1000, _ => 5000 + 1000 * StageNumber,
        };
        AddScore(points);
        if (e.Kind == EnemyKind.Boss)
        {
            Explode(e.Pos, big: true);
            Explode(e.Pos + new Vec(-100, 10), big: true);
            Explode(e.Pos + new Vec(100, 10), big: true);
            Popup(e.Pos, points.ToString(), 2);
            Play(Sfx.Explode);
            BossDown();
            return;
        }
        Downed++;
        Explode(e.Pos, big: e.Kind == EnemyKind.Bomber);
        if (e.Kind == EnemyKind.Bomber) Popup(e.Pos, points.ToString(), 1.2);
        Play(e.Kind == EnemyKind.Bomber ? Sfx.Explode : Sfx.Kill);
        if (e.Squadron >= 0 && _squadrons.TryGetValue(e.Squadron, out var sq))
        {
            sq = (sq.Left - 1, sq.Downed + 1);
            _squadrons[e.Squadron] = sq;
            if (sq.Left == 0)
            {
                _squadrons.Remove(e.Squadron);
                if (sq.Downed == 5) DropPow(e.Pos);
            }
        }
    }

    private void DropPow(Vec at)
    {
        var p = Player;
        PowKind kind = _rng.NextDouble() < 0.08 ? PowKind.Life
            : !p.Wide ? PowKind.Wide
            : !p.Wingmen.Any(w => w.Alive) ? PowKind.Wingmen
            : (PowKind)new[] { PowKind.Roll, PowKind.Blast, PowKind.Points }[_rng.Next(3)];
        Pows.Add(new Pow { Kind = kind, Pos = at });
        Play(Sfx.PowAppear);
    }

    private void Collect(Pow p)
    {
        p.Dead = true;
        Play(Sfx.PowUp);
        string label;
        switch (p.Kind)
        {
            case PowKind.Wide:
                Player.Wide = true;
                label = "WIDE SHOT";
                break;
            case PowKind.Wingmen:
                Player.Wingmen.Clear();
                Player.Wingmen.Add(new Wingman { Side = -1 });
                Player.Wingmen.Add(new Wingman { Side = 1 });
                label = "WINGMEN";
                break;
            case PowKind.Roll:
                Player.Rolls++;
                label = "+1 ROLL";
                break;
            case PowKind.Blast:
                foreach (var e in Enemies.Where(e => e.Active && e.Kind != EnemyKind.Boss).ToList()) Kill(e);
                Shots.RemoveAll(s => s.Hostile);
                label = "BLAST!";
                break;
            case PowKind.Life:
                Lives++;
                Play(Sfx.ExtraLife);
                label = "1UP";
                break;
            default:
                AddScore(1000);
                label = "1000";
                break;
        }
        AddScore(500);
        Popup(p.Pos, label, 1.5);
    }

    private void PlayerDown()
    {
        Lives--;
        Explode(Player.Pos, big: true);
        foreach (var w in Player.Wingmen.Where(w => w.Alive)) Explode(Player.WingmanPos(w));
        Player.Wingmen.Clear();
        Player.Wide = false;
        Play(Sfx.Die);
        Music(false);
        SetPhase(Phase.Dying);
    }

    // ───────────────────────────── the end of a stage ─────────────────────────────

    private void BossDown()
    {
        foreach (var e in Enemies.Where(e => e.Active && e.Kind != EnemyKind.Boss)) Explode(e.Pos);
        Enemies.Clear();
        Shots.Clear();
        Music(false);
        Play(Sfx.StageClear);
        Say("STAGE CLEAR", 3);
        SetPhase(Phase.Landing);
        CarrierY = -260;
    }

    /// <summary>The carrier sails into view and the plane comes in to land on its deck.</summary>
    private void Land(double dt)
    {
        CarrierY = Math.Min(CarrierDeckY, (CarrierY ?? -260) + 160 * dt);
        if (PhaseTime > 1.5)
        {
            var deck = new Vec(Field.Width / 2, CarrierY!.Value);
            var to = deck - Player.Pos;
            Player.Pos += to.Length < 4 ? to : to.Unit * Math.Min(to.Length, 200 * dt);
        }
        if (PhaseTime > 5.5)
        {
            double pct = Spawned == 0 ? 1 : (double)Downed / Spawned;
            Perfect = Downed == Spawned && Spawned > 0;
            TallyBonus = (int)Math.Round(pct * 100) * 50 + (Perfect ? 10000 : 0) + Player.Rolls * 500;
            AddScore(TallyBonus);
            Play(Sfx.Tally);
            SetPhase(Phase.Tally);
        }
    }

    // ───────────────────────────── test hooks ─────────────────────────────

    /// <summary>Test hook: skip the take-off.</summary>
    internal void Go()
    {
        if (Phase != Phase.Takeoff) return;
        CarrierY = null;
        SetPhase(Phase.Playing);
        Message = null;
    }

    internal Enemy PutEnemy(EnemyKind kind, Vec at, int hits = 1, int squadron = -1)
    {
        var e = Add(kind, [at, at + new Vec(0, 0.001)], 0.0001, 0, hits, squadron);
        e.FireCooldown = 99;
        return e;
    }

    internal List<Enemy> PutSquadron(Vec at)
    {
        int id = _nextSquadron++;
        _squadrons[id] = (5, 0);
        return Enumerable.Range(0, 5).Select(i => PutEnemy(EnemyKind.Red, at + new Vec((i - 2) * 50, 0), squadron: id)).ToList();
    }

    internal void PutShot(Vec at, Vec vel) => Shots.Add(new Shot { Pos = at, Vel = vel, Hostile = true });
    internal void PutPow(PowKind kind, Vec at) => Pows.Add(new Pow { Kind = kind, Pos = at });
    internal void JumpTo(int stage) => StartStage(stage);
    internal void SkipToBoss() => StageTime = Stage.Length;

    internal void Fly(int ticks, Controls c)
    {
        for (int i = 0; i < ticks; i++) Tick(c);
    }

    internal void Idle(int ticks) => Fly(ticks, default);
}
