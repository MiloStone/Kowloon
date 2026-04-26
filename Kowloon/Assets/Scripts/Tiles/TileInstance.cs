using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Live, per-draw state of a tile: definition + rolled door layout.
/// Created when a tile enters the active slot (PickNextTile, stair toggle, first
/// hold-summon). Held tiles preserve their TileInstance — doors are not re-rolled
/// on stash/swap.
/// </summary>
public class TileInstance
{
    public readonly TileDefinition Def;
    public readonly DoorSlot[]     Doors;

    public TileInstance(TileDefinition def, DoorSlot[] doors)
    {
        Def   = def;
        Doors = doors;
    }

    /// <summary>
    /// Roll a door layout. Stair always gets 4 doors (one per side); other tiles
    /// roll a side count from {1:5%, 2:50%, 3:35%, 4:10%}, then map each chosen
    /// side to one of that side's exterior walls uniformly at random.
    /// </summary>
    public static TileInstance Roll(TileDefinition def, bool isStair)
    {
        int sideCount = isStair ? 4 : RollSideCount();
        var sides     = ShuffleSides();
        var cellSet   = new HashSet<Vector2Int>(def.cells);
        var doors     = new List<DoorSlot>(sideCount);

        for (int i = 0; i < sideCount; i++)
        {
            var side = sides[i];
            var v    = side.Vec();
            var candidates = new List<int>(def.cells.Length);
            for (int ci = 0; ci < def.cells.Length; ci++)
            {
                var c = def.cells[ci];
                if (!cellSet.Contains(new Vector2Int(c.x + v.x, c.y + v.y)))
                    candidates.Add(ci);
            }
            if (candidates.Count == 0) continue; // never happens for tetrominoes
            int pick = candidates[Random.Range(0, candidates.Count)];
            doors.Add(new DoorSlot(pick, side));
        }

        return new TileInstance(def, doors.ToArray());
    }

    static List<Dir> ShuffleSides()
    {
        var s = new List<Dir> { Dir.North, Dir.East, Dir.South, Dir.West };
        for (int i = s.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (s[i], s[j]) = (s[j], s[i]);
        }
        return s;
    }

    static int RollSideCount()
    {
        float r = Random.value;
        if (r < 0.05f) return 1;
        if (r < 0.55f) return 2;
        if (r < 0.90f) return 3;
        return 4;
    }
}
