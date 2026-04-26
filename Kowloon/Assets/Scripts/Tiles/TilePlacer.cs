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

    [Tooltip("Inset bar drawn on the cell edge facing each rolled door.")]
    public Color doorIndicatorColor = new Color(1f, 0.85f, 0.20f);
    [Tooltip("Door bar thickness as a fraction of cellSize (perpendicular to the edge).")]
    [Range(0.02f, 0.5f)]
    public float doorIndicatorInsetFraction = 0.125f;

    [Header("Animation")]
    [Tooltip("Seconds for a placed tile to drop into its final position.")]
    public float dropDuration = 0.25f;
    [Tooltip("How high above the target a tile starts, in cell heights.")]
    public float dropHeightCells = 0.6f;

    [Header("Audio")]
    public AudioClip placeChime;
    [Tooltip("Volume for the place chime.")]
    [Range(0f, 1f)] public float placeChimeVolume = 1f;
    [Tooltip("Major-scale degrees the chime randomly hits (1-indexed).")]
    public int[] pentatonicDegrees = { 1, 2, 3, 5, 6 };
    private AudioSource _audio;
    private int         _lastDegree = -1;

    // ── runtime state ─────────────────────────────────────────────────────────

    private TileInstance     _instance;
    private TileInstance     _heldInstance;
    private int              _rotation;
    private List<Vector2Int> _preview = new();
    private bool             _previewLive;
    private List<GameObject> _doorPreviewIndicators = new();

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (grid         == null) grid         = FindFirstObjectByType<GridManager>();
        if (floorManager == null) floorManager = FindFirstObjectByType<FloorManager>();
        if (meshLibrary  == null) meshLibrary  = FindFirstObjectByType<TileMeshLibrary>();

        _audio                  = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake      = false;
        _audio.spatialBlend     = 0f;
    }

    void PlayPlaceChime()
    {
        if (placeChime == null || _audio == null) return;
        // Major-scale degree → semitone offset from root.
        // 1=0, 2=2, 3=4, 4=5, 5=7, 6=9, 7=11.
        int[] semis = { 0, 2, 4, 5, 7, 9, 11 };
        int   deg;
        if (pentatonicDegrees.Length <= 1)
            deg = pentatonicDegrees[0];
        else
            do { deg = pentatonicDegrees[Random.Range(0, pentatonicDegrees.Length)]; }
            while (deg == _lastDegree);
        _lastDegree = deg;
        int   s     = (deg >= 1 && deg <= 7) ? semis[deg - 1] : 0;
        _audio.pitch  = Mathf.Pow(2f, s / 12f);
        _audio.PlayOneShot(placeChime, placeChimeVolume);
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
        if (_instance != null && _instance.Def == stairTile) return;
        if (floorManager == null || !floorManager.ConstraintsSatisfied) return;

        ClearPreview();
        _instance = TileInstance.Roll(stairTile, true);
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

        // Yellow door indicators: thin bar on the world-edge each door faces.
        for (int di = 0; di < _instance.Doors.Length; di++)
        {
            var door      = _instance.Doors[di];
            var cellLocal = _instance.Def.cells[door.CellIndex];
            var cellWorld = anchor + PlacedTile.RotateOffset(cellLocal, _rotation);
            if (!grid.IsInBounds(cellWorld.x, cellWorld.y)) continue;
            SpawnDoorPreviewIndicator(cellWorld, door.Face.Rotate(_rotation));
        }
    }

    void ClearPreview()
    {
        if (!_previewLive) return;
        foreach (var c in _preview)
            grid.ResetCellColor(c.x, c.y);
        _preview.Clear();
        _previewLive = false;

        for (int i = 0; i < _doorPreviewIndicators.Count; i++)
            if (_doorPreviewIndicators[i] != null) Destroy(_doorPreviewIndicators[i]);
        _doorPreviewIndicators.Clear();
    }

    void SpawnDoorPreviewIndicator(Vector2Int cell, Dir worldDir)
    {
        var   v        = worldDir.Vec();
        float halfCell = grid.CellSize * 0.5f;
        float inset    = grid.CellSize * doorIndicatorInsetFraction;

        var cellCenter = grid.CellToWorld(cell.x, cell.y);
        var pos = new Vector3(
            cellCenter.x + v.x * (halfCell - inset * 0.5f),
            grid.BaseY + grid.emptyHeight + 0.005f,
            cellCenter.z + v.y * (halfCell - inset * 0.5f));

        int absX = Mathf.Abs(v.x), absZ = Mathf.Abs(v.y);
        var scale = new Vector3(
            absX * inset + (1 - absX) * grid.CellSize,
            1f,
            absZ * inset + (1 - absZ) * grid.CellSize);

        var go = new GameObject($"DoorPreview_{cell.x}_{cell.y}_{worldDir}");
        go.transform.position   = pos;
        go.transform.localScale = scale;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh     = meshLibrary.IndicatorQuad;
        mr.sharedMaterial = meshLibrary.OverlayMaterial;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", doorIndicatorColor);
        mr.SetPropertyBlock(mpb);

        _doorPreviewIndicators.Add(go);
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
        // doors checks its world-facing neighbour for a matching door. Doors with
        // a neighbour but no matching door are "born blocked" (no overlay).
        var doorOpenStates    = new bool[_instance.Doors.Length];
        var doorBornBlocked   = new bool[_instance.Doors.Length];
        var pendingNeighbour  = new List<(PlacedTile tile, int doorIdx)>();

        for (int di = 0; di < _instance.Doors.Length; di++)
        {
            var door      = _instance.Doors[di];
            var cellLocal = _instance.Def.cells[door.CellIndex];
            var cellWorld = anchor + PlacedTile.RotateOffset(cellLocal, _rotation);
            var dirWorld  = door.Face.Rotate(_rotation);
            var neighbourCell = cellWorld + dirWorld.Vec();

            var neighbour = grid.GetTileAt(neighbourCell.x, neighbourCell.y);
            if (neighbour == null) continue;

            if (neighbour.TryFindDoor(neighbourCell, dirWorld.Opposite(), out int nIdx))
            {
                doorOpenStates[di] = true;
                pendingNeighbour.Add((neighbour, nIdx));
            }
            else
            {
                doorBornBlocked[di] = true;
            }
        }

        // Wall-block pass: for each of this tile's exterior wall faces that ISN'T
        // one of our doors, if the neighbour cell holds a tile with a closed door
        // staring at our wall, that door is now permanently closed — hide it.
        var bDoorSet     = new HashSet<(int, Dir)>();
        foreach (var d in _instance.Doors) bDoorSet.Add((d.CellIndex, d.Face));
        var localCellSet = new HashSet<Vector2Int>(_instance.Def.cells);
        for (int ci = 0; ci < _instance.Def.cells.Length; ci++)
        {
            var cellLocal = _instance.Def.cells[ci];
            var cellWorld = anchor + PlacedTile.RotateOffset(cellLocal, _rotation);
            for (int di = 0; di < 4; di++)
            {
                var d  = (Dir)di;
                var dv = d.Vec();
                if (localCellSet.Contains(new Vector2Int(cellLocal.x + dv.x, cellLocal.y + dv.y))) continue;
                if (bDoorSet.Contains((ci, d))) continue;

                var dirWorld      = d.Rotate(_rotation);
                var neighbourCell = cellWorld + dirWorld.Vec();
                var neighbour     = grid.GetTileAt(neighbourCell.x, neighbourCell.y);
                if (neighbour == null) continue;
                if (!neighbour.TryFindDoor(neighbourCell, dirWorld.Opposite(), out int nIdx)) continue;
                neighbour.SealDoor(nIdx);
            }
        }

        foreach (var c in cells) grid.MarkOccupied(c.x, c.y);
        var placed = SpawnVisual(_instance, anchor, _rotation, doorOpenStates, doorBornBlocked);
        foreach (var c in cells) grid.SetTileAt(c.x, c.y, placed);

        floorManager?.RegisterPlacedTile(placed);
        foreach (var (neighbour, nIdx) in pendingNeighbour)
        {
            neighbour.OpenDoor(nIdx);
            floorManager?.Connect(placed, neighbour);
        }

        floorManager?.RaiseContractChanged();
        PlayPlaceChime();
        placed.StartCoroutine(placed.AnimateDropIn(
            dropDuration, dropHeightCells * grid.PlacedHeight));
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
        bool isFirst       = !grid.HasAnyOccupied();
        bool hasAdj        = false;
        bool hasStackBelow = false;

        foreach (var c in cells)
        {
            if (!grid.IsInBounds(c.x, c.y))                       return false;
            if (grid.IsOccupied(c.x, c.y))                         return false;
            if (!isFirst && grid.HasAdjacentOccupied(c.x, c.y))   hasAdj        = true;
            if (grid.WasPrevOccupied(c.x, c.y))       hasStackBelow = true;
        }

        return hasStackBelow && (isFirst || hasAdj);
    }

    // ── visual spawning ───────────────────────────────────────────────────────

    PlacedTile SpawnVisual(TileInstance inst, Vector2Int anchor, int rotation, bool[] doorOpenStates, bool[] doorBornBlocked)
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

        var floorGo = new GameObject("Floor");
        floorGo.transform.SetParent(root.transform, false);
        // Lift above the empty-cell mesh (y = 0..emptyHeight) so the transparent
        // grid cell doesn't tint the floor color toward the cell's dark gray.
        floorGo.transform.localPosition = new Vector3(0f, grid.emptyHeight + 0.005f, 0f);
        var floorMf = floorGo.AddComponent<MeshFilter>();
        var floorMr = floorGo.AddComponent<MeshRenderer>();
        floorMf.sharedMesh     = meshLibrary.GetFloorMesh(inst.Def);
        floorMr.sharedMaterial = meshLibrary.SolidMaterial;
        var floorMpb = new MaterialPropertyBlock();
        floorMpb.SetColor("_BaseColor", Color.Lerp(inst.Def.color, Color.black, 0.20f));
        floorMr.SetPropertyBlock(floorMpb);

        var topGo = new GameObject("TopCap");
        topGo.transform.SetParent(root.transform, false);
        var topMf = topGo.AddComponent<MeshFilter>();
        var topMr = topGo.AddComponent<MeshRenderer>();
        topMf.sharedMesh     = topMesh;
        topMr.sharedMaterial = meshLibrary.TopMaterial;
        var topMpb = new MaterialPropertyBlock();
        topMpb.SetColor("_BaseColor", inst.Def.color);
        topMr.SetPropertyBlock(topMpb);
        // Hidden until the floor above completes (RevealTop activates it).
        topGo.SetActive(false);

        var placed = root.AddComponent<PlacedTile>();
        placed.bodyRenderer = bodyMr;
        placed.topRenderer  = topMr;
        placed.tileColor    = inst.Def.color;
        placed.Setup(inst, rotation, anchor);

        // Open doors leave a real hole in the body mesh. Closed doors get an
        // overlay covering the cutout — darker tile colour if the door could
        // still open later (no wall blocks it yet), or the flat tile colour
        // when the door is born permanently sealed.
        for (int di = 0; di < inst.Doors.Length; di++)
        {
            if (doorOpenStates[di])
            {
                placed.MarkDoorOpenAtSpawn(di);
                continue;
            }
            Color overlayColor = doorBornBlocked[di]
                ? inst.Def.color
                : Color.Lerp(inst.Def.color, Color.black, 0.20f);
            var overlay = meshLibrary.CreateDoorOverlay(
                root.transform, inst.Def, inst.Doors[di], overlayColor);
            placed.SetDoorOverlay(di, overlay);
        }

        return placed;
    }
}
