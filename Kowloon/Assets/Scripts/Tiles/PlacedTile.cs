using UnityEngine;

/// <summary>
/// Attached to every placed tile root. Stores the renderers so FloorManager
/// can call RevealTop() when the floor above is completed.
/// </summary>
public class PlacedTile : MonoBehaviour
{
    public Renderer bodyRenderer;
    public Renderer topRenderer;
    public Color    tileColor;

    public void RevealTop()
    {
        var mpb = new MaterialPropertyBlock();
        topRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", new Color(tileColor.r, tileColor.g, tileColor.b, 1f));
        topRenderer.SetPropertyBlock(mpb);
    }
}
