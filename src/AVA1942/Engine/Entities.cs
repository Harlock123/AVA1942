namespace AVA1942.Engine;

/// <summary>The stick (eight ways), the fire button and the roll.</summary>
public record struct Controls(int Dx, int Dy, bool Fire = false, bool Roll = false);

public enum EnemyKind
{
    /// <summary>Small fighters in waves: one hit.</summary>
    Fighter,
    /// <summary>A red squadron: shoot every one and a power-up drops.</summary>
    Red,
    /// <summary>Fast fighters that climb up from behind.</summary>
    Grey,
    /// <summary>Medium bombers: slow, tough, and they spray shot.</summary>
    Bomber,
    /// <summary>The great bomber that ends each stage.</summary>
    Boss,
}

public sealed class Enemy
{
    public EnemyKind Kind;
    public Vec Pos;
    public Vec Vel;
    /// <summary>The way it flies: points to pass through in turn, at its speed.</summary>
    public List<Vec> Path = [];
    public int Next;
    public double Speed;
    /// <summary>Seconds before it appears (a wave comes in one after another).</summary>
    public double Delay;
    public int Hits = 1;
    public double FireCooldown;
    public double Flash;
    /// <summary>The red squadron it belongs to, if any.</summary>
    public int Squadron = -1;
    public bool Dead;
    public bool Gone;
    public double Age;
    public bool Active => Delay <= 0 && !Dead && !Gone;
    public double Radius => Kind switch { EnemyKind.Bomber => 34, EnemyKind.Boss => 90, _ => 16 };
}

public sealed class Shot
{
    public Vec Pos;
    public Vec Vel;
    public bool Hostile;
    public bool Dead;
}

/// <summary>A power-up, dropped when a red squadron is wiped out.</summary>
public enum PowKind
{
    /// <summary>Wide shot.</summary>
    Wide,
    /// <summary>Two wingmen who fly and fire alongside.</summary>
    Wingmen,
    /// <summary>One more roll.</summary>
    Roll,
    /// <summary>Every enemy on the screen destroyed.</summary>
    Blast,
    /// <summary>Points.</summary>
    Points,
    /// <summary>An extra plane (rare).</summary>
    Life,
}

public sealed class Pow
{
    public PowKind Kind;
    public Vec Pos;
    public double Age;
    public bool Dead;
}

public sealed class Wingman
{
    /// <summary>-1 on the left, +1 on the right.</summary>
    public int Side;
    public bool Alive = true;
}

public enum EffectKind { Explosion, BigExplosion, Score, Splash }

public sealed class Effect
{
    public EffectKind Kind;
    public Vec Pos;
    public double Age;
    public double Life = 0.6;
    public string Text = "";
    public bool Done => Age >= Life;
}
