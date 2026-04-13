using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 成就卡欣赏层：<b>大卡区域以场景 UI 为准</b>——把 <see cref="cardDisplayImage"/> 当作显示窗口（非 Play 下可随意拖 RectTransform）；
/// 点击槽内任意成就卡时，仅把该卡的 Sprite/标题填到这个窗口里。Dim 为全屏半透明底。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class AchievementCardViewerOverlay : MonoBehaviour
{
    [Header("欣赏窗口（推荐自行在层级里摆好，并把引用拖到此处）")]
    [Tooltip("全屏半透明遮罩；可空（运行时仍会自动建 Dim）")]
    public Image dimImage;

    [Tooltip("成就大图显示在此 Image 上——这就是可拖动的「显示窗口」主体")]
    public Image cardDisplayImage;

    [Tooltip("标题；可空")]
    public TextMeshProUGUI cardTitleText;

    [Header("Dim 颜色（仅当使用自动/已绑定的 Dim 时有效）")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.72f);

    [Header("自动生成窗口时的默认尺寸（仅当 Card Display Image 仍为空、走自动创建时）")]
    [FormerlySerializedAs("previewMaxSize")]
    public Vector2 autoWindowSize = new Vector2(520f, 680f);

    [Header("编辑器布局预览（非 Play）")]
    public bool showLayoutInEditMode = true;

    public string editorPreviewTitle = "示例标题 · 调整 Card Display 窗口位置";

    public Sprite editorPreviewSprite;

    /// <summary>为 true 时：Esc 与点击遮罩/大图关闭无效，仅允许脚本 <see cref="Hide"/>（如失败卡流程）。</summary>
    public bool suppressUserDismiss;

    [Header("音效（查看大卡；Time.timeScale=0 时仍播放）")]
    [Tooltip("打开欣赏层时；留空则静音")]
    public AudioClip previewOpenClickClip;
    [Tooltip("点击遮罩/大图/标题关闭时；留空则静音（Esc 关闭不播放）")]
    public AudioClip previewCloseClickClip;
    [Range(0f, 1f)]
    public float previewClickVolume = 1f;

    [SerializeField] [HideInInspector] Vector2 _lastAutoWindowSize;

    CanvasGroup _canvasGroup;
    AudioSource _viewerSfx;

    Transform _runtimeReparentOriginalParent;
    int _runtimeReparentOriginalSiblingIndex = -1;

    bool _failureTopCanvasAdded;
    bool _failureRaycasterAdded;

#if UNITY_EDITOR
    static Sprite _editorWhiteSprite;
#endif

    const string NodeCardWindow = "CardWindow";
    const string NodeCardDisplay = "CardDisplay";
    const string LegacyPreviewStack = "PreviewStack";
    const string LegacyLargeCard = "LargeCard";
    const string NodeDim = "Dim";
    const string NodeTitle = "Title";

    void Awake()
    {
        EnsureBuilt();
        if (Application.isPlaying)
            HideImmediateForRuntimeOrHiddenEditor();
    }

    void OnEnable()
    {
        EnsureBuilt();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            ApplyEditorVisibility();
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureBuilt();
        ApplyAutoWindowSizeIfLegacy();
        ApplyDimColor();
        ApplyEditorVisibility();
    }
#endif

    void Update()
    {
        if (!Application.isPlaying)
            return;
        if (suppressUserDismiss)
            return;
        if (_canvasGroup != null && _canvasGroup.alpha > 0.01f && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public void Show(Sprite sprite, string title)
    {
        Show(sprite, title, playPreviewOpenSound: true);
    }

    /// <summary>
    /// 挂到场景 <see cref="Canvas.rootCanvas"/> 下并全屏拉伸，避免嵌套在成就面板等子 Canvas 里被遮挡或受父级 <see cref="CanvasGroup"/> 影响。
    /// 与 <see cref="RestoreRuntimeParentIfReparented"/> 成对调用。
    /// </summary>
    public void ReparentUnderCanvasRootAndStretch()
    {
        var rt = transform as RectTransform;
        if (rt == null)
            return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        if (transform.parent == root.transform)
        {
            StretchFull(rt);
            transform.SetAsLastSibling();
            return;
        }

        if (_runtimeReparentOriginalParent == null)
        {
            _runtimeReparentOriginalParent = transform.parent;
            _runtimeReparentOriginalSiblingIndex = transform.GetSiblingIndex();
        }

        transform.SetParent(root.transform, false);
        SetLayerRecursively(transform, root.gameObject.layer);
        StretchFull(rt);
        transform.SetAsLastSibling();
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (var i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    public void RestoreRuntimeParentIfReparented()
    {
        if (_runtimeReparentOriginalParent == null)
            return;

        var p = _runtimeReparentOriginalParent;
        var idx = _runtimeReparentOriginalSiblingIndex;
        _runtimeReparentOriginalParent = null;
        _runtimeReparentOriginalSiblingIndex = -1;

        transform.SetParent(p, false);
        if (idx >= 0 && p != null && idx < p.childCount)
            transform.SetSiblingIndex(idx);
    }

    public void Show(Sprite sprite, string title, bool playPreviewOpenSound)
    {
        EnsureBuilt();

        SetRuntimeRaycastsAndCloseHandlers(true);

        if (suppressUserDismiss)
            EnsureFailureDrawsAboveAllHud();

        if (cardDisplayImage != null)
        {
            cardDisplayImage.type = Image.Type.Simple;
            cardDisplayImage.preserveAspect = true;
            cardDisplayImage.sprite = sprite;
            cardDisplayImage.enabled = sprite != null;
            cardDisplayImage.color = Color.white;
            cardDisplayImage.SetAllDirty();
        }

        if (cardTitleText != null)
        {
            var t = title ?? string.Empty;
            cardTitleText.text = t;
            cardTitleText.gameObject.SetActive(!string.IsNullOrEmpty(t));
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (_canvasGroup != null)
        {
            if (suppressUserDismiss)
                _canvasGroup.ignoreParentGroups = true;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        Canvas.ForceUpdateCanvases();

        if (suppressUserDismiss)
            ApplyLockedDismissUi();
        if (playPreviewOpenSound)
            PlayPreviewOpenSound();
    }

    /// <summary>遮罩挡点击穿通，但禁用点击关闭与关闭音效。</summary>
    void ApplyLockedDismissUi()
    {
        if (dimImage != null)
            dimImage.raycastTarget = true;
        if (cardDisplayImage != null)
            cardDisplayImage.raycastTarget = false;
        if (cardTitleText != null)
            cardTitleText.raycastTarget = false;
        foreach (var c in GetComponentsInChildren<AchievementCardViewerCloseClick>(true))
            c.enabled = false;
    }

    /// <summary>失败层：本物体原无 Canvas 时加嵌套 Overlay + 超高 sortingOrder，避免被其它 UI/嵌套 Canvas 盖住。</summary>
    void EnsureFailureDrawsAboveAllHud()
    {
        if (GetComponent<Canvas>() != null)
            return;

        var cv = gameObject.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 32000;
        _failureTopCanvasAdded = true;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
            _failureRaycasterAdded = true;
        }
    }

    void TearDownFailureDrawBoost()
    {
        if (_failureRaycasterAdded)
        {
            var gr = GetComponent<GraphicRaycaster>();
            if (gr != null)
                Destroy(gr);
            _failureRaycasterAdded = false;
        }

        if (_failureTopCanvasAdded)
        {
            var cv = GetComponent<Canvas>();
            if (cv != null)
                Destroy(cv);
            _failureTopCanvasAdded = false;
        }

        if (_canvasGroup != null)
            _canvasGroup.ignoreParentGroups = false;
    }

    void EnsureViewerSfxSource()
    {
        if (_viewerSfx != null)
            return;
        _viewerSfx = GetComponent<AudioSource>();
        if (_viewerSfx == null)
            _viewerSfx = gameObject.AddComponent<AudioSource>();
        _viewerSfx.playOnAwake = false;
        _viewerSfx.loop = false;
        _viewerSfx.spatialBlend = 0f;
        _viewerSfx.ignoreListenerPause = true;
    }

    void PlayPreviewOpenSound()
    {
        if (previewOpenClickClip == null || !Application.isPlaying)
            return;
        EnsureViewerSfxSource();
        _viewerSfx.PlayOneShot(previewOpenClickClip, Mathf.Clamp01(previewClickVolume));
    }

    /// <summary>由 <see cref="AchievementCardViewerCloseClick"/> 在点击关闭时调用；Esc 关闭不调用。</summary>
    public void PlayPreviewCloseClickSound()
    {
        if (previewCloseClickClip == null || !Application.isPlaying)
            return;
        EnsureViewerSfxSource();
        _viewerSfx.PlayOneShot(previewCloseClickClip, Mathf.Clamp01(previewClickVolume));
    }

    public void Hide()
    {
        TearDownFailureDrawBoost();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (Application.isPlaying)
            gameObject.SetActive(false);

#if UNITY_EDITOR
        if (!Application.isPlaying && showLayoutInEditMode)
        {
            gameObject.SetActive(true);
            ShowEditorLayoutPreview();
        }
#endif
    }

    void EnsureBuilt()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        var rootRt = transform as RectTransform;
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
        }

        if (cardDisplayImage != null)
        {
            WireCloseClickTargets();
            return;
        }

        if (TryBindExistingNodes())
        {
            WireCloseClickTargets();
#if UNITY_EDITOR
            _lastAutoWindowSize = autoWindowSize;
#endif
            return;
        }

        CreateDefaultUiHierarchy();
#if UNITY_EDITOR
        _lastAutoWindowSize = autoWindowSize;
#endif
        WireCloseClickTargets();
        ApplyEditorVisibility();
    }

    bool TryBindExistingNodes()
    {
        var w = transform.Find(NodeCardWindow) as RectTransform
                ?? transform.Find(LegacyPreviewStack) as RectTransform;
        if (w == null)
            return false;

        var dispTf = w.Find(NodeCardDisplay) ?? w.Find(LegacyLargeCard);
        if (dispTf == null)
            return false;

        cardDisplayImage = dispTf.GetComponent<Image>();
        if (cardDisplayImage == null)
            return false;

        var dimTf = transform.Find(NodeDim);
        if (dimTf != null)
            dimImage = dimTf.GetComponent<Image>();

        var titleTf = w.Find(NodeTitle);
        if (titleTf != null)
            cardTitleText = titleTf.GetComponent<TextMeshProUGUI>();

        return true;
    }

    void CreateDefaultUiHierarchy()
    {
        if (transform.Find(NodeDim) == null)
        {
            var dimGo = new GameObject(NodeDim, typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(transform, false);
            var dimRt = dimGo.GetComponent<RectTransform>();
            StretchFull(dimRt);
            dimImage = dimGo.GetComponent<Image>();
            dimImage.color = dimColor;
            dimImage.raycastTarget = true;
        }

        if (transform.Find(NodeCardWindow) != null)
            return;

        var windowGo = new GameObject(NodeCardWindow, typeof(RectTransform));
        windowGo.transform.SetParent(transform, false);
        var winRt = windowGo.GetComponent<RectTransform>();
        winRt.anchorMin = winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = autoWindowSize;
        winRt.anchoredPosition = Vector2.zero;

        var displayGo = new GameObject(NodeCardDisplay, typeof(RectTransform), typeof(Image));
        displayGo.transform.SetParent(windowGo.transform, false);
        var dispRt = displayGo.GetComponent<RectTransform>();
        dispRt.anchorMin = new Vector2(0f, 0.25f);
        dispRt.anchorMax = new Vector2(1f, 1f);
        dispRt.offsetMin = new Vector2(16f, 0f);
        dispRt.offsetMax = new Vector2(-16f, -8f);
        cardDisplayImage = displayGo.GetComponent<Image>();
        cardDisplayImage.preserveAspect = true;
        cardDisplayImage.color = Color.white;
        cardDisplayImage.raycastTarget = true;

        var titleGo = new GameObject(NodeTitle, typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(windowGo.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(1f, 0.22f);
        titleRt.offsetMin = new Vector2(16f, 12f);
        titleRt.offsetMax = new Vector2(-16f, 8f);
        cardTitleText = titleGo.GetComponent<TextMeshProUGUI>();
        cardTitleText.fontSize = 26f;
        cardTitleText.alignment = TextAlignmentOptions.Center;
        cardTitleText.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            cardTitleText.font = TMP_Settings.defaultFontAsset;
    }

    void WireCloseClickTargets()
    {
        EnsureCloseClick(dimImage != null ? dimImage.gameObject : null);
        EnsureCloseClick(cardDisplayImage != null ? cardDisplayImage.gameObject : null);
        EnsureCloseClick(cardTitleText != null ? cardTitleText.gameObject : null);
    }

    static void EnsureCloseClick(GameObject go)
    {
        if (go == null)
            return;
        var c = go.GetComponent<AchievementCardViewerCloseClick>();
        if (c == null)
            c = go.AddComponent<AchievementCardViewerCloseClick>();
        c.Owner = go.GetComponentInParent<AchievementCardViewerOverlay>();
    }

#if UNITY_EDITOR
    void ApplyAutoWindowSizeIfLegacy()
    {
        var w = transform.Find(NodeCardWindow) as RectTransform;
        if (w == null)
            return;
        if (autoWindowSize == _lastAutoWindowSize)
            return;
        _lastAutoWindowSize = autoWindowSize;
        w.sizeDelta = autoWindowSize;
    }

#endif

    void ApplyDimColor()
    {
        if (dimImage != null)
            dimImage.color = dimColor;
    }

    void ApplyEditorVisibility()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && showLayoutInEditMode)
        {
            ShowEditorLayoutPreview();
            return;
        }
#endif
        HideImmediateForRuntimeOrHiddenEditor();
    }

    void ShowEditorLayoutPreview()
    {
        if (_canvasGroup == null || cardDisplayImage == null)
            return;

        SetRuntimeRaycastsAndCloseHandlers(false);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        if (dimImage != null)
        {
            dimImage.color = dimColor;
            dimImage.raycastTarget = false;
        }

#if UNITY_EDITOR
        var ph = editorPreviewSprite != null ? editorPreviewSprite : GetOrCreateEditorWhiteSprite();
        cardDisplayImage.sprite = ph;
        cardDisplayImage.color = Color.white;
        cardDisplayImage.enabled = true;
        cardDisplayImage.raycastTarget = false;
#endif

        if (cardTitleText != null)
        {
            cardTitleText.gameObject.SetActive(true);
            cardTitleText.text = editorPreviewTitle;
            cardTitleText.raycastTarget = false;
        }
    }

    void SetRuntimeRaycastsAndCloseHandlers(bool runtimeOn)
    {
        if (dimImage != null)
            dimImage.raycastTarget = runtimeOn;
        if (cardDisplayImage != null)
            cardDisplayImage.raycastTarget = runtimeOn;
        if (cardTitleText != null)
            cardTitleText.raycastTarget = runtimeOn;

        foreach (var c in GetComponentsInChildren<AchievementCardViewerCloseClick>(true))
            c.enabled = runtimeOn;
    }

    void HideImmediateForRuntimeOrHiddenEditor()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    static Sprite GetOrCreateEditorWhiteSprite()
    {
        if (_editorWhiteSprite != null)
            return _editorWhiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _editorWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        _editorWhiteSprite.name = "AchievementViewer_EditorWhite";
        return _editorWhiteSprite;
    }
#endif

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }
}

/// <summary>点击关闭欣赏层（挂在遮罩与大图上）。</summary>
[DisallowMultipleComponent]
public class AchievementCardViewerCloseClick : MonoBehaviour, IPointerClickHandler
{
    public AchievementCardViewerOverlay Owner;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Application.isPlaying)
            return;
        if (Owner != null && Owner.suppressUserDismiss)
            return;
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        Owner?.PlayPreviewCloseClickSound();
        Owner?.Hide();
    }
}
