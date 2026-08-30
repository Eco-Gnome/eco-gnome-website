namespace ecocraft.BuildingPlanner;

// Axes Eco : X et Z horizontaux, Y vertical. Axes du plan 2D : x → X, y → Z, z (hauteur) → Y.
public readonly record struct Vec3i(int X, int Y, int Z)
{
    public static readonly Vec3i Zero = new(0, 0, 0);

    public static Vec3i operator +(Vec3i a, Vec3i b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3i operator -(Vec3i a, Vec3i b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public bool IsDiagonal => Math.Abs(X) + Math.Abs(Y) + Math.Abs(Z) > 1;

    public override string ToString() => $"({X},{Y},{Z})";
}

public static class Geometry
{
    // Rotation entière autour de l'axe vertical, r quarts de tour. Les quatre empreintes sont celles du jeu
    // (Quaternion.RotateVector + arrondi half-up) ; seul l'étiquetage « rotation 1 » peut différer.
    public static Vec3i Rotate(Vec3i o, int r) => (r & 3) switch
    {
        0 => o,
        1 => new Vec3i(o.Z, o.Y, -o.X),
        2 => new Vec3i(-o.X, o.Y, -o.Z),
        _ => new Vec3i(-o.Z, o.Y, o.X),
    };

    public static Vec3i PlanToEco(int x, int y, int z) => new(x, z, y);

    public static readonly Vec3i[] Offsets26 = BuildOffsets26();

    public static readonly Vec3i[] Offsets4Xz =
    [
        new Vec3i(-1, 0, 0), new Vec3i(1, 0, 0), new Vec3i(0, 0, -1), new Vec3i(0, 0, 1),
    ];

    public static readonly (int X, int Y)[] PlanNeighbors4 = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    public static readonly (int X, int Y)[] PlanNeighbors8 =
    [
        (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
    ];

    // Distance euclidienne 3D, comme Vector3i.WrappedDistance (float) dans le jeu.
    public static float Distance(Vec3i a, Vec3i b)
    {
        var d = a - b;
        return MathF.Sqrt((float)d.X * d.X + (float)d.Y * d.Y + (float)d.Z * d.Z);
    }

    private static Vec3i[] BuildOffsets26()
    {
        var list = new List<Vec3i>(26);
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        for (var z = -1; z <= 1; z++)
        {
            if (x == 0 && y == 0 && z == 0) continue;
            list.Add(new Vec3i(x, y, z));
        }
        return list.ToArray();
    }
}
