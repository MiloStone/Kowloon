using UnityEngine;

/// <summary>
/// Cardinal direction used for tile sides and cell-face orientation.
/// In tile-local cell-grid coords: North=+Y, East=+X, South=-Y, West=-X.
/// Maps to world: North=+Z, East=+X, South=-Z, West=-X.
/// </summary>
public enum Dir { North = 0, East = 1, South = 2, West = 3 }

public static class DirExt
{
    public static Dir Opposite(this Dir d) => (Dir)(((int)d + 2) & 3);

    /// <summary>Apply a rotation in 90° CW steps (matches TilePlacer.RotateOffset).</summary>
    public static Dir Rotate(this Dir d, int steps) => (Dir)(((int)d + steps) & 3);

    /// <summary>Cell-grid offset for this direction.</summary>
    public static Vector2Int Vec(this Dir d) => d switch
    {
        Dir.North => new Vector2Int( 0,  1),
        Dir.East  => new Vector2Int( 1,  0),
        Dir.South => new Vector2Int( 0, -1),
        Dir.West  => new Vector2Int(-1,  0),
        _         => Vector2Int.zero,
    };
}
