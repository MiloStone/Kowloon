using UnityEngine;

/// <summary>
/// ScriptableObject describing one tile shape (e.g. T, L, I …).
/// cells[] contains offset coordinates in tile-local space.
/// (0,0) is always the anchor — the cell that stays fixed during rotation
/// and that the cursor snaps to during placement.
/// </summary>
[CreateAssetMenu(fileName = "NewTile", menuName = "Kowloon/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    [Tooltip("Short identifier shown in debug / future UI.")]
    public string displayName;

    [Tooltip("Cell offsets in tile-local space. (0,0) = anchor; always include it.")]
    public Vector2Int[] cells;

    public Color color = Color.white;
}
