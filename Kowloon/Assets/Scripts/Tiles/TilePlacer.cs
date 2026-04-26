using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the active tile: rotation (Q/E), hold (Space), stair toggle (Enter),
/// hover preview, and left-click placement.
/// </summary>
public class TilePlacer : MonoBehaviour
{
    [Header("References")]
    public GridManager      grid;
    public FloorManager     floorManager;
    public TileMeshLibrary  meshLibrary;
    public TileDefinition[] availableTiles;

    [Header("Special Tiles")]
    [Tooltip("The stair tile. Not in availableTiles — toggled with Enter.")]
    public TileDefinition stairTile;

    [Header("Preview Colours")]
    public Color validColor   = new Color(0.40f, 0.80f, 0.45f);
    public Color invalidColor = new Color(0.80f, 0.28f, 0.28f);

    [Tooltip("0 = anchor same colour as rest; 1 = anchor pure white.")]
    [Range(0f, 1f)]
    public float anchorWhiteLerp = 0.35f;

    // ── runtime state ─────────────────────────────────────────────────────────

    private TileDefinition   _tile;
    private TileDefinition   _heldTile;
    private int              _rotation;
    private List<Vector2Int> _preview = new();
    private bool             _previewLive;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (grid         == null) grid         = FindFirstObjectByType<GridManager>();
        if (floorManager == null) floorManager = FindFirstObjectByType<FloorManager>();
        if (meshLibrary  == null) meshLibrary  = FindFirstObjectByType<TileMeshLibrary>();
    }

    void Start() => PickNextTile();

    void Update()
    {
        HandleStairToggle();
        HandleRotation();
        HandleHold();
        UpdatePreview();
        HandleClick();
    }

    // ── stair toggle ──────────────────────────────────────────────────────────

    void HandleStairToggle()
    {
        if (!Keyboard.current.enterKey.wasPressedThisFrame) return;
        ClearPreview();
        _tile     = (_tile == stairTile) ? RandomTile() : stairTile;
        _rotation = 0;
    }

    // ── rotation ──────────────────────────────────────────────────────────────

    void HandleRotation()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame) { ClearPreview(); _rotation = (_rotation + 3) % 4; }
        if (Keyboard.current.eKey.wasPressedThisFrame) { ClearPreview(); _rotation = (_rotation + 1) % 4; }
    }

    // ── hold ──────────────────────────────────────────────────────────────────

    void HandleHold()
    {
        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;
        ClearPreview();
        var incoming = _heldTile ?? RandomTile();
        _heldTile = _tile;
        _tile     = incoming;
        _rotation = 0;
    }

    // ── preview ───────────────────────────────────────────────────────────────

    void UpdatePreview()
    {
        ClearPreview();
        if (_tile == null) return;
        if (!grid.TryGetMouseCell(out Vector2Int anchor)) return;

        _preview     = GetWorldCells(anchor);
        _previewLive = true;
        bool  valid       = IsValidPlacement(_preview);
        Color baseColor   = valid ? validColor : invalidColor;
        Color anchorColor = Color.Lerp(baseColor, Color.white, anchorWhiteLerp);

        foreach (var c in _preview)
        {
            if (!grid.IsInBounds(c.x, c.y)) continue;
            grid.SetCellPreview(c.x, c.y, c == anchor ? anchorColor : baseColor);
        }
    }

    void ClearPreview()
    {
        if (!_previewLive) return;
        foreach (var c in _preview)
            grid.ResetCellColor(c.x, c.y);
        _preview.Clear();
        _previewLive = false;
    }

    // ── placement ─────────────────────────────────────────────────────────────

    void HandleClick()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (!grid.TryGetMouseCell(out Vector2Int anchor)) return;

        var cells = GetWorldCells(anchor);
        if (!IsValidPlacement(cells)) return;

        bool wasStair = (_tile == stairTile);
        ClearPreview();

        foreach (var c in cells) grid.MarkOccupied(c.x, c.y);
        SpawnVisual(_tile, anchor);
        PickNextTile();

        if (wasStair) floorManager?.CompleteFloor();
    }

    // ── tile helpers ──────────────────────────────────────────────────────────

    void PickNextTile()
    {
        if (availableTiles == null || availableTiles.Length == 0) return;
        _tile     = RandomTile();
        _rotation = 0;
    }

    TileDefinition RandomTile() =>
        availableTiles[Random.Range(0, availableTiles.Length)];

    List<Vector2Int> GetWorldCells(Vector2Int anchor)
    {
        var result = new List<Vector2Int>();
        foreach (var offset in _tile.cells)
            result.Add(anchor + RotateOffset(offset, _rotation));
        return result;
    }

    static Vector2Int RotateOffset(Vector2Int v, int steps)
    {
        for (int i = 0; i < steps; i++)
            v = new Vector2Int(v.y, -v.x);
        return v;
    }

    bool IsValidPlacement(List<Vector2Int> cells)
    {
        bool isFirst = !grid.HasAnyOccupied();
        bool hasAdj  = false;

        foreach (var c in cells)
        {
            if (!grid.IsInBounds(c.x, c.y))                      return false;
            if (grid.IsOccupied(c.x, c.y))                        return false;
            if (!isFirst && grid.HasAdjacentOccupied(c.x, c.y))  hasAdj = true;
        }

        return isFirst || hasAdj;
    }

    // ── visual spawning ───────────────────────────────────────────────────────

    void SpawnVisual(TileDefinition tile, Vector2Int anchor)
    {
        if (!meshLibrary.TryGetMeshes(tile, out Mesh bodyMesh, out Mesh topMesh))
        {
            Debug.LogWarning($"TileMeshLibrary: no cached mesh for {tile.displayName}");
            return;
        }

        var root = new GameObject($"Tile_{tile.displayName}");
        root.transform.position = grid.CellToWorld(anchor.x, anchor.y);
        root.transform.rotation = Quaternion.Euler(0f, _rotation * 90f, 0f);

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(root.transform, false);
        var bodyMf = bodyGo.AddComponent<MeshFilter>();
        var bodyMr = bodyGo.AddComponent<MeshRenderer>();
        bodyMf.sharedMesh      = bodyMesh;
        bodyMr.sharedMaterial  = meshLibrary.SolidMaterial;
        var bodyMpb = new MaterialPropertyBlock();
        bodyMpb.SetColor("_BaseColor", tile.color);
        bodyMr.SetPropertyBlock(bodyMpb);

        var topGo = new GameObject("TopCap");
        topGo.transform.SetParent(root.transform, false);
        var topMf = topGo.AddComponent<MeshFilter>();
        var topMr = topGo.AddComponent<MeshRenderer>();
        topMf.sharedMesh     = topMesh;
        topMr.sharedMaterial = meshLibrary.TopMaterial;
        var topMpb = new MaterialPropertyBlock();
        topMpb.SetColor("_BaseColor", new Color(tile.color.r, tile.color.g, tile.color.b, 0.05f));
        topMr.SetPropertyBlock(topMpb);

        var placed = root.AddComponent<PlacedTile>();
        placed.bodyRenderer = bodyMr;
        placed.topRenderer  = topMr;
        placed.tileColor    = tile.color;

        floorManager?.RegisterPlacedTile(placed);
    }
}
