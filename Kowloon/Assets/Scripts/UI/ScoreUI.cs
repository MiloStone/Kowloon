using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right HUD: a 9-sliced textbox containing "SCORE: N", updated from
/// ScoreManager events. Follows the same author-once / build-at-runtime
/// pattern as ContractPanel.
/// </summary>
public class ScoreUI : MonoBehaviour
{
    [Header("References")]
    public ScoreManager scoreManager;

    [Header("Sprite & Font")]
    public Sprite bubbleSprite;
    public Font   font;

    [Header("Layout")]
    [Tooltip("Bubble width as a fraction of screen width.")]
    [Range(0.08f, 0.4f)] public float widthFraction = 0.16f;
    [Tooltip("Pixel offset from the top-right corner (negative x = inset).")]
    public Vector2 anchoredOffset = new Vector2(-30, -30);
    [Tooltip("Shrinks the bubble's 9-slice borders. Higher = smaller corners.")]
    [Range(0.5f, 16f)] public float bubblePixelsPerUnitMultiplier = 4f;
    public RectOffset padding;

    [Header("Style")]
    public string labelPrefix    = "SCORE: ";
    public Color  textColor      = new Color(0.15f, 0.20f, 0.25f);
    public int    fontSize       = 42;

    private Text _scoreText;

    void Awake()
    {
        if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
        if (padding == null) padding = new RectOffset(60, 60, 50, 50);
        BuildHierarchy();
    }

    void OnEnable()
    {
        if (scoreManager != null) scoreManager.ScoreChanged += Refresh;
    }

    void OnDisable()
    {
        if (scoreManager != null) scoreManager.ScoreChanged -= Refresh;
    }

    void Start() => Refresh();

    void BuildHierarchy()
    {
        var canvasGo = new GameObject("ScoreCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0f;

        // Bubble (anchored top-right, grows down via ContentSizeFitter).
        var bubbleGo = new GameObject("ScoreBubble",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        bubbleGo.transform.SetParent(canvasGo.transform, false);
        var bubbleRt = (RectTransform)bubbleGo.transform;
        bubbleRt.anchorMin = new Vector2(1f, 1f);
        bubbleRt.anchorMax = new Vector2(1f, 1f);
        bubbleRt.pivot     = new Vector2(1f, 1f);
        bubbleRt.anchoredPosition = anchoredOffset;
        bubbleRt.sizeDelta = new Vector2(widthFraction * 1920f, 100f);

        var bubbleImg = bubbleGo.GetComponent<Image>();
        bubbleImg.sprite                  = bubbleSprite;
        bubbleImg.type                    = Image.Type.Sliced;
        bubbleImg.preserveAspect          = false;
        bubbleImg.pixelsPerUnitMultiplier = bubblePixelsPerUnitMultiplier;
        bubbleImg.raycastTarget           = false;

        var vlg = bubbleGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding                = padding;
        vlg.spacing                = 0;
        vlg.childAlignment         = TextAnchor.MiddleCenter;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var fitter = bubbleGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("ScoreText",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(bubbleGo.transform, false);
        _scoreText = textGo.GetComponent<Text>();
        if (font != null) _scoreText.font = font;
        _scoreText.text                = labelPrefix + "0";
        _scoreText.fontSize            = fontSize;
        _scoreText.color               = textColor;
        _scoreText.alignment           = TextAnchor.MiddleCenter;
        _scoreText.horizontalOverflow  = HorizontalWrapMode.Overflow;
        _scoreText.verticalOverflow    = VerticalWrapMode.Overflow;
    }

    void Refresh()
    {
        if (_scoreText == null) return;
        int s = scoreManager != null ? scoreManager.Score : 0;
        _scoreText.text = labelPrefix + s;
    }
}
