namespace AVA1942.Engine;

/// <summary>
/// The computer pilot for the attract mode (and the tests). It slides out of the way of shots and
/// planes coming at it (rolling if it has to), picks up power-ups, and otherwise lines up under the
/// nearest enemy with the trigger held down.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private int _rollHeld;

    public Controls Decide(GameEngine g)
    {
        var me = g.Player;
        bool roll = false;
        if (_rollHeld > 0) _rollHeld--;

        // What will pass close in the next moment, and how soon?
        double soonest = double.MaxValue;
        double pushX = 0, pushY = 0;
        void Threat(Vec pos, Vec vel, double radius)
        {
            var rel = pos - me.Pos;
            double t = Math.Clamp(-(rel.X * vel.X + rel.Y * vel.Y) / Math.Max(1, vel.LengthSq), 0, 0.9);
            var at = rel + vel * t;
            if (at.Length > radius + 26) return;
            soonest = Math.Min(soonest, t);
            double weight = 1 / (0.15 + t);
            pushX += (at.X <= 0 ? 1 : -1) * weight;
            if (at.Y < 0 && Math.Abs(at.X) < radius) pushY += weight * 0.5;
        }
        foreach (var s in g.Shots.Where(s => s.Hostile)) Threat(s.Pos, s.Vel, 6);
        foreach (var e in g.Enemies.Where(e => e.Active)) Threat(e.Pos, e.Vel, e.Radius);

        if (soonest < 0.18 && me.Rolls > 0 && !me.Rolling && _rollHeld == 0)
        {
            roll = true;
            _rollHeld = 20;
        }

        int dx = 0, dy = 0;
        if (pushX != 0)
        {
            dx = Math.Sign(pushX);
            // Against the edge, go the other way.
            if (me.Pos.X < 60 && dx < 0 || me.Pos.X > Field.Width - 60 && dx > 0) dx = -dx;
            dy = pushY > 0.5 ? 1 : 0;
        }
        else
        {
            Vec? goal = null;
            var pow = g.Pows.OrderBy(p => (p.Pos - me.Pos).Length).FirstOrDefault();
            if (pow is not null) goal = pow.Pos;
            else
            {
                var target = g.Enemies.Where(e => e.Active && e.Pos.Y < me.Pos.Y - 60 && e.Pos.Y > -10)
                    .OrderBy(e => e.Kind == EnemyKind.Boss ? 0 : 1).ThenBy(e => Math.Abs(e.Pos.X - me.Pos.X) + (me.Pos.Y - e.Pos.Y) * 0.3).FirstOrDefault();
                if (target is not null)
                {
                    // Under it, leading by its sideways drift.
                    double t = (me.Pos.Y - target.Pos.Y) / GameEngine.ShotSpeed;
                    goal = new Vec(target.Pos.X + target.Vel.X * t, 640);
                }
                else goal = new Vec(Field.Width / 2, 640);
            }
            var to = goal.Value - me.Pos;
            dx = Math.Abs(to.X) > 8 ? Math.Sign(to.X) : 0;
            dy = Math.Abs(to.Y) > 16 ? Math.Sign(to.Y) : 0;
            // Don't go up into the thick of it chasing a power-up.
            if (pow is null && me.Pos.Y < 560 && dy < 0) dy = 0;
        }
        // A short break in the firing now and then, as a person's thumb would.
        bool fire = _rng.NextDouble() > 0.03;
        return new Controls(dx, dy, fire, roll);
    }
}
