using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the NxN grid: builds base cell visuals, tracks occupied state,
/// and exposes an API for TilePlacer to query and modify.
/// Does NOT handle tile selection or placement logic — that lives in TilePlacer.
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
    public Color emptyColor = new Color(0.18f, 0.18f, 0.22f);

    // ── derived helpers ───────────────────────────────────────────────────────

    public float Step   => cellSize + cellGap;
    public float Origin => -(gridSize - 1) * Step * 0.5f;

    // ── internal ──────────────────────────────────────────────────────────────

    private float         _baseY;
    private bool[,]       _occupied;
    private PlacedTile[,] _tileAt;
    private GameObject[,] _cells;
    private Material[,]   _mats;
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
        _cam      = Camera.main;
        _occupied = new bool[gridSize, gridSize];
        _tileAt   = new PlacedTile[gridSize, gridSize];
        _cells    = new GameObject[gridSize, gridSize];
        _mats     = new Material[gridSize, gridSize];
        BuildGrid();
    }

    void BuildGrid()
    {
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            var pos  = new Vector3(Origin + x * Step, _baseY + emptyHeight * 0.5f, Origin + z * Step);
            var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.name = $"Cell_{x}_{z}";
            cell.transform.SetParent(transform, false);
            cell.transform.position   = pos;
            cell.transform.localScale = new Vector3(cellSize, emptyHeight, cellSize);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = emptyColor;
            cell.GetComponent<Renderer>().material = mat;

            _cells[x, z] = cell;
            _mats[x, z]  = mat;
        }
    }

    // ── floor transition API ──────────────────────────────────────────────────

    /// <summary>Repositions the empty grid cells to sit on top of the given Y level.</summary>
    public void MoveTo(float newY)
    {
        _baseY = newY;
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            var pos = _cells[x, z].transform.position;
            pos.y   = _baseY + emptyHeight * 0.5f;
            _cells[x, z].transform.position = pos;
        }
    }

    /// <summary>Clears occupied state and resets all cell colours for a fresh floor.</summary>
    public void ClearOccupied()
    {
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            _occupied[x, z]    = false;
            _tileAt[x, z]      = null;
            _mats[x, z].color  = emptyColor;
        }
    }

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

    public void SetCellPreview(int x, int z, Color c)  { if (IsInBounds(x, z)) _mats[x, z].color = c; }
    public void ResetCellColor(int x, int z)           { if (IsInBounds(x, z)) _mats[x, z].color = emptyColor; }

    /// <summary>
    /// Returns the grid cell under the mouse by raycasting against the current
    /// floor plane. Works correctly even when tall placed-tile cubes are in the way.
    /// </summary>
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

    /// <summary>Returns the world-space position of cell (x, z) at the current floor's base Y.</summary>
    public Vector3 CellToWorld(int x, int z) =>
        new Vector3(Origin + x * Step, _baseY, Origin + z * Step);
}
