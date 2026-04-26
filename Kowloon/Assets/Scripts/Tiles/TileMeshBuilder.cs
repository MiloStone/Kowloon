using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds body and top-cap meshes for a tetromino in tile-local space.
/// Anchor cell (0,0) sits at local origin; other cells offset by multiples of step.
/// Body mesh: outer walls + bottom face, with door-shaped cutouts where DoorSlots
///   are present (jambs + lintel). Adjacent cells share exact edges — no seam.
/// Top mesh: top faces only (revealed via PlacedTile.RevealTop when the floor
///   above completes); door-independent.
/// </summary>
public static class TileMeshBuilder
{
    // Door window (relative to cell): width along the wall, height from the floor.
    public const float DoorWidthFraction  = 0.45f;
    public const float DoorHeightFraction = 0.75f;

    public static Mesh BuildBody(
        Vector2Int[] cells, float cellSize, float cellGap, float placedHeight,
        IEnumerable<DoorSlot> doors)
    {
        var doorSet = new HashSet<DoorSlot>();
        if (doors != null) foreach (var d in doors) doorSet.Add(d);

        float step    = cellSize + cellGap;
        var   cellSet = new HashSet<Vector2Int>(cells);

        var bv = new List<Vector3>();
        var bt = new List<int>();

        float dw = cellSize     * DoorWidthFraction;
        float dh = placedHeight * DoorHeightFraction;

        for (int ci = 0; ci < cells.Length; ci++)
        {
            var   c  = cells[ci];
            float cx = c.x * step;
            float cz = c.y * step;

            bool hasL = cellSet.Contains(new(c.x - 1, c.y));
            bool hasR = cellSet.Contains(new(c.x + 1, c.y));
            bool hasB = cellSet.Contains(new(c.x, c.y - 1));
            bool hasF = cellSet.Contains(new(c.x, c.y + 1));

            float x0 = cx - cellSize * 0.5f - (hasL ? cellGap * 0.5f : 0f);
            float x1 = cx + cellSize * 0.5f + (hasR ? cellGap * 0.5f : 0f);
            float z0 = cz - cellSize * 0.5f - (hasB ? cellGap * 0.5f : 0f);
            float z1 = cz + cellSize * 0.5f + (hasF ? cellGap * 0.5f : 0f);
            float h  = placedHeight;
            float g  = cellGap;

            float dz0 = cz - dw * 0.5f, dz1 = cz + dw * 0.5f;
            float dx0 = cx - dw * 0.5f, dx1 = cx + dw * 0.5f;

            // Bottom face (-Y)
            AddQuad(bv, bt,
                new(x0, 0, z0), new(x0, 0, z1), new(x1, 0, z1), new(x1, 0, z0));

            // West wall (-X)
            if (!hasL)
            {
                if (doorSet.Contains(new DoorSlot(ci, Dir.West)))
                {
                    AddQuad(bv, bt, new(x0, 0, z0),  new(x0, h, z0),  new(x0, h, dz0), new(x0, 0, dz0));
                    AddQuad(bv, bt, new(x0, 0, dz1), new(x0, h, dz1), new(x0, h, z1),  new(x0, 0, z1));
                    AddQuad(bv, bt, new(x0, dh, dz0), new(x0, h, dz0), new(x0, h, dz1), new(x0, dh, dz1));
                }
                else
                {
                    AddQuad(bv, bt, new(x0, 0, z0), new(x0, h, z0), new(x0, h, z1), new(x0, 0, z1));
                }
            }

            // East wall (+X)
            if (!hasR)
            {
                if (doorSet.Contains(new DoorSlot(ci, Dir.East)))
                {
                    AddQuad(bv, bt, new(x1, 0, z1),  new(x1, h, z1),  new(x1, h, dz1), new(x1, 0, dz1));
                    AddQuad(bv, bt, new(x1, 0, dz0), new(x1, h, dz0), new(x1, h, z0),  new(x1, 0, z0));
                    AddQuad(bv, bt, new(x1, dh, dz1), new(x1, h, dz1), new(x1, h, dz0), new(x1, dh, dz0));
                }
                else
                {
                    AddQuad(bv, bt, new(x1, 0, z1), new(x1, h, z1), new(x1, h, z0), new(x1, 0, z0));
                }
            }

            // South wall (-Z)
            if (!hasB)
            {
                if (doorSet.Contains(new DoorSlot(ci, Dir.South)))
                {
                    AddQuad(bv, bt, new(x1, 0, z0),  new(x1, h, z0),  new(dx1, h, z0), new(dx1, 0, z0));
                    AddQuad(bv, bt, new(dx0, 0, z0), new(dx0, h, z0), new(x0, h, z0),  new(x0, 0, z0));
                    AddQuad(bv, bt, new(dx1, dh, z0), new(dx1, h, z0), new(dx0, h, z0), new(dx0, dh, z0));
                }
                else
                {
                    AddQuad(bv, bt, new(x1, 0, z0), new(x1, h, z0), new(x0, h, z0), new(x0, 0, z0));
                }
            }

            // North wall (+Z)
            if (!hasF)
            {
                if (doorSet.Contains(new DoorSlot(ci, Dir.North)))
                {
                    AddQuad(bv, bt, new(x0, 0, z1),  new(x0, h, z1),  new(dx0, h, z1), new(dx0, 0, z1));
                    AddQuad(bv, bt, new(dx1, 0, z1), new(dx1, h, z1), new(x1, h, z1),  new(x1, 0, z1));
                    AddQuad(bv, bt, new(dx0, dh, z1), new(dx0, h, z1), new(dx1, h, z1), new(dx1, dh, z1));
                }
                else
                {
                    AddQuad(bv, bt, new(x0, 0, z1), new(x0, h, z1), new(x1, h, z1), new(x1, 0, z1));
                }
            }

            // Inside corner slivers — when two perpendicular neighbours exist but
            // their shared diagonal doesn't, gap-bridging extensions create a notch.
            bool hasBR = cellSet.Contains(new(c.x + 1, c.y - 1));
            bool hasBL = cellSet.Contains(new(c.x - 1, c.y - 1));
            bool hasFR = cellSet.Contains(new(c.x + 1, c.y + 1));
            bool hasFL = cellSet.Contains(new(c.x - 1, c.y + 1));

            float s = g * 0.5f;
            if (hasR && hasB && !hasBR)
            {
                AddQuad(bv, bt, new(x1, 0, z0 + s), new(x1, h, z0 + s), new(x1, h, z0), new(x1, 0, z0));
                AddQuad(bv, bt, new(x1, 0, z0), new(x1, h, z0), new(x1 - s, h, z0), new(x1 - s, 0, z0));
            }
            if (hasR && hasF && !hasFR)
            {
                AddQuad(bv, bt, new(x1, 0, z1), new(x1, h, z1), new(x1, h, z1 - s), new(x1, 0, z1 - s));
                AddQuad(bv, bt, new(x1 - s, 0, z1), new(x1 - s, h, z1), new(x1, h, z1), new(x1, 0, z1));
            }
            if (hasL && hasB && !hasBL)
            {
                AddQuad(bv, bt, new(x0, 0, z0), new(x0, h, z0), new(x0, h, z0 + s), new(x0, 0, z0 + s));
                AddQuad(bv, bt, new(x0 + s, 0, z0), new(x0 + s, h, z0), new(x0, h, z0), new(x0, 0, z0));
            }
            if (hasL && hasF && !hasFL)
            {
                AddQuad(bv, bt, new(x0, 0, z1 - s), new(x0, h, z1 - s), new(x0, h, z1), new(x0, 0, z1));
                AddQuad(bv, bt, new(x0, 0, z1), new(x0, h, z1), new(x0 + s, h, z1), new(x0 + s, 0, z1));
            }
        }

        return MakeMesh("TileBody", bv, bt);
    }

