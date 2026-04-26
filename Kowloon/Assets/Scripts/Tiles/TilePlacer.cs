using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the active tile: rotation (Q/E), hold (Space), stair toggle (Enter),
/// hover preview, and left-click placement. Owns per-draw door rolling and
/// resolves door connectivity at placement time.
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

    private TileInstance     _instance;
    private TileInstance     _heldInstance;
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
        bool wasStair = _instance != null && _instance.Def == stairTile;
        _instance = wasStair
            ? TileInstance.Roll(RandomTile(), false)
            : TileInstance.Roll(stairTile,    true);
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
        var incoming  = _heldInstance ?? TileInstance.Roll(RandomTile(), false);
        _heldInstance = _instance;
        _instance     = incoming;
        _rotation     = 0;
    }

    // ── preview ───────────────────────────────────────────────────────────────

    void UpdatePreview()
    {
        ClearPreview();
        if (_instance == null) return;
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

        bool wasStair = (_instance.Def == stairTile);
        ClearPreview();

        // Resolve door connectivity before spawning visuals: each of this tile's
        // doors checks its world-facing neighbour for a matching door.
        var doorOpenStates  = new bool[_instance.Doors.Length];
        var pendingNeighbour = new List<(PlacedTile tile, int doorIdx)>();

        for (int di = 0; di < _instance.Doors.Length; di++)
        {
            var door      = _instance.Doors[di];
            var cellLocal = _instance.Def.cells[door.CellIndex];
            var cellWorld = anchor + PlacedTile.RotateOffset(cellLocal, _rotation);
            var dirWorld  = door.Face.Rotate(_rotation);
            var neighbourCell = cellWorld + dirWorld.Vec();

            var neighbour = grid.GetTileAt(neighbourCell.x, neighbourCell.y);
            if (neighbour == null) continue;
            if (!neighbour.TryFindDoor(neighbourCell, dirWorld.Opposite(), out int nIdx)) continue;

            doorOpenStates[di] = true;
            pendingNeighbour.Add((neighbour, nIdx));
        }

        foreach (var c in cells) grid.MarkOccupied(c.x, c.y);
        var placed = SpawnVisual(_instance, anchor, _rotation, doorOpenStates);
        foreach (var c in cells) grid.SetTileAt(c.x, c.y, placed);

        floorManager?.RegisterPlacedTile(placed);
        foreach (var (neighbour, nIdx) in pendingNeighbour)
        {
            neighbour.OpenDoor(nIdx);
            floorManager?.Connect(placed, neighbour);
        }

        PickNextTile();

        if (wasStair) floorManager?.CompleteFloor();
    }

    // ── tile helpers ──────────────────────────────────────────────────────────

    void PickNextTile()
    {
        if (availableTiles == null || availableTiles.Length == 0) return;
        _instance = TileInstance.Roll(RandomTile(), false);
        _rotation = 0;
    }

    TileDefinition RandomTile() =>
        availableTiles[Random.Range(0, availableTiles.Length)];

    List<Vector2Int> GetWorldCells(Vector2Int anchor)
    {
        var result = new List<Vector2Int>();
        foreach (var offset in _instance.Def.cells)
            result.Add(anchor + PlacedTile.RotateOffset(offset, _rotation));
        return result;
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

    PlacedTile SpawnVisual(TileInstance inst, Vector2Int anchor, int rotation, bool[] doorOpenStates)
    {
        var bodyMesh = meshLibrary.GetBodyMesh(inst);
        var topMesh  = meshLibrary.GetTopMesh(inst.Def);

        var root = new GameObject($"Tile_{inst.Def.displayName}");
        root.transform.position = grid.CellToWorld(anchor.x, anchor.y);
        root.transform.rotation = Quaternion.Euler(0f, rotation * 90f, 0f);

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(root.transform, false);
        var bodyMf = bodyGo.AddComponent<MeshFilter>();
        var bodyMr = bodyGo.AddComponent<MeshRenderer>();
        bodyMf.sharedMesh     = bodyMesh;
        bodyMr.sharedMaterial = meshLibrary.SolidMaterial;
        var bodyMpb = new MaterialPropertyBlock();
        bodyMpb.SetColor("_BaseColor", inst.Def.color);
        bodyMr.SetPropertyBlock(bodyMpb);

        var topGo = new GameObject("TopCap");
        topGo.transform.SetParent(root.transform, false);
        var topMf = topGo.AddComponent<MeshFilter>();
        var topMr = topGo.AddComponent<MeshRenderer>();
        topMf.sharedMesh     = topMesh;
        topMr.sharedMaterial = meshLibrary.TopMaterial;
        var topMpb = new MaterialPropertyBlock();
        topMpb.SetColor("_BaseColor", new Color(inst.Def.color.r, inst.Def.color.g, inst.Def.color.b, 0.05f));
        topMr.SetPropertyBlock(topMpb);

        var placed = root.AddComponent<PlacedTile>();
        placed.bodyRenderer = bodyMr;
        placed.topRenderer  = topMr;
        placed.tileColor    = inst.Def.color;
        placed.Setup(inst, rotation, anchor);

        // Closed doors get an overlay quad covering the cutout; open doors stay
        // as a real hole in the body mesh.
        for (int di = 0; di < inst.Doors.Length; di++)
        {
            if (doorOpenStates[di])
            {
                placed.MarkDoorOpenAtSpawn(di);
            }
            else
            {
                var overlay = meshLibrary.CreateDoorOverlay(
                    root.transform, inst.Def, inst.Doors[di], inst.Def.color);
                placed.SetDoorOverlay(di, overlay);
            }
        }

        return placed;
    }
}
