using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-right HUD showing a continuously-rotating 3D mockup of the current
/// tile and the held tile, drawn over a backdrop sprite that contains both
/// slots in one image. Each slot has its own off-screen camera + RenderTexture
/// pointed at a tiny preview tile (body mesh only — door cutouts visible, no
/// closed-door overlays).
///
/// Authoring: drop on a GameObject and assign references in the inspector.
/// The full canvas hierarchy and preview cameras are built at runtime.
/// </summary>
public class TilePreviewPanel : MonoBehaviour
{
    [Header("References")]
    public TilePlacer      tilePlacer;
    public TileMeshLibrary meshLibrary;
    public GridManager     grid;

    [Header("Backdrop")]
    public Sprite backdropSprite;
    [Tooltip("Backdrop width as a fraction of screen width.")]
    [Range(0.1f, 0.5f)] public float widthFraction = 0.28f;
    [Tooltip("Pixel offset from the bottom-right corner (negative = inset).")]
    public Vector2 anchoredOffset = new Vector2(-30, 30);

    [Header("Slot positions (within the backdrop, 0..1)")]
    [Tooltip("Center of the large/current slot, 0..1 from bottom-left of the backdrop.")]
    public Vector2 currentSlotCenter = new Vector2(0.34f, 0.62f);
    [Tooltip("Side length of the large slot as a fraction of the backdrop's smaller dimension.")]
    [Range(0.1f, 1f)] public float currentSlotSize = 0.55f;
    [Tooltip("Center of the small/held slot.")]
    public Vector2 heldSlotCenter   = new Vector2(0.78f, 0.30f);
    [Range(0.1f, 1f)] public float heldSlotSize    = 0.32f;
    [Tooltip("Width / height ratio for the rendered slots. >1 = wider " +
             "(more horizontal room for tiles whose diagonals would otherwise clip).")]
    [Range(0.5f, 2.5f)] public float slotAspect = 1.4f;

    [Header("Render texture resolution")]
    public int currentRTSize = 256;
    public int heldRTSize    = 192;

    [Header("Spin")]
    [Tooltip("Rotation speed in degrees per second.")]
    [Range(0f, 360f)] public float spinDegPerSec = 60f;
    [Tooltip("Held tile's starting rotation offset from current, in degrees, " +
             "so the two don't appear locked together.")]
    [Range(-180f, 180f)] public float spinOffsetDeg = 90f;

    [Header("Preview camera")]
    [Tooltip("Vertical (X) tilt of the preview camera, degrees.")]
    [Range(0f, 80f)] public float cameraTiltDeg = 30f;
    [Tooltip("Frame size of the preview camera (orthographic). " +
             "Smaller = tile fills more of the slot.")]
    public float orthoSize = 1.6f;
    public Color clearColor = new Color(0f, 0f, 0f, 0f);

    // ── runtime ───────────────────────────────────────────────────────────────

    const int    PreviewLayer = 6; // matches "TilePreview" in TagManager.asset
    const float  PreviewWorldSpacing = 200f; // world distance between the two preview rigs

    private GameObject _previewRoot;
    private GameObject _currentTile, _heldTile;
    private MeshFilter _currentMf,   _heldMf;
    private MeshRenderer _currentMr, _heldMr;
    private Transform  _currentMeshXf, _heldMeshXf;
    private Camera     _currentCam,  _heldCam;
    private RenderTexture _rtCurrent, _rtHeld;
    private RawImage   _rawCurrent, _rawHeld;
    private Material   _previewMat;

    private TileInstance _lastCurrent, _lastHeld;
    private float        _spinAngle;

    void Awake()
    {
        if (tilePlacer  == null) tilePlacer  = FindFirstObjectByType<TilePlacer>();
        if (meshLibrary == null) meshLibrary = FindFirstObjectByType<TileMeshLibrary>();
        if (grid        == null) grid        = FindFirstObjectByType<GridManager>();

        BuildPreviewRig();
        BuildCanvas();
    }

    void OnDestroy()
    {
        if (_rtCurrent != null) _rtCurrent.Release();
        if (_rtHeld    != null) _rtHeld.Release();
    }

    // ── build ─────────────────────────────────────────────────────────────────

    void BuildPreviewRig()
    {
        // Plain unlit material for preview tiles (URP/Unlit, no dither).
        _previewMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        {
            name             = "TilePreviewMat",
            enableInstancing = true,
        };
        _previewMat.SetFloat("_Cull", 0f);

        _previewRoot = new GameObject("TilePreviewRig");
        _previewRoot.transform.SetParent(transform, false);
        _previewRoot.transform.position = new Vector3(0f, 9999f, 0f);

        _currentTile = MakePreviewTile("PreviewCurrent", new Vector3(-PreviewWorldSpacing, 0, 0),
                                       out _currentMf, out _currentMr, out _currentMeshXf);
        _heldTile    = MakePreviewTile("PreviewHeld",    new Vector3( PreviewWorldSpacing, 0, 0),
                                       out _heldMf,    out _heldMr,    out _heldMeshXf);

        _rtCurrent = MakeRT(currentRTSize);
        _rtHeld    = MakeRT(heldRTSize);

        _currentCam = MakePreviewCam("PreviewCamCurrent", _currentTile.transform.position, _rtCurrent);
        _heldCam    = MakePreviewCam("PreviewCamHeld",    _heldTile.transform.position,    _rtHeld);

        // Stagger initial rotation so the two don't spin in lockstep.
        _heldTile.transform.localRotation = Quaternion.Euler(0f, spinOffsetDeg, 0f);
    }