    /// <summary>Inward-facing floor: same footprint as the top cap but at y=0 with +Y normal.</summary>
    public static Mesh BuildFloor(
        Vector2Int[] cells, float cellSize, float cellGap, float placedHeight)
    {
        float step    = cellSize + cellGap;
        var   cellSet = new HashSet<Vector2Int>(cells);
        var   fv      = new List<Vector3>();
        var   ft      = new List<int>();

        foreach (var c in cells)
        {
            float cx = c.x * step;
            float cz = c.y * step;
            bool hasL = cellSet.Contains(new(c.x - 1, c.y));
            bool hasR = cellSet.Contains(new(c.x + 1, c.y));
            bool hasB = cellSet.Contains(new(c.x, c.y - 1));
            bool hasF = cellSet.Contains(new(c.x, c.y + 1));

            float x0 = cx - cellSize * 0.5f - (hasL ? cellGap * 0.5f : 0f);
            float x1 = cx + cellSize * 0.5f + (hasR ? cellGap * 0.5f : 0f);
            float z0 = cz - cellSize * 0.5f - (hasB ? cellGap * 0.5f : 0f);
            float z1 = cz + cellSize * 0.5f + (hasF ? cellGap * 0.5f : 0f);

            // +Y winding (same vertex order as top cap, just at y=0).
            AddQuad(fv, ft,
                new(x1, 0, z0), new(x1, 0, z1), new(x0, 0, z1), new(x0, 0, z0));
        }

        return MakeMesh("TileFloor", fv, ft);
    }

