using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVA1942.Engine;
using AVA1942.Views;

namespace AVA1942.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set AVA1942_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "ava1942-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("AVA1942_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVA1942-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Plays with the computer pilot until something worth a picture is on screen.</summary>
    private static void PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int stage, int seed, int maxSeconds = 150)
    {
        g.StartGame();
        if (stage != 1) g.JumpTo(stage);
        var pilot = new AttractPilot(seed);
        for (int t = 0; t < 60 * maxSeconds && !done(g); t++)
        {
            g.Tick(pilot.Decide(g));
            if (g.State != GameState.Playing) { g.StartGame(); g.JumpTo(stage); }
        }
        c.Advance(0.016);
    }

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(60);
        c.Advance(0.3);
        Shot(w, c, "title");
        g.Idle(60 * 16 + 60 * 46);
        c.Advance(0.1);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void Take_off_and_battle()
    {
        var (w, c, g) = Open(21);
        g.StartGame();
        g.Idle(70);
        Shot(w, c, "takeoff");
        (w, c, g) = Open(22);
        PlayUntil(g, c, x => x.Phase == Phase.Playing && x.StageTime > 20 && x.Enemies.Count(e => e.Active && e.Pos.Y > 60 && e.Pos.Y < 500) >= 5 && x.Shots.Count(s => s.Hostile) >= 2, 1, 2);
        Shot(w, c, "battle");
        (w, c, g) = Open(23);
        PlayUntil(g, c, x => x.Phase == Phase.Playing && x.Enemies.Any(e => e.Active && e.Kind == EnemyKind.Red && e.Pos.Y > 120) && x.Player.Wide, 3, 3);
        Shot(w, c, "squadron");
        (w, c, g) = Open(24);
        PlayUntil(g, c, x => x.Phase == Phase.Playing && x.Enemies.Any(e => e.Active && e.Kind == EnemyKind.Bomber && e.Pos.Y > 150 && e.Pos.Y < 420), 4, 4);
        Shot(w, c, "bomber");
        (w, c, g) = Open(25);
        PlayUntil(g, c, x => x.Player.Rolling && x.Player.RollTime < 1.0 && x.Player.RollTime > 0.8, 5, 5, 300);
        Shot(w, c, "roll");
    }

    [AvaloniaFact]
    public void The_great_bomber_and_the_landing()
    {
        var (w, c, g) = Open(31);
        PlayUntil(g, c, x => x.Boss is { } b && b.Pos.Y > 120 && b.Age > 6 && x.Shots.Count(s => s.Hostile) > 3, 2, 1, 200);
        Shot(w, c, "great-bomber");
        (w, c, g) = Open(32);
        PlayUntil(g, c, x => x.Phase == Phase.Tally && x.PhaseTime > 1, 1, 1, 200);
        Shot(w, c, "tally");
    }
}
