using UnityEngine;

/// <summary>
/// Component on every placed tile root. Tracks renderers (for top reveal) and
/// per-door runtime state (world cell + direction + open/closed + overlay quad).
/// </summary>
public class PlacedTile : MonoBehaviour
{
    public Renderer bodyRenderer;
    public Renderer topRenderer;
    public Color    tileColor;

    public TileInstance Instance { get; private set; }
    public int          Rotation { get; private set; }
    public Vector2Int   Anchor   { get; private set; }
    public DoorRuntime[] Doors   { get; private set; }

    public class DoorRuntime
    {
        public DoorSlot   Local;
        public Vector2Int WorldCell;
        public Dir        WorldDir;
        public bool       IsOpen;
        public GameObject Overlay; // null when open or not yet created
    }

    public void Setup(TileInstance instance, int rotation, Vector2Int anchor)
    {
        Instance = instance;
        Rotation = rotation;
        Anchor   = anchor;
        Doors    = new DoorRuntime[instance.Doors.Length];
        for (int i = 0; i < instance.Doors.Length; i++)
        {
            var d         = instance.Doors[i];
            var cellLocal = instance.Def.cells[d.CellIndex];
            var cellWorld = anchor + RotateOffset(cellLocal, rotation);
            Doors[i] = new DoorRuntime
            {
                Local     = d,
                WorldCell = cellWorld,
                WorldDir  = d.Face.Rotate(rotation),
                IsOpen    = false,
                Overlay   = null,
            };
        }
    }

    /// <summary>Returns the index of a door whose world face matches, or -1.</summary>
    public bool TryFindDoor(Vector2Int worldCell, Dir worldDir, out int doorIdx)
    {
        for (int i = 0; i < Doors.Length; i++)
        {
            var d = Doors[i];
            if (d.WorldCell == worldCell && d.WorldDir == worldDir)
            {
                doorIdx = i;
                return true;
            }
        }
        doorIdx = -1;
        return false;
    }

    public void SetDoorOverlay(int doorIdx, GameObject overlay)
    {
        Doors[doorIdx].Overlay = overlay;
    }

    public void MarkDoorOpenAtSpawn(int doorIdx)
    {
        Doors[doorIdx].IsOpen = true;
    }

    /// <summary>Open a door now: destroy its overlay and mark it open.</summary>
    public void OpenDoor(int doorIdx)
    {
        var d = Doors[doorIdx];
        if (d.IsOpen) return;
        d.IsOpen = true;
        if (d.Overlay != null)
        {
            Destroy(d.Overlay);
            d.Overlay = null;
        }
    }

    /// <summary>
    /// Permanently seal a door's overlay because a wall (not a matching door) was
    /// placed against it: recolour the overlay to the wall's tile colour so the
    /// slot blends in. Door stays closed.
    /// </summary>
    public void SealDoor(int doorIdx)
    {
        var d = Doors[doorIdx];
        if (d.Overlay == null) return;
        var mr  = d.Overlay.GetComponent<Renderer>();
        var mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", tileColor);
        mr.SetPropertyBlock(mpb);
    }

    public void RevealTop()
    {
        if (topRenderer != null) topRenderer.gameObject.SetActive(true);
    }

    public static Vector2Int RotateOffset(Vector2Int v, int steps)
    {
        for (int i = 0; i < steps; i++)
            v = new Vector2Int(v.y, -v.x);
        return v;
    }
}
