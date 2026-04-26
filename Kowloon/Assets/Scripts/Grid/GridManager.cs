using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the NxN grid: builds base cell visuals, tracks occupied state,
/// and exposes an API for TilePlacer to query and modify.
///
/// Two cell layers:
///   Bottom (_cells)   — sits at baseY; shows preview for UNOCCUPIED cells.
///   Top    (_topCells) — sits at baseY + placedHeight; normally invisible,
///                        shows preview for OCCUPIED cells so highlights aren't
///                        hidden behind placed tile blocks.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int   gridSize = 7;
    public float cellSize = 1f;
    public float cellGap  = 0.08f;

    [Header("Heights")]
    public float emptyHeight  = 0.08f;
    public float placedHeight = 0.4f;

    [Header("Colours")]
    public Color emptyColor = new Color(0.18f, 0.18f, 0.22f, 0.55f);

    // ── derived helpers ───────────────────────────────────────────────────────

    public float Step   => cellSize + cellGap;
    public float Origin => -(gridSize - 1) * Step * 0.5f;

    // ── internal ──────────────────────────────────────────────────────────────

    private static readonly Color Invisible = new Color(0f, 0f, 0f, 0f);

    private float         _baseY;
    private bool[,]       _occupied;
    private bool[,]       _prevOccupied;
    private PlacedTile[,] _tileAt;
    private GameObject[,] _cells;
    private Material[,]   _mats;
    private GameObject[,] _topCells;
    private Material[,]   _topMats;
    private Camera        _cam;

    // ── public properties ─────────────────────────────────────────────────────

    public int   GridSize     => gridSize;
    public float CellSize     => cellSize;
    public float CellGap      => cellGap;
    public float PlacedHeight => placedHeight;
    public float BaseY        => _baseY;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _cam          = Camera.main;
        _occupied     = new bool[gridSize, gridSize];
        _prevOccupied = new bool[gridSize, gridSize];
        _tileAt       = new PlacedTile[gridSize, gridSize];
        _cells        = new GameObject[gridSize, gridSize];
        _mats         = new Material[gridSize, gridSize];
        _topCells     = new GameObject[gridSize, gridSize];
        _topMats      = new Material[gridSize, gridSize];

        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
            _prevOccupied[x, z] = true;

        BuildGrid();
    }

    void BuildGrid()
    {
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            float xw = Origin + x * Step;
            float zw = Origin + z * Step;

            // ── bottom cell ───────────────────────────────────────────────────
            var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.name = $"Cell_{x}_{z}";
            cell.transform.SetParent(transform, false);
            cell.transform.position   = new Vector3(xw, _baseY + emptyHeight * 0.5f, zw);
            cell.transform.localScale = new Vector3(cellSize, emptyHeight, cellSize);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            MakeTransparent(mat);
            mat.color = emptyColor;
            cell.GetComponent<Renderer>().material = mat;

            _cells[x, z] = cell;
            _mats[x, z]  = mat;

            // ── top cell (invisible overlay on top of placed tiles) ───────────
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = $"TopCell_{x}_{z}";
            top.transform.SetParent(transform, false);
            top.transform.position   = new Vector3(xw, _baseY + placedHeight + emptyHeight * 0.5f, zw);
            top.transform.localScale = new Vector3(cellSize, emptyHeight, cellSize);

            // Top cells don't need colliders — mouse detection uses plane raycasting
            Destroy(top.GetComponent<Collider>());

            var topMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            MakeTransparent(topMat);
            topMat.color = Invisible;
            top.GetComponent<Renderer>().material = topMat;

            _topCells[x, z] = top;
            _topMats[x, z]  = topMat;
        }
    }

    // ── material helpers ──────────────────────────────────────────────────────

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

    // ── floor transition API ──────────────────────────────────────────────────

    public void MoveTo(float newY)
    {
        _baseY = newY;
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            var p = _cells[x, z].transform.position;
            p.y = _baseY + emptyHeight * 0.5f;
            _cells[x, z].transform.position = p;

            var tp = _topCells[x, z].transform.position;
            tp.y = _baseY + placedHeight + emptyHeight * 0.5f;
            _topCells[x, z].transform.position = tp;
        }
    }

    public void ClearOccupied()
    {
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            _prevOccupied[x, z]  = _occupied[x, z];
            _occupied[x, z]      = false;
            _tileAt[x, z]        = null;
            _mats[x, z].color    = emptyColor;
            _topMats[x, z].color = Invisible;
        }
    }

    public bool WasPrevOccupied(int x, int z) =>
        IsInBounds(x, z) && _prevOccupied[x, z];

    // ── query / mutation API ──────────────────────────────────────────────────

    public bool IsInBounds(int x, int z) => x >= 0 && x < gridSize && z >= 0 && z < gridSize;
    public bool IsOccupied(int x, int z) => _occupied[x, z];

    public bool HasAnyOccupied()
    {
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
            if (_occupied[x, z]) return true;
        return false;
    }

    public bool HasAdjacentOccupied(int x, int z)
    {
        return (IsInBounds(x - 1, z) && _occupied[x - 1, z]) ||
               (IsInBounds(x + 1, z) && _occupied[x + 1, z]) ||
               (IsInBounds(x, z - 1) && _occupied[x, z - 1]) ||
               (IsInBounds(x, z + 1) && _occupied[x, z + 1]);
    }

    public void MarkOccupied(int x, int z) => _occupied[x, z] = true;

    public void       SetTileAt(int x, int z, PlacedTile t) { if (IsInBounds(x, z)) _tileAt[x, z] = t; }
    public PlacedTile GetTileAt(int x, int z)               => IsInBounds(x, z) ? _tileAt[x, z] : null;

    /// <summary>
    /// Sets the preview colour for cell (x, z).
    /// Occupied cells are highlighted on the top layer; unoccupied on the bottom.
    /// </summary>
    public void SetCellPreview(int x, int z, Color c)
    {
        if (!IsInBounds(x, z)) return;
        if (_occupied[x, z])
            _topMats[x, z].color = c;
        else
            _mats[x, z].color = c;
    }

    /// <summary>Resets both layers to their resting state.</summary>
    public void ResetCellColor(int x, int z)
    {
        if (!IsInBounds(x, z)) return;
        _mats[x, z].color    = emptyColor;
        _topMats[x, z].color = Invisible;
    }

    public bool TryGetMouseCell(out Vector2Int cell)
    {
        cell  = new(-1, -1);
        var ray   = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        var plane = new Plane(Vector3.up, new Vector3(0f, _baseY, 0f));
        if (!plane.Raycast(ray, out float dist)) return false;

        var wp = ray.GetPoint(dist);
        int x  = Mathf.RoundToInt((wp.x - Origin) / Step);
        int z  = Mathf.RoundToInt((wp.z - Origin) / Step);
        if (!IsInBounds(x, z)) return false;

        cell = new(x, z);
        return true;
    }

    public Vector3 CellToWorld(int x, int z) =>
        new Vector3(Origin + x * Step, _baseY, Origin + z * Step);
}
