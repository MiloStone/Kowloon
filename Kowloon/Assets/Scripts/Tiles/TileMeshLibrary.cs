using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pre-builds and caches one (body, top) mesh pair per TileDefinition.
/// Owns the two shared materials so TilePlacer can set per-instance colour
/// via MaterialPropertyBlock without allocating a material per tile.
/// </summary>
public class TileMeshLibrary : MonoBehaviour
{
    [Header("References")]
    public GridManager      grid;
    public TileDefinition[] tileDefinitions;
    public TileDefinition   stairTile;

    public Material SolidMaterial { get; private set; }
    public Material TopMaterial   { get; private set; }

    private readonly Dictionary<TileDefinition, (Mesh body, Mesh top)> _cache = new();

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        BuildSharedMaterials();
        foreach (var def in tileDefinitions)
            Cache(def);
        if (stairTile != null)
            Cache(stairTile);
    }

    void BuildSharedMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");

        SolidMaterial = new Material(shader)
        {
            name             = "TileSolid",
            enableInstancing = true,
        };

        TopMaterial = new Material(shader)
        {
            name             = "TileTop",
            enableInstancing = true,
        };
        MakeTransparent(TopMaterial);
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

    void Cache(TileDefinition def)
    {
        if (def == null || _cache.ContainsKey(def)) return;
        _cache[def] = TileMeshBuilder.Build(def.cells, grid.CellSize, grid.CellGap, grid.PlacedHeight);
    }

    public bool TryGetMeshes(TileDefinition def, out Mesh body, out Mesh top)
    {
        if (_cache.TryGetValue(def, out var pair))
        {
            body = pair.body;
            top  = pair.top;
            return true;
        }
        body = top = null;
        return false;
    }
}
