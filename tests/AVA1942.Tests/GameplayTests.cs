using AVA1942.Engine;

namespace AVA1942.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "ava1942-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>A game under way, the take-off skipped, and no wave arriving unless a test puts it there.</summary>
    private static GameEngine Game(int stage = 1)
    {
        var g = new GameEngine(7, HighScoreTable.Load(TempFolder())) { Silent = true, Calm = true };
        g.StartGame();
        if (stage != 1) g.JumpTo(stage);
        g.Go();
        g.Idle(1);
        return g;
    }

    [Fact]
    public void A_stage_starts_with_a_take_off_from_the_carrier()
    {
        var g = new GameEngine(7, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.StartGame();
        Assert.Equal(Phase.Takeoff, g.Phase);
        Assert.Equal(GameEngine.CarrierDeckY, g.CarrierY);
        g.Fly(30, new Controls(1, 0, true));
        Assert.Equal(Field.Width / 2, g.Player.Pos.X);
        Assert.Empty(g.Shots);
        g.Idle(60 * 3);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Null(g.CarrierY);
    }

    [Fact]
    public void The_plane_flies_eight_ways_and_stays_on_the_screen()
    {
        var g = Game();
        var start = g.Player.Pos;
        g.Fly(30, new Controls(1, 0));
        Assert.InRange(g.Player.Pos.X - start.X, 130, 140);
        g.Fly(600, new Controls(-1, -1));
        Assert.True(g.Player.Pos.X >= 26 && g.Player.Pos.Y >= 60);
    }

    [Fact]
    public void Twin_guns_and_with_the_wide_shot_four()
    {
        var g = Game();
        g.Fly(1, new Controls(0, 0, true));
        Assert.Equal(2, g.Shots.Count);
        Assert.All(g.Shots, s => Assert.True(s.Vel.Y < -800 && Math.Abs(s.Vel.X) < 1));
        g.Shots.Clear();
        g.PutPow(PowKind.Wide, g.Player.Pos);
        g.Idle(10);
        Assert.True(g.Player.Wide);
        g.Fly(1, new Controls(0, 0, true));
        Assert.Equal(4, g.Shots.Count);
        Assert.Equal(2, g.Shots.Count(s => Math.Abs(s.Vel.X) > 100));
    }

    [Fact]
    public void A_fighter_goes_down_with_one_hit_and_a_bomber_takes_many()
    {
        var g = Game();
        var f = g.PutEnemy(EnemyKind.Fighter, g.Player.Pos + new Vec(-7, -250));
        g.Fly(20, new Controls(0, 0, true));
        Assert.True(f.Dead);
        Assert.Equal(50, g.Score);
        var b = g.PutEnemy(EnemyKind.Bomber, g.Player.Pos + new Vec(0, -300), hits: 9);
        g.Fly(6, new Controls(0, 0, true));
        Assert.False(b.Dead);
        for (int i = 0; i < 120 && !b.Dead; i++) g.Tick(new Controls(0, 0, true));
        Assert.True(b.Dead);
        Assert.Equal(50 + 1000, g.Score);
    }

    [Fact]
    public void A_whole_red_squadron_shot_down_drops_a_pow()
    {
        var g = Game();
        var squad = g.PutSquadron(new Vec(Field.Width / 2, 200));
        foreach (var e in squad.Take(4))
        {
            g.Player.Pos = new Vec(e.Pos.X + 7, 600);
            for (int i = 0; i < 60 && !e.Dead; i++) g.Tick(new Controls(0, 0, true));
        }
        Assert.Empty(g.Pows);
        g.Player.Pos = new Vec(squad[4].Pos.X + 7, 600);
        for (int i = 0; i < 60 && !squad[4].Dead; i++) g.Tick(new Controls(0, 0, true));
        Assert.Single(g.Pows);
    }

    [Fact]
    public void One_escaped_red_and_there_is_no_pow()
    {
        var g = Game();
        var squad = g.PutSquadron(new Vec(Field.Width / 2, 200));
        squad[0].Gone = true;
        typeof(GameEngine).GetMethod("Escape", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(g, [squad[0]]);
        foreach (var e in squad.Skip(1))
        {
            g.Player.Pos = new Vec(e.Pos.X + 7, 600);
            for (int i = 0; i < 60 && !e.Dead; i++) g.Tick(new Controls(0, 0, true));
        }
        Assert.Empty(g.Pows);
    }

    [Theory]
    [InlineData(PowKind.Wingmen)]
    [InlineData(PowKind.Roll)]
    [InlineData(PowKind.Blast)]
    [InlineData(PowKind.Points)]
    [InlineData(PowKind.Life)]
    public void Each_pow_does_its_thing(PowKind kind)
    {
        var g = Game();
        var e = g.PutEnemy(EnemyKind.Fighter, new Vec(100, 100));
        int rolls = g.Player.Rolls, lives = g.Lives, score = g.Score;
        g.PutPow(kind, g.Player.Pos);
        g.Idle(2);
        switch (kind)
        {
            case PowKind.Wingmen: Assert.Equal(2, g.Player.Wingmen.Count(w => w.Alive)); break;
            case PowKind.Roll: Assert.Equal(rolls + 1, g.Player.Rolls); break;
            case PowKind.Blast: Assert.True(e.Dead); break;
            case PowKind.Points: Assert.Equal(score + 1500, g.Score); break;
            case PowKind.Life: Assert.Equal(lives + 1, g.Lives); break;
        }
    }

    [Fact]
    public void Wingmen_fire_too_and_take_a_hit_for_the_plane()
    {
        var g = Game();
        g.PutPow(PowKind.Wingmen, g.Player.Pos);
        g.Idle(2);
        g.Fly(1, new Controls(0, 0, true));
        Assert.Equal(4, g.Shots.Count);
        var wing = g.Player.WingmanPos(g.Player.Wingmen[0]);
        g.PutShot(wing + new Vec(0, -30), new Vec(0, 400));
        g.Idle(10);
        Assert.False(g.Player.Wingmen[0].Alive);
        Assert.Equal(Phase.Playing, g.Phase);
    }

    [Fact]
    public void A_roll_carries_the_plane_through_anything_but_uses_one_up()
    {
        var g = Game();
        Assert.Equal(GameEngine.RollsPerStage, g.Player.Rolls);
        g.Fly(1, new Controls(0, 0, false, true));
        Assert.True(g.Player.Rolling);
        Assert.Equal(GameEngine.RollsPerStage - 1, g.Player.Rolls);
        g.PutShot(g.Player.Pos + new Vec(0, -30), new Vec(0, 300));
        g.PutEnemy(EnemyKind.Fighter, g.Player.Pos + new Vec(0, -40));
        g.Fly(20, new Controls(0, 0, true, true));
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.DoesNotContain(g.Shots, s => !s.Hostile);
        g.Idle(120);
        Assert.False(g.Player.Rolling);
    }

    [Fact]
    public void Shot_down_loses_a_life_and_the_power_ups()
    {
        var g = Game();
        g.PutPow(PowKind.Wide, g.Player.Pos);
        g.Idle(2);
        g.PutShot(g.Player.Pos + new Vec(0, -30), new Vec(0, 300));
        g.Idle(10);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Equal("shot", g.LastDeath);
        Assert.Equal(GameEngine.StartingLives - 1, g.Lives);
        Assert.False(g.Player.Wide);
        g.Idle(60 * 3);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.True(g.Player.Invulnerable > 0);
    }

    [Fact]
    public void Running_out_of_planes_ends_the_game_and_a_good_score_goes_in_the_table()
    {
        var folder = TempFolder();
        var g = new GameEngine(3, HighScoreTable.Load(folder)) { Silent = true, Calm = true };
        g.StartGame();
        g.Go();
        g.AddScore(70000);
        for (int i = 0; i < 12 && g.State == GameState.Playing; i++)
        {
            if (g.Phase == Phase.Playing && !g.Player.Safe) g.PutShot(g.Player.Pos + new Vec(0, -20), new Vec(0, 300));
            g.Idle(60 * 4);
        }
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('L');
        g.Initials.Type('W');
        g.Initials.Type('X');
        g.Initials.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal("LWX", HighScoreTable.Load(folder).Entries[0].Initials);
    }

    [Fact]
    public void At_the_end_of_the_stage_the_great_bomber_comes_and_downing_it_lands_the_plane_with_a_tally()
    {
        var g = Game();
        g.PutEnemy(EnemyKind.Fighter, g.Player.Pos + new Vec(-7, -250));
        g.Fly(30, new Controls(0, 0, true));
        g.Calm = false;
        g.SkipToBoss();
        g.Idle(2);
        var boss = g.Boss;
        Assert.NotNull(boss);
        g.Calm = true;
        for (int i = 0; i < 60 * 6; i++)
        {
            g.Player.Invulnerable = 1;
            g.Tick(default);
        }
        Assert.InRange(boss!.Pos.Y, 100, 200);
        int score = g.Score;
        for (int i = 0; i < 60 * 60 && g.Phase == Phase.Playing; i++)
        {
            g.Player.Pos = new Vec(boss.Pos.X + 7, 640);
            g.Player.Invulnerable = 1;
            g.Tick(new Controls(0, 0, true));
        }
        Assert.Equal(Phase.Landing, g.Phase);
        Assert.True(g.Score >= score + 6000);
        g.Idle(60 * 6);
        Assert.Equal(Phase.Tally, g.Phase);
        Assert.Equal(1, g.Downed);
        Assert.Equal(1, g.Spawned);
        Assert.True(g.Perfect);
        Assert.Equal(100 * 50 + 10000 + GameEngine.RollsPerStage * 500, g.TallyBonus);
        Assert.Equal(Field.Width / 2, g.Player.Pos.X, 1);
        g.Idle(60 * 5);
        Assert.Equal(2, g.StageNumber);
        Assert.Equal(Phase.Takeoff, g.Phase);
        Assert.Equal(GameEngine.RollsPerStage, g.Player.Rolls);
    }

    [Fact]
    public void Waves_arrive_by_themselves_and_red_squadrons_and_bombers_turn_up()
    {
        var g = Game(2);
        g.Calm = false;
        bool red = false, bomber = false, grey = false;
        for (int i = 0; i < 60 * 70; i++)
        {
            g.Player.Invulnerable = 1;
            g.Tick(default);
            red |= g.Enemies.Any(e => e.Kind == EnemyKind.Red);
            bomber |= g.Enemies.Any(e => e.Kind == EnemyKind.Bomber);
            grey |= g.Enemies.Any(e => e.Kind == EnemyKind.Grey);
        }
        Assert.True(red);
        Assert.True(bomber);
        Assert.True(grey);
        Assert.True(g.Spawned > 60);
    }

    [Fact]
    public void Extra_planes_at_twenty_thousand_and_every_seventy_thousand()
    {
        var g = Game();
        int lives = g.Lives;
        g.AddScore(20000);
        Assert.Equal(lives + 1, g.Lives);
        g.AddScore(49999);
        Assert.Equal(lives + 1, g.Lives);
        g.AddScore(1);
        Assert.Equal(lives + 2, g.Lives);
        g.AddScore(70000);
        Assert.Equal(lives + 3, g.Lives);
    }

    [Fact]
    public void After_eight_stages_they_come_round_again()
    {
        var g = Game(9);
        Assert.Equal(Stage.All[0].Name, g.Stage.Name);
        Assert.Equal(1, g.Loop);
    }

    [Fact]
    public void The_title_turns_into_the_demonstration_then_the_table_then_the_title()
    {
        var g = new GameEngine(5, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.Idle(60 * 16);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle(60 * 46);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(60 * 10);
        Assert.Equal(GameState.Title, g.State);
        g.Tick(default, start: true);
        Assert.Equal(GameState.Playing, g.State);
    }

    [Fact]
    public void Pausing_stops_the_clock()
    {
        var g = Game();
        g.Pause();
        double t = g.StageTime;
        g.Idle(60);
        Assert.Equal(t, g.StageTime);
        g.Resume();
        g.Idle(1);
        Assert.True(g.StageTime > t);
    }

    [Fact]
    public void The_computer_pilot_flies_the_first_stages_through()
    {
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var g = new GameEngine(seed, HighScoreTable.Load(TempFolder())) { Silent = true };
            g.StartGame();
            var pilot = new AttractPilot(seed);
            for (int t = 0; t < 60 * 200 && g.State == GameState.Playing && g.StageNumber < 3; t++) g.Tick(pilot.Decide(g));
            Assert.True(g.StageNumber >= 3, $"seed {seed}: stuck at stage {g.StageNumber}");
        }
    }

    [Fact]
    public void Every_sound_has_a_file()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop))
            Assert.True(File.Exists(Path.Combine(dir, s + ".wav")), s.ToString());
    }

    [Fact]
    public void Every_sprite_is_16_by_16()
    {
        foreach (var (name, rows) in AVA1942.Views.Sprites.All)
        {
            Assert.Equal(16, rows.Length);
            Assert.All(rows, r => Assert.True(r.Length == 16, $"{name}: {r}"));
        }
    }
}
