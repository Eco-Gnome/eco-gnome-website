using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Rastérisation des opérations du mode architecture. Même spécification que evalOps dans building-planner.js :
// tout est calculé en entiers (pas de flottant) pour que le rendu JS et la nomenclature C# comptent exactement
// les mêmes blocs. Boîte englobante inclusive [x0..x1]×[y0..y1]×[z0..z1] ; R = étendue par axe, d = 2·coord − x0 − x1
// (double de l'écart au centre, entier même pour un diamètre pair). Sphère : Σ d²·(produit des autres R²) ≤ ΠR².
// Creux : dans l'extérieur et hors de la boîte rétrécie de Thickness sur les axes concernés (box : x,y ; sphere :
// x,y,z ; cylinder : les deux axes radiaux) ; une boîte intérieure qui s'inverse rend la forme pleine.
// Les cellules hors [0,w)×[0,d)×[0,h) sont ignorées. Coordonnées du plan : (x, y, z) avec z vertical.
public static class ArchShapes
{
    public static readonly string[] Kinds = ["box", "sphere", "cylinder", "line", "curve", "cells"];

    private readonly record struct Bounds(int X0, int X1, int Y0, int Y1, int Z0, int Z1)
    {
        public static Bounds From(int[] a, int[] b) => new(Math.Min(a[0], b[0]), Math.Max(a[0], b[0]), Math.Min(a[1], b[1]), Math.Max(a[1], b[1]), Math.Min(a[2], b[2]), Math.Max(a[2], b[2]));

        public Bounds Shrink(int t, bool x, bool y, bool z) => new(X0 + (x ? t : 0), X1 - (x ? t : 0), Y0 + (y ? t : 0), Y1 - (y ? t : 0), Z0 + (z ? t : 0), Z1 - (z ? t : 0));

        public bool IsEmpty => X0 > X1 || Y0 > Y1 || Z0 > Z1;
    }

    // Appelle set(x, y, z) pour chaque cellule de l'opération dans [0,w)×[0,d)×[0,h) (ArchLayer.Evaluate les accumule).
    public static void Paint(ArchOp op, int w, int d, int h, Action<int, int, int> set)
    {
        switch (op.Kind)
        {
            case "cells":
                if (op.Cells is null) return;
                for (var i = 0; i + 2 < op.Cells.Length; i += 3) Set(op.Cells[i], op.Cells[i + 1], op.Cells[i + 2]);
                return;
            case "line":
                if (op.A is not { Length: 3 } || op.B is not { Length: 3 }) return;
                foreach (var (x, y, z) in LineCells(op.A, op.B)) Set(x, y, z);
                return;
            case "curve":
                if (op.A is not { Length: 3 } || op.B is not { Length: 3 } || op.C is not { Length: 3 }) return;
                foreach (var (x, y, z) in CurveCells(op.A, op.C, op.B)) Set(x, y, z);
                return;
            case "box": case "sphere": case "cylinder":
                if (op.A is not { Length: 3 } || op.B is not { Length: 3 }) return;
                var outer = Bounds.From(op.A, op.B);
                var axis = op.Kind == "cylinder" ? NormalizeAxis(op.Axis) : 'z';
                var inner = op.Hollow ? Shrunk(op.Kind, axis, outer, Math.Max(1, op.Thickness)) : null;
                for (var z = Math.Max(0, outer.Z0); z <= Math.Min(h - 1, outer.Z1); z++)
                    for (var y = Math.Max(0, outer.Y0); y <= Math.Min(d - 1, outer.Y1); y++)
                        for (var x = Math.Max(0, outer.X0); x <= Math.Min(w - 1, outer.X1); x++)
                            if (Inside(op.Kind, axis, outer, x, y, z) && !(inner is { } inn && Inside(op.Kind, axis, inn, x, y, z)))
                                set(x, y, z);
                return;
        }

        void Set(int x, int y, int z)
        {
            if (x >= 0 && y >= 0 && z >= 0 && x < w && y < d && z < h) set(x, y, z);
        }
    }

    // Plus haute couche touchée par l'op (−1 si invalide) : dimensionne la grille.
    public static int MaxZ(ArchOp op)
    {
        if (op.Kind == "cells")
        {
            var max = -1;
            if (op.Cells is not null) for (var i = 2; i < op.Cells.Length; i += 3) max = Math.Max(max, op.Cells[i]);
            return max;
        }
        if (op.A is not { Length: 3 } || op.B is not { Length: 3 }) return -1;
        var top = Math.Max(op.A[2], op.B[2]);
        return op.Kind == "curve" && op.C is { Length: 3 } ? Math.Max(top, op.C[2]) : top;
    }

