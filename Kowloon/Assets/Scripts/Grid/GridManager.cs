using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the NxN tile grid: builds visuals, handles hover highlight,
/// validates placement, and places tiles on click.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridSize = 7;
    public float cellSize = 1f;
    public float cellGap = 0.08f;

    [Header("Cell Heights")]
    public float emptyHeight = 0.08f;
    public float placedHeight = 0.4f;

    [Header("Colours")]
    public Color emptyColor   = new Color(0.18f, 0.18f, 0.22f);
    public Color hoverValid   = new Color(0.40f, 0.80f, 0.45f);
    public Color hoverInvalid = new Color(0.80f, 0.28f, 0.28f);
    public Color placedColor  = new Color(0.65f, 0.55f, 0.42f);

    // ── internal state ────────────────────────────────────────────────────────

    private bool[,]       _occupied;
    private GameObject[,] _cells;
    private Material[,]   _mats;
    private Vector2Int    _hovered = new(-1, -1);
    private Camera        _cam;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _cam      = Camera.main;
        _occupied = new bool[gridSize, gridSize];
        _cells    = new GameObject[gridSize, gridSize];
        _mats     = new Material[gridSize, gridSize];
        BuildGrid();
    }

    void Update()
    {
        UpdateHover();
        HandlePlacement();
    }

    // ── grid construction ─────────────────────────────────────────────────────

    void BuildGrid()
    {
        float step   = cellSize + cellGap;
        float origin = -(gridSize - 1) * step * 0.5f;   // centre the grid at 0,0,0

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 pos = new Vector3(origin + x * step, emptyHeight * 0.5f, origin + z * step);

                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = $"Cell_{x}_{z}";
                cell.transform.SetParent(transform, false);
                cell.transform.position = pos;
                cell.transform.localScale = new Vector3(cellSize, emptyHeight, cellSize);

                // URP unlit material so colour shows correctly regardless of lighting
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = emptyColor;
                cell.GetComponent<Renderer>().material = mat;

                _cells[x, z] = cell;
                _mats[x, z]  = mat;
            }
        }
    }

    // ── hover ─────────────────────────────────────────────────────────────────

    void UpdateHover()
    {
        // Reset last hovered cell to its resting colour
        if (IsInGrid(_hovered))
            RefreshCellColor(_hovered.x, _hovered.y);

        _hovered = new(-1, -1);

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Vector2Int cell = FindCell(hit.collider.gameObject);
        if (!IsInGrid(cell)) return;

        _hovered = cell;
        _mats[cell.x, cell.y].color = IsValidPlacement(cell.x, cell.y) ? hoverValid : hoverInvalid;
    }

    // ── placement ─────────────────────────────────────────────────────────────

    void HandlePlacement()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (!IsInGrid(_hovered)) return;
        if (!IsValidPlacement(_hovered.x, _hovered.y)) return;

        PlaceTile(_hovered.x, _hovered.y);
    }

    void PlaceTile(int x, int z)
    {
        _occupied[x, z] = true;

        // Raise the tile to placed height (keep bottom flush with y=0)
        Transform t = _cells[x, z].transform;
        t.localScale = new Vector3(cellSize, placedHeight, cellSize);
        t.position   = new Vector3(t.position.x, placedHeight * 0.5f, t.position.z);

        _mats[x, z].color = placedColor;
    }

    // ── validity ──────────────────────────────────────────────────────────────

    bool IsValidPlacement(int x, int z)
    {
        if (_occupied[x, z]) return false;

        // First tile can go anywhere
        if (!HasAnyTile()) return true;

        // All subsequent tiles must touch an existing tile (4-directional)
        return HasAdjacentTile(x, z);
    }

    bool HasAnyTile()
    {
        for (int x = 0; x < gridSize; x++)
            for (int z = 0; z < gridSize; z++)
                if (_occupied[x, z]) return true;
        return false;
    }

    bool HasAdjacentTile(int x, int z)
    {
        return (x > 0            && _occupied[x - 1, z]) ||
               (x < gridSize - 1 && _occupied[x + 1, z]) ||
               (z > 0            && _occupied[x, z - 1]) ||
               (z < gridSize - 1 && _occupied[x, z + 1]);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    void RefreshCellColor(int x, int z)
    {
        _mats[x, z].color = _occupied[x, z] ? placedColor : emptyColor;
    }

    Vector2Int FindCell(GameObject obj)
    {
        for (int x = 0; x < gridSize; x++)
            for (int z = 0; z < gridSize; z++)
                if (_cells[x, z] == obj) return new(x, z);
        return new(-1, -1);
    }

    bool IsInGrid(Vector2Int c) => c.x >= 0 && c.y >= 0;
}
