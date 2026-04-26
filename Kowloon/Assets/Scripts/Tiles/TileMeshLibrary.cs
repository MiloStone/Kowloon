using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns shared materials and mesh caches for placed tiles.
/// Body meshes are cached per (TileDefinition, doorMask) since door cutouts make
/// each door layout structurally distinct. Top meshes are cached per definition
/// (doors don't affect the top cap). Overlay meshes/material are shared across
/// every closed-door overlay quad for GPU instancing.
/// </summary>
public class TileMeshLibrary : MonoBehaviour
{
    [Header("References")]
    public GridManager      grid;
    public TileDefinition[] tileDefinitions;
    public TileDefinition   stairTile;

    public Material SolidMaterial   { get; private set; }
    public Material TopMaterial     { get; private set; }
    public Material OverlayMaterial { get; private set; }
    public Mesh     OverlayQuad     { get; private set; }
    public Mesh     IndicatorQuad   { get; private set; }

    private readonly Dictionary<TileDefinition, Mesh>          _topCache   = new();
    private readonly Dictionary<TileDefinition, Mesh>          _floorCache = new();
    private readonly Dictionary<(TileDefinition, ulong), Mesh> _bodyCache  = new();

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        BuildSharedMaterials();
        OverlayQuad   = TileMeshBuilder.BuildUnitQuad();
        IndicatorQuad = TileMeshBuilder.BuildUnitQuadXZ();

        // Pre-warm top meshes (cheap; door-independent).
        foreach (var def in tileDefinitions) GetTopMesh(def);
        if (stairTile != null) GetTopMesh(stairTile);
    }

    void BuildSharedMaterials()
    {
        var shader      = Shader.Find("Universal Render Pipeline/Unlit");
        var ditherShader = Shader.Find("Kowloon/DitherWall");

        SolidMaterial = new Material(ditherShader != null ? ditherShader : shader)
        {
            name             = "TileSolid",
            enableInstancing = true,
        };

        TopMaterial = new Material(shader)
        {
            name             = "TileTop",
            enableInstancing = true,
        };
        // Render both sides so a revealed top cap is solid whether viewed from
        // above (the new floor's ground) or from below (the room ceiling).
        TopMaterial.SetFloat("_Cull", 0f);

        OverlayMaterial = new Material(shader)
        {
            name             = "DoorOverlay",
            enableInstancing = true,
        };
        // Render both sides so door overlays / preview indicators don't get
        // back-face culled regardless of which way their unit-quad winding faces.
        OverlayMaterial.SetFloat("_Cull", 0f);
    }

    static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface",  1f);
        mat.SetFloat("_Blend",    0f);
        mat.SetFloat("_SrcBlend", 5f);
        mat.SetFloat("_DstBlend", 10f);
        mat.SetFloat("_ZWrite",   0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public Mesh GetTopMesh(TileDefinition def)
    {
        if (def == null) return null;
        if (_topCache.TryGetValue(def, out var m)) return m;
        m = TileMeshBuilder.BuildTop(def.cells, grid.CellSize, grid.CellGap, grid.PlacedHeight);
        _topCache[def] = m;
        return m;
    }

    public Mesh GetFloorMesh(TileDefinition def)
    {
        if (def == null) return null;
        if (_floorCache.TryGetValue(def, out var m)) return m;
        m = TileMeshBuilder.BuildFloor(def.cells, grid.CellSize, grid.CellGap, grid.PlacedHeight);
        _floorCache[def] = m;
        return m;
    }

    public Mesh GetBodyMesh(TileInstance inst)
    {
        if (inst == null) return null;
        var key = (inst.Def, DoorMask(inst.Doors));
        if (_bodyCache.TryGetValue(key, out var m)) return m;
        m = TileMeshBuilder.BuildBody(
            inst.Def.cells, grid.CellSize, grid.CellGap, grid.PlacedHeight, inst.Doors);
        _bodyCache[key] = m;
        return m;
    }

    static ulong DoorMask(DoorSlot[] doors)
    {
        ulong mask = 0;
        if (doors == null) return 0;
        foreach (var d in doors) mask |= 1UL << (d.CellIndex * 4 + (int)d.Face);
        return mask;
    }

    /// <summary>
    /// Spawn a closed-door overlay quad as a child of the tile root. Caller picks
    /// the colour (typically a darkened tile tint for closed-by-loneliness, or the
    /// flat tile tint for a permanently sealed slot).
    /// </summary>
    public GameObject CreateDoorOverlay(Transform parent, TileDefinition def, DoorSlot slot, Color overlayColor)
    {
        var   cell        = def.cells[slot.CellIndex];
        float step        = grid.CellSize + grid.CellGap;
        float wallOffset  = TileMeshBuilder.OuterWallOffset(grid.CellSize, grid.CellGap);
        float cx          = cell.x * step;
        float cz          = cell.y * step;

        var   v        = slot.Face.Vec();
        var   faceVec3 = new Vector3(v.x, 0f, v.y);
        float dh       = grid.PlacedHeight * TileMeshBuilder.DoorHeightFraction;
        float dw       = grid.CellSize     * TileMeshBuilder.DoorWidthFraction;

        // Inset the overlay slightly toward the room so the dithered near wall
        // sits visibly in front of it instead of z-fighting at the same plane.
        var pos = new Vector3(cx, dh * 0.5f, cz) + faceVec3 * (wallOffset - 0.01f);

        var go = new GameObject($"Door_{slot.CellIndex}_{slot.Face}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        // Face the same way the surrounding wall does (normal points INWARD to
        // the room) so the dither shader treats this overlay identically — back
        // face dithered from outside, front face opaque from inside.
        go.transform.localRotation = Quaternion.LookRotation(-faceVec3, Vector3.up);
        go.transform.localScale    = new Vector3(dw, dh, 1f);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh     = OverlayQuad;
        mr.sharedMaterial = SolidMaterial;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", overlayColor);
        mr.SetPropertyBlock(mpb);
        return go;
    }
}
