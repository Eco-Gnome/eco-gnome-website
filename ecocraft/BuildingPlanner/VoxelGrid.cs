namespace ecocraft.BuildingPlanner;

public enum VoxelKind : byte
{
    Air,
    Terrain,        // sol par défaut (tier 0, mur)
    Block,          // bloc de construction posé par le plan (mur, sol, plafond)
    ObjectOccupied, // WorldObjectBlock [Occupied] : bloque la pose, traversé par la détection de pièce, compte dans le volume
    ObjectWall,     // BuildingWorldObjectBlock [Solid, Wall] : portes, interrupteurs
    ObjectSolid,    // PipeSlotBlock [Solid]
}

public struct Voxel
{
    public VoxelKind Kind;
    public int MaterialIndex;   // index dans BuildContext.Materials (Block), sinon -1
    public int ObjectIndex;     // index de l'objet posé (Object*), sinon -1
    public bool IsCeiling;
    public bool IsFloor;

    public static readonly Voxel Air = new() { Kind = VoxelKind.Air, MaterialIndex = -1, ObjectIndex = -1 };
    public static readonly Voxel Terrain = new() { Kind = VoxelKind.Terrain, MaterialIndex = -1, ObjectIndex = -1, IsFloor = true };

    public bool IsSolid => VoxelGrid.IsSolidKind(Kind);
    public bool IsObject => Kind is VoxelKind.ObjectOccupied or VoxelKind.ObjectWall or VoxelKind.ObjectSolid;
}

// Grille voxel du plan. X ∈ [0, SizeX), Z ∈ [0, SizeZ) (axes horizontaux du plan), Y ∈ [0, SizeY) vertical.
// Hors grille : terrain plat — Y ≤ 0 solide, au-dessus de l'air sans plafond (TopSolid = 0).
public sealed class VoxelGrid
{
    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }

    private readonly Voxel[] _cells;
    private readonly int[] _topSolid;

    public VoxelGrid(int sizeX, int sizeY, int sizeZ)
    {
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        _cells = new Voxel[sizeX * sizeY * sizeZ];
        Array.Fill(_cells, Voxel.Air);
        _topSolid = new int[sizeX * sizeZ];
    }

    public static bool IsSolidKind(VoxelKind kind) => kind is VoxelKind.Terrain or VoxelKind.Block or VoxelKind.ObjectWall or VoxelKind.ObjectSolid;

    public bool InBounds(Vec3i p) => p.X >= 0 && p.Y >= 0 && p.Z >= 0 && p.X < SizeX && p.Y < SizeY && p.Z < SizeZ;
    public bool InBoundsXz(int x, int z) => x >= 0 && z >= 0 && x < SizeX && z < SizeZ;

    public int Index(Vec3i p) => (p.Y * SizeZ + p.Z) * SizeX + p.X;

    public Voxel Get(Vec3i p)
    {
        if (InBounds(p)) return _cells[Index(p)];
        return p.Y <= 0 ? Voxel.Terrain : Voxel.Air;
    }

    public void Set(Vec3i p, Voxel v)
    {
        if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p, "Voxel outside the grid.");
        _cells[Index(p)] = v;
    }

    // Plus haut voxel solide de la colonne (World.GetTopSolidBlockY). Hors grille : 0 (terrain).
    public int TopSolidY(int x, int z) => InBoundsXz(x, z) ? _topSolid[z * SizeX + x] : 0;

    public void RecomputeTopSolid()
    {
        for (var z = 0; z < SizeZ; z++)
        for (var x = 0; x < SizeX; x++)
        {
            var top = 0;
            for (var y = SizeY - 1; y >= 0; y--)
            {
                if (IsSolidKind(_cells[(y * SizeZ + z) * SizeX + x].Kind)) { top = y; break; }
            }
            _topSolid[z * SizeX + x] = top;
        }
    }
}
