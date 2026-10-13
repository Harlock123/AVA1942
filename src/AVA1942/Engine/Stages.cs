namespace AVA1942.Engine;

/// <summary>
/// A stage: a stretch of ocean flown from one carrier to the next, with its waves of fighters, its
/// bombers, and a great bomber at the end. The names are this game's own.
/// </summary>
public sealed record Stage(string Name, uint Sea, uint SeaDeep, uint Land, double Length, double WaveGap, double FireRate, double EnemySpeed, int BossHits)
{
    public static readonly Stage[] All =
    [
        new("CORAL REACH", 0xFF2A6AB0, 0xFF1A4A88, 0xFF4A9A4A, 70, 2.4, 0.18, 210, 60),
        new("PALM ATOLL", 0xFF2A70A8, 0xFF1A4E80, 0xFF5AA048, 75, 2.2, 0.22, 220, 70),
        new("BLACK SANDS", 0xFF285E98, 0xFF183E70, 0xFF6A7A40, 80, 2.1, 0.26, 230, 80),
        new("TYPHOON STRAIT", 0xFF2A5A80, 0xFF183A58, 0xFF4A7A4A, 85, 2.0, 0.3, 240, 90),
        new("FIRE RIDGE", 0xFF2A5A90, 0xFF1A3A68, 0xFF7A6A40, 90, 1.9, 0.34, 250, 100),
        new("GHOST LAGOON", 0xFF205A70, 0xFF103A4A, 0xFF3A6A5A, 95, 1.8, 0.38, 260, 110),
        new("IRON HARBOUR", 0xFF305878, 0xFF1A3850, 0xFF5A6A5A, 100, 1.7, 0.42, 270, 120),
        new("LAST LIGHT", 0xFF3A4A80, 0xFF22285A, 0xFF5A5A48, 105, 1.6, 0.46, 280, 140),
    ];

    public static Stage Of(int number) => All[(number - 1) % All.Length];
}
