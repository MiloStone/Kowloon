using System;

/// <summary>
/// One door on a tile, in tile-local (unrotated) space.
/// CellIndex indexes into TileDefinition.cells[]; Face is the cell-face direction.
/// </summary>
[Serializable]
public struct DoorSlot : IEquatable<DoorSlot>
{
    public int CellIndex;
    public Dir Face;

    public DoorSlot(int cellIndex, Dir face) { CellIndex = cellIndex; Face = face; }

    public bool Equals(DoorSlot o)        => CellIndex == o.CellIndex && Face == o.Face;
    public override bool Equals(object o) => o is DoorSlot s && Equals(s);
    public override int  GetHashCode()    => (CellIndex << 2) | (int)Face;

    public static bool operator ==(DoorSlot a, DoorSlot b) => a.Equals(b);
    public static bool operator !=(DoorSlot a, DoorSlot b) => !a.Equals(b);
}