    // Nombre de cellules visitées par Paint (boîte englobante ∩ grille ; ligne : n + 1 ; cells : triplets) — budget du validateur.
    public static long WorkVolume(ArchOp op, int w, int d, int h)
    {
        switch (op.Kind)
        {
            case "cells": return (op.Cells?.Length ?? 0) / 3;
            case "line":
                if (op.A is not { Length: 3 } || op.B is not { Length: 3 }) return 0;
                return Cheb(op.A, op.B) + 1;
            case "curve":
                if (op.A is not { Length: 3 } || op.B is not { Length: 3 } || op.C is not { Length: 3 }) return 0;
                return 2 * Math.Max(Cheb(op.A, op.C), Cheb(op.C, op.B)) + 1;
            default:
                if (op.A is not { Length: 3 } || op.B is not { Length: 3 }) return 0;
                var b = Bounds.From(op.A, op.B);
                long sx = Math.Min(w - 1, b.X1) - Math.Max(0, b.X0) + 1, sy = Math.Min(d - 1, b.Y1) - Math.Max(0, b.Y0) + 1, sz = Math.Min(h - 1, b.Z1) - Math.Max(0, b.Z0) + 1;
                return sx <= 0 || sy <= 0 || sz <= 0 ? 0 : sx * sy * sz;
        }
    }

    public static char NormalizeAxis(string? axis) => axis is "x" or "y" ? axis[0] : 'z';

    private static Bounds? Shrunk(string kind, char axis, Bounds outer, int t)
    {
        var inner = kind switch
        {
            "box" => outer.Shrink(t, true, true, false),
            "sphere" => outer.Shrink(t, true, true, true),
            _ => outer.Shrink(t, axis != 'x', axis != 'y', axis != 'z'),
        };
        return inner.IsEmpty ? null : inner;
    }

    private static bool Inside(string kind, char axis, Bounds b, int x, int y, int z)
    {
        if (x < b.X0 || x > b.X1 || y < b.Y0 || y > b.Y1 || z < b.Z0 || z > b.Z1) return false;
        if (kind == "box") return true;
        long rx = b.X1 - b.X0 + 1, ry = b.Y1 - b.Y0 + 1, rz = b.Z1 - b.Z0 + 1;
        long dx = 2L * x - b.X0 - b.X1, dy = 2L * y - b.Y0 - b.Y1, dz = 2L * z - b.Z0 - b.Z1;
        if (kind == "sphere") return dx * dx * ry * ry * rz * rz + dy * dy * rx * rx * rz * rz + dz * dz * rx * rx * ry * ry <= rx * rx * ry * ry * rz * rz;
        return axis switch
        {
            'x' => dy * dy * rz * rz + dz * dz * ry * ry <= ry * ry * rz * rz,
            'y' => dx * dx * rz * rz + dz * dz * rx * rx <= rx * rx * rz * rz,
            _ => dx * dx * ry * ry + dy * dy * rx * rx <= rx * rx * ry * ry,
        };
    }

    // Ligne 3D : n + 1 cellules, chaque coordonnée arrondie au plus proche (floor((2·a·n + 2·Δ·k + n) / 2n)).
    private static IEnumerable<(int X, int Y, int Z)> LineCells(int[] a, int[] b)
    {
        var n = Math.Max(Math.Abs(b[0] - a[0]), Math.Max(Math.Abs(b[1] - a[1]), Math.Abs(b[2] - a[2])));
        if (n == 0) { yield return (a[0], a[1], a[2]); yield break; }
        for (var k = 0; k <= n; k++)
            yield return (Step(a[0], b[0], k, n), Step(a[1], b[1], k, n), Step(a[2], b[2], k, n));
    }

    private static int Step(int a, int b, int k, int n) => FloorDiv(2L * a * n + 2L * (b - a) * k + n, 2L * n);

    // Bézier quadratique a → b tirée par c : 2·max(|c−a|, |b−c|) + 1 échantillons (un pas ≤ 1 case par axe, donc
    // une chaîne 26-connexe), arrondis au plus proche, doublons consécutifs fusionnés — même arithmétique que curveCells en JS.
    public static IEnumerable<(int X, int Y, int Z)> CurveCells(int[] a, int[] c, int[] b)
    {
        var n = 2 * Math.Max(Cheb(a, c), Cheb(c, b));
        if (n == 0) { yield return (a[0], a[1], a[2]); yield break; }
        long den = (long)n * n;
        (int X, int Y, int Z)? last = null;
        for (var k = 0; k <= n; k++)
        {
            int At(int i) => FloorDiv(2L * ((long)(n - k) * (n - k) * a[i] + 2L * k * (n - k) * c[i] + (long)k * k * b[i]) + den, 2L * den);
            var p = (At(0), At(1), At(2));
            if (p == last) continue;
            last = p;
            yield return p;
        }
    }

    private static int Cheb(int[] p, int[] q) => Math.Max(Math.Abs(q[0] - p[0]), Math.Max(Math.Abs(q[1] - p[1]), Math.Abs(q[2] - p[2])));

    private static int FloorDiv(long num, long den)
    {
        var q = num / den;
        if (num % den != 0 && num < 0) q--;
        return (int)q;
    }
}