    GameObject MakePreviewTile(string name, Vector3 localOffset,
                               out MeshFilter mf, out MeshRenderer mr, out Transform meshXf)
    {
        // Pivot GameObject — rotates here, sits at the camera's focus point.
        var pivot = new GameObject(name);
        pivot.transform.SetParent(_previewRoot.transform, false);
        pivot.transform.localPosition = localOffset;
        pivot.layer = PreviewLayer;

        // Mesh holder — re-centered each time the mesh changes so the tile's
        // bounds center sits on the pivot, giving us nice center-of-mass spin.
        var mesh = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
        mesh.transform.SetParent(pivot.transform, false);
        mesh.layer = PreviewLayer;
        mf = mesh.GetComponent<MeshFilter>();
        mr = mesh.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _previewMat;
        meshXf = mesh.transform;
        return pivot;
    }

    Camera MakePreviewCam(string name, Vector3 lookAt, RenderTexture target)
    {
        var go = new GameObject(name, typeof(Camera));
        go.transform.SetParent(_previewRoot.transform, false);
        // Position camera back along an iso-ish angle.
        var dir = Quaternion.Euler(cameraTiltDeg, 45f, 0f) * Vector3.back;
        go.transform.position = lookAt + dir * 5f;
        go.transform.LookAt(lookAt);

        var cam = go.GetComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = clearColor;
        cam.cullingMask      = 1 << PreviewLayer;
        cam.targetTexture    = target;
        cam.nearClipPlane    = 0.1f;
        cam.farClipPlane     = 100f;
        cam.allowHDR         = false;
        cam.allowMSAA        = false;
        return cam;
    }

    RenderTexture MakeRT(int size)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(size * slotAspect));
        int h = size;
        var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        rt.Create();
        return rt;
    }

    void BuildCanvas()
    {
        // Canvas (overlay).
        var canvasGo = new GameObject("TilePreviewCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0f;

        // Backdrop image (anchored bottom-right).
        var bgGo = new GameObject("PreviewBackdrop",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = new Vector2(1f, 0f);
        bgRt.anchorMax = new Vector2(1f, 0f);
        bgRt.pivot     = new Vector2(1f, 0f);
        bgRt.anchoredPosition = anchoredOffset;

        float refW = scaler.referenceResolution.x;
        float panelW = refW * widthFraction;
        float aspect = backdropSprite != null
            ? backdropSprite.rect.width / Mathf.Max(1f, backdropSprite.rect.height)
            : 1.5f;
        float panelH = panelW / aspect;
        bgRt.sizeDelta = new Vector2(panelW, panelH);

        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite        = backdropSprite;
        bgImg.preserveAspect = true;
        bgImg.raycastTarget  = false;

        // Slot raw images (parented to backdrop, normalised positions).
        _rawCurrent = MakeSlot(bgGo.transform, "CurrentSlot", _rtCurrent,
            currentSlotCenter, currentSlotSize, panelW, panelH);
        _rawHeld    = MakeSlot(bgGo.transform, "HeldSlot",    _rtHeld,
            heldSlotCenter,    heldSlotSize,    panelW, panelH);
    }

    RawImage MakeSlot(Transform parent, string name, RenderTexture rt,
                      Vector2 centerNorm, float sizeFraction,
                      float backdropW, float backdropH)
    {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var rtRt = (RectTransform)go.transform;
        rtRt.anchorMin = Vector2.zero;
        rtRt.anchorMax = Vector2.zero;
        rtRt.pivot     = new Vector2(0.5f, 0.5f);

        // Slot is sized by its smaller dimension; widening via slotAspect
        // matches the camera's wider frustum so the rendered tile keeps
        // proper proportions instead of squishing.
        float side = Mathf.Min(backdropW, backdropH) * sizeFraction;
        rtRt.sizeDelta        = new Vector2(side * slotAspect, side);
        rtRt.anchoredPosition = new Vector2(centerNorm.x * backdropW, centerNorm.y * backdropH);

        var raw = go.GetComponent<RawImage>();
        raw.texture       = rt;
        raw.raycastTarget = false;
        return raw;
    }

    // ── per-frame ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (tilePlacer == null) return;

        // Sync mesh when the current/held instance changes.
        var curr = tilePlacer.CurrentInstance;
        if (curr != _lastCurrent) { _lastCurrent = curr; SyncMesh(_currentMf, _currentMr, curr); }
        var held = tilePlacer.HeldInstance;
        if (held != _lastHeld)    { _lastHeld    = held; SyncMesh(_heldMf,    _heldMr,    held); }

        // Spin both, with offset preserved.
        float delta = spinDegPerSec * Time.deltaTime;
        _spinAngle = (_spinAngle + delta) % 360f;
        _currentTile.transform.localRotation = Quaternion.Euler(0f, _spinAngle, 0f);
        _heldTile.transform.localRotation    = Quaternion.Euler(0f, _spinAngle + spinOffsetDeg, 0f);
    }

    void SyncMesh(MeshFilter mf, MeshRenderer mr, TileInstance inst)
    {
        if (inst == null) { mf.sharedMesh = null; return; }
        // Body mesh has door cutouts but no overlays — exactly the "open doors,
        // walls only" preview the user wants.
        mf.sharedMesh = meshLibrary.GetBodyMesh(inst);

        // Re-center the geometry on the pivot so rotation spins around the
        // tile's centroid rather than its anchor cell.
        var bounds = mf.sharedMesh.bounds;
        mf.transform.localPosition = -bounds.center;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", inst.Def.color);
        mr.SetPropertyBlock(mpb);
    }
}
