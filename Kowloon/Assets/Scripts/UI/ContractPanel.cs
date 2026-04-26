using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Camera-overlay panel showing the current floor's contract: title + a
/// checklist of constraint items, each with a check/x icon. Rebuilds when
/// FloorManager.ContractChanged fires (placement or new floor).
///
/// Authoring: drop on a GameObject in the scene and assign the five sprite/font
/// references in the inspector. The full canvas hierarchy is built at runtime.
/// </summary>
public class ContractPanel : MonoBehaviour
{
    [Header("References")]
    public FloorManager floorManager;

    [Header("Sprites")]
    public Sprite bubbleSprite;
    public Sprite checkSprite;
    public Sprite xSprite;

    [Header("Fonts")]
    public Font titleFont;
    public Font bodyFont;

    [Header("Layout")]
    [Tooltip("Bubble width as a fraction of the screen width.")]
    [Range(0.15f, 0.5f)] public float widthFraction = 0.25f;
    [Tooltip("Left edge inset as a fraction of screen width.")]
    [Range(0.0f, 0.1f)]  public float leftInsetFraction = 0.02f;

    [Header("Style")]
    public string titleText        = "CONTRACT";
    public Color  titleColor       = new Color(0.15f, 0.20f, 0.25f);
    public Color  bodyColor        = new Color(0.15f, 0.20f, 0.25f);
    public int    titleFontSize    = 48;
    public int    bodyFontSize     = 28;
    public int    iconSize         = 36;
    public int    rowSpacing       = 6;
    public RectOffset padding;

    // built at runtime
    private RectTransform _listRoot;

    void Awake()
    {
        if (floorManager == null) floorManager = FindFirstObjectByType<FloorManager>();
        if (padding == null) padding = new RectOffset(40, 40, 36, 36);
        BuildHierarchy();
    }

    void OnEnable()
    {
        if (floorManager != null) floorManager.ContractChanged += Rebuild;
    }

    void OnDisable()
    {
        if (floorManager != null) floorManager.ContractChanged -= Rebuild;
    }

    void Start() => Rebuild();

    // ── hierarchy ─────────────────────────────────────────────────────────────

    void BuildHierarchy()
    {
        // Canvas (overlay, scales with screen width).
        var canvasGo = new GameObject("ContractCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode      = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode  = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f; // match width — bubble is sized in screen-width units

        // Bubble (anchored left, vertically centered).
        var bubbleGo = new GameObject("ContractBubble",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        bubbleGo.transform.SetParent(canvasGo.transform, false);
        var bubbleRt = (RectTransform)bubbleGo.transform;
        bubbleRt.anchorMin = new Vector2(0f, 0.5f);
        bubbleRt.anchorMax = new Vector2(0f, 0.5f);
        bubbleRt.pivot     = new Vector2(0f, 0.5f);
        bubbleRt.anchoredPosition = new Vector2(leftInsetFraction * 1920f, 0f);
        bubbleRt.sizeDelta = new Vector2(widthFraction * 1920f, 200f);

        var bubbleImg = bubbleGo.GetComponent<Image>();
        bubbleImg.sprite = bubbleSprite;
        bubbleImg.type   = Image.Type.Sliced;
        bubbleImg.preserveAspect = false;

        var vlg = bubbleGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = padding;
        vlg.spacing = rowSpacing;
        vlg.childAlignment             = TextAnchor.UpperLeft;
        vlg.childControlWidth          = true;
        vlg.childControlHeight         = true;
        vlg.childForceExpandWidth      = true;
        vlg.childForceExpandHeight     = false;

        var fitter = bubbleGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Title.
        var titleGo = new GameObject("Title",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleGo.transform.SetParent(bubbleGo.transform, false);
        var titleTxt = titleGo.GetComponent<Text>();
        if (titleFont != null) titleTxt.font = titleFont;
        titleTxt.text                = titleText;
        titleTxt.fontSize            = titleFontSize;
        titleTxt.color               = titleColor;
        titleTxt.alignment           = TextAnchor.UpperLeft;
        titleTxt.horizontalOverflow  = HorizontalWrapMode.Overflow;
        titleTxt.verticalOverflow    = VerticalWrapMode.Overflow;

        // List root (children = checklist rows).
        var listGo = new GameObject("Checklist",
            typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGo.transform.SetParent(bubbleGo.transform, false);
        var listVlg = listGo.GetComponent<VerticalLayoutGroup>();
        listVlg.spacing                = rowSpacing;
        listVlg.childAlignment         = TextAnchor.UpperLeft;
        listVlg.childControlWidth      = true;
        listVlg.childControlHeight     = true;
        listVlg.childForceExpandWidth  = true;
        listVlg.childForceExpandHeight = false;
        _listRoot = (RectTransform)listGo.transform;
    }

    // ── refresh ───────────────────────────────────────────────────────────────

    void Rebuild()
    {
        if (_listRoot == null) return;

        for (int i = _listRoot.childCount - 1; i >= 0; i--)
            Destroy(_listRoot.GetChild(i).gameObject);

        var scenario = floorManager != null ? floorManager.CurrentScenario : null;
        if (scenario == null) return;

        var items = scenario.BuildChecklist(floorManager.Graph);
        foreach (var item in items) AddRow(item);
    }

    void AddRow(ChecklistItem item)
    {
        var rowGo = new GameObject("Row",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGo.transform.SetParent(_listRoot, false);
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 12;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // Icon.
        var iconGo = new GameObject("Icon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconGo.transform.SetParent(rowGo.transform, false);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.sprite = item.Satisfied ? checkSprite : xSprite;
        iconImg.preserveAspect = true;
        var iconLe = iconGo.GetComponent<LayoutElement>();
        iconLe.preferredWidth  = iconSize;
        iconLe.preferredHeight = iconSize;
        iconLe.minWidth        = iconSize;
        iconLe.minHeight       = iconSize;

        // Label.
        var labelGo = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        labelGo.transform.SetParent(rowGo.transform, false);
        var labelTxt = labelGo.GetComponent<Text>();
        if (bodyFont != null) labelTxt.font = bodyFont;
        labelTxt.text                = item.Text;
        labelTxt.fontSize            = bodyFontSize;
        labelTxt.color               = bodyColor;
        labelTxt.alignment           = TextAnchor.MiddleLeft;
        labelTxt.horizontalOverflow  = HorizontalWrapMode.Wrap;
        labelTxt.verticalOverflow    = VerticalWrapMode.Overflow;
        var labelLe = labelGo.GetComponent<LayoutElement>();
        labelLe.flexibleWidth = 1f;
    }
}
