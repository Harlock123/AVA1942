namespace AVA1942.Engine;

public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator -(Vec a) => new(-a.X, -a.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSq => X * X + Y * Y;
    public Vec Unit => Length < 1e-9 ? new Vec(0, -1) : this * (1 / Length);

    /// <summary>A unit vector for a heading in radians: 0 is up the screen, turning clockwise.</summary>
    public static Vec FromAngle(double a) => new(Math.Sin(a), -Math.Cos(a));

    /// <summary>This vector's heading, in the same terms as <see cref="FromAngle"/>.</summary>
    public double Angle => Math.Atan2(X, -Y);
}

public static class Field
{
    public const double Step = 1.0 / 60;
    /// <summary>The playfield: a tall strip of sky and sea down the middle of the screen.</summary>
    public const double Width = 576, Height = 768;
    /// <summary>Where the playfield starts across the 1024-wide screen; the panels sit either side.</summary>
    public const double Left = 224;
    /// <summary>How fast the sea scrolls past underneath, in pixels a second.</summary>
    public const double ScrollSpeed = 70;

    public static bool Inside(Vec p, double margin = 0) =>
        p.X > -margin && p.Y > -margin && p.X < Width + margin && p.Y < Height + margin;
}
