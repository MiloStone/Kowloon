using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds body and top-cap meshes for a tetromino in tile-local space.
/// Anchor cell (0,0) sits at local origin; other cells offset by multiples of step.
/// Body mesh: outer walls + bottom face. Adjacent cells share exact edges — no seam.
/// Top mesh: top faces only (revealed via PlacedTile.RevealTop when the floor above completes).
/// </summary>
public static class TileMeshBuilder
{
    public static (Mesh body, Mesh top) Build(
        Vector2Int[] cells, float cellSize, float cellGap, float placedHeight)
    {
        float step    = cellSize + cellGap;
        var   cellSet = new HashSet<Vector2Int>(cells);

        var bv = new List<Vector3>();
        var bt = new List<int>();
        var tv = new List<Vector3>();
        var tt = new List<int>();

        foreach (var c in cells)
        {
            float cx = c.x * step;
            float cz = c.y * step;

            bool hasL = cellSet.Contains(new(c.x - 1, c.y));
            bool hasR = cellSet.Contains(new(c.x + 1, c.y));
            bool hasB = cellSet.Contains(new(c.x, c.y - 1));
            bool hasF = cellSet.Contains(new(c.x, c.y + 1));

            // Extend bounds toward same-tile neighbours to bridge the cell gap
            float x0 = cx - cellSize * 0.5f - (hasL ? cellGap * 0.5f : 0f);
            float x1 = cx + cellSize * 0.5f + (hasR ? cellGap * 0.5f : 0f);
            float z0 = cz - cellSize * 0.5f - (hasB ? cellGap * 0.5f : 0f);
            float z1 = cz + cellSize * 0.5f + (hasF ? cellGap * 0.5f : 0f);
            float h  = placedHeight;
            float g  = cellGap;

            // Bottom face (normal -Y)
            AddQuad(bv, bt,
                new(x0, 0, z0), new(x0, 0, z1), new(x1, 0, z1), new(x1, 0, z0));

            // Top cap face (normal +Y) — separate mesh
            AddQuad(tv, tt,
                new(x1, h, z0), new(x1, h, z1), new(x0, h, z1), new(x0, h, z0));

            // Left wall (normal -X)
            if (!hasL)
                AddQuad(bv, bt,
                    new(x0, 0, z0), new(x0, h, z0), new(x0, h, z1), new(x0, 0, z1));

            // Right wall (normal +X)
            if (!hasR)
                AddQuad(bv, bt,
                    new(x1, 0, z1), new(x1, h, z1), new(x1, h, z0), new(x1, 0, z0));

            // Back wall (normal -Z)
            if (!hasB)
                AddQuad(bv, bt,
                    new(x1, 0, z0), new(x1, h, z0), new(x0, h, z0), new(x0, 0, z0));

            // Front wall (normal +Z)
            if (!hasF)
                AddQuad(bv, bt,
                    new(x0, 0, z1), new(x0, h, z1), new(x1, h, z1), new(x1, 0, z1));

            // Inside corner slivers — when two perpendicular neighbours exist but their
            // shared diagonal doesn't, the gap-bridging extensions create a small notch.
            // Each case adds two thin quads (one per axis) to seal it.
            bool hasBR = cellSet.Contains(new(c.x + 1, c.y - 1));
            bool hasBL = cellSet.Contains(new(c.x - 1, c.y - 1));
            bool hasFR = cellSet.Contains(new(c.x + 1, c.y + 1));
            bool hasFL = cellSet.Contains(new(c.x - 1, c.y + 1));

            float s = g * 0.5f; // sliver width = each cell's extension toward a neighbour

            if (hasR && hasB && !hasBR)
            {
                // right-sliver (+X): fills the overhang below neighbour B
                AddQuad(bv, bt, new(x1, 0, z0 + s), new(x1, h, z0 + s), new(x1, h, z0), new(x1, 0, z0));
                // back-sliver (-Z): fills the overhang beyond neighbour C
                AddQuad(bv, bt, new(x1, 0, z0), new(x1, h, z0), new(x1 - s, h, z0), new(x1 - s, 0, z0));
            }
            if (hasR && hasF && !hasFR)
            {
                // right-sliver (+X): fills the overhang above neighbour
                AddQuad(bv, bt, new(x1, 0, z1), new(x1, h, z1), new(x1, h, z1 - s), new(x1, 0, z1 - s));
                // front-sliver (+Z): fills the overhang beyond neighbour
                AddQuad(bv, bt, new(x1 - s, 0, z1), new(x1 - s, h, z1), new(x1, h, z1), new(x1, 0, z1));
            }
            if (hasL && hasB && !hasBL)
            {
                // left-sliver (-X): fills the overhang below neighbour
                AddQuad(bv, bt, new(x0, 0, z0), new(x0, h, z0), new(x0, h, z0 + s), new(x0, 0, z0 + s));
                // back-sliver (-Z): fills the overhang beyond neighbour
                AddQuad(bv, bt, new(x0 + s, 0, z0), new(x0 + s, h, z0), new(x0, h, z0), new(x0, 0, z0));
            }
            if (hasL && hasF && !hasFL)
            {
                // left-sliver (-X): fills the overhang above neighbour
                AddQuad(bv, bt, new(x0, 0, z1 - s), new(x0, h, z1 - s), new(x0, h, z1), new(x0, 0, z1));
                // front-sliver (+Z): fills the overhang beyond neighbour
                AddQuad(bv, bt, new(x0, 0, z1), new(x0, h, z1), new(x0 + s, h, z1), new(x0 + s, 0, z1));
            }
        }

        return (MakeMesh("TileBody", bv, bt), MakeMesh("TileTop", tv, tt));
    }

    static Mesh MakeMesh(string meshName, List<Vector3> verts, List<int> tris)
    {
        var m = new Mesh { name = meshName };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void AddQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }
}