    public static Mesh BuildTop(
        Vector2Int[] cells, float cellSize, float cellGap, float placedHeight)
    {
        float step    = cellSize + cellGap;
        var   cellSet = new HashSet<Vector2Int>(cells);
        var   tv      = new List<Vector3>();
        var   tt      = new List<int>();

        foreach (var c in cells)
        {
            float cx = c.x * step;
            float cz = c.y * step;
            bool hasL = cellSet.Contains(new(c.x - 1, c.y));
            bool hasR = cellSet.Contains(new(c.x + 1, c.y));
            bool hasB = cellSet.Contains(new(c.x, c.y - 1));
            bool hasF = cellSet.Contains(new(c.x, c.y + 1));

            float x0 = cx - cellSize * 0.5f - (hasL ? cellGap * 0.5f : 0f);
            float x1 = cx + cellSize * 0.5f + (hasR ? cellGap * 0.5f : 0f);
            float z0 = cz - cellSize * 0.5f - (hasB ? cellGap * 0.5f : 0f);
            float z1 = cz + cellSize * 0.5f + (hasF ? cellGap * 0.5f : 0f);
            float h  = placedHeight;

            AddQuad(tv, tt,
                new(x1, h, z0), new(x1, h, z1), new(x0, h, z1), new(x0, h, z0));
        }

        return MakeMesh("TileTop", tv, tt);
    }

    /// <summary>Unit quad in the XY plane with +Z normal. Used for door overlays.</summary>
    public static Mesh BuildUnitQuad()
    {
        var v = new List<Vector3> {
            new(-0.5f, -0.5f, 0f),
            new(-0.5f,  0.5f, 0f),
            new( 0.5f,  0.5f, 0f),
            new( 0.5f, -0.5f, 0f),
        };
        var t = new List<int> { 0, 1, 2, 0, 2, 3 };
        return MakeMesh("DoorOverlayQuad", v, t);
    }

    /// <summary>Unit quad in the XZ plane with +Y normal. Used for door preview indicators.</summary>
    public static Mesh BuildUnitQuadXZ()
    {
        var v = new List<Vector3> {
            new( 0.5f, 0f, -0.5f),
            new( 0.5f, 0f,  0.5f),
            new(-0.5f, 0f,  0.5f),
            new(-0.5f, 0f, -0.5f),
        };
        var t = new List<int> { 0, 1, 2, 0, 2, 3 };
        return MakeMesh("DoorIndicatorQuad", v, t);
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
