using System.Collections;
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

    /// <summary>
    /// Seal every still-closed door on this tile (called when the floor finishes).
    /// Doors that connected to a neighbour are already open and untouched.
    /// </summary>
    public void SealAllClosedDoors()
    {
        for (int i = 0; i < Doors.Length; i++)
        {
            if (!Doors[i].IsOpen && Doors[i].Overlay != null)
                SealDoor(i);
        }
    }

    public static Vector2Int RotateOffset(Vector2Int v, int steps)
    {
        for (int i = 0; i < steps; i++)
            v = new Vector2Int(v.y, -v.x);
        return v;
    }

    // ── animations ────────────────────────────────────────────────────────────

    /// <summary>Drop in from above with a slight scale pop. Run once at spawn.</summary>
    public IEnumerator AnimateDropIn(float duration, float dropHeight)
    {
        var restPos   = transform.position;
        var startPos  = restPos + Vector3.up * dropHeight;
        var restScale = transform.localScale;
        var startScale = restScale * 0.92f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u  = Mathf.Clamp01(t / duration);
            float eu = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic
            transform.position   = Vector3.LerpUnclamped(startPos,   restPos,   eu);
            transform.localScale = Vector3.LerpUnclamped(startScale, restScale, eu);
            yield return null;
        }
        transform.position   = restPos;
        transform.localScale = restScale;
    }

    /// <summary>Pop up then settle back. Used for floor-completion ripple.</summary>
    public IEnumerator AnimateBounce(float delay, float duration, float height)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var restPos = transform.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // sine arc 0→1→0 over the duration.
            float arc = Mathf.Sin(u * Mathf.PI);
            transform.position = restPos + Vector3.up * (arc * height);
            yield return null;
        }
        transform.position = restPos;
    }
}
