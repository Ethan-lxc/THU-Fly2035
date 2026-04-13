using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 背包面板：可配置列×行固定网格（默认 4×2；可选 ScrollRect 在运行时被关闭）、线索卡点击放大；交互与成就台对齐。
/// </summary>
public class InventoryPanelController : MonoBehaviour, ICardPreviewSource
{
    [Header("引用")]
    public Button toggleButton;

    [Tooltip("可选；打开背包前关闭暂停/成就。空则打开时 FindObjectOfType")]
    public GameplayHudLayout gameplayHudLayout;

    [Tooltip("背包面板根（建议带 CanvasGroup）")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("无 CanvasGroup 时可用普通 GameObject 控制显隐")]
    public GameObject panelFallbackRoot;

    [Tooltip("卡片父节点；空则使用 ScrollRect.content")]
    public RectTransform contentRoot;

    [Tooltip("已废弃：不再使用占位 Prefab")]
    [HideInInspector] public GameObject slotPlaceholderPrefab;

    [HideInInspector] public int placeholderSlotCount = 12;

    [HideInInspector] public bool runtimeInventoryOnly;

    [Tooltip("空则 FindObjectOfType")]
    public InventoryRuntime inventoryRuntime;

    [Header("行为")]
    public bool startClosed = true;

    [Tooltip("可选；若层级里仍有 Scroll View，仅用于定位 Content，滚动会在运行时关闭")]
    public ScrollRect scrollRect;

    [Tooltip("卡面欣赏层（可与成就台共用同 Canvas 下已有组件）")]
    public AchievementCardViewerOverlay achievementCardViewer;

    [Header("网格")]
    [Tooltip("列数（横向格子数）")]
    [Min(1)] public int gridColumns = 4;

    [Tooltip("行数（纵向排数）；总槽位 = 列 × 行")]
    [Min(1)] public int gridRows = 2;

    [Tooltip("单格尺寸，与成就台默认一致时可保留 78×96")]
    public Vector2 cellSize = new Vector2(78f, 96f);

    public Vector2 cellSpacing = new Vector2(6f, 6f);

    [Tooltip("相对 Content 根的四边留白")]
    public RectOffset gridPadding;

    [Tooltip("整块网格在 Content 区域内的对齐")]
    public TextAnchor gridChildAlignment = TextAnchor.UpperLeft;

    int InventorySlotCount => Mathf.Max(1, gridColumns) * Mathf.Max(1, gridRows);

    [Header("外观（可选图片）")]
    [Tooltip("背包整块面板背景")]
    public Sprite panelBackgroundSprite;

    public Color panelBackgroundTint = Color.white;

    [Tooltip("空则尝试用本物体或子物体上的 Image 作为面板底图")]
    public Image panelBackgroundImage;

    [Header("空槽占位")]
    public Color emptySlotBackground = new Color(0.15f, 0.15f, 0.18f, 0.85f);

    [Tooltip("空槽底图；不指定则仅用 Empty Slot Background 纯色")]
    public Sprite emptySlotSprite;

    [Header("线索卡 UI")]
    [Tooltip("与成就台相同要求：RectTransform 根且含 Image；空则用运行时简易卡")]
    public GameObject itemCardPrefab;

    [Tooltip("线索增加后自动打开背包（仅当数量变多时）")]
    public bool openPanelWhenItemAdded = true;

    [Header("音效（背包开关按钮；Time.timeScale=0 时仍播放）")]
    public AudioClip panelToggleClickClip;

    [Range(0f, 1f)]
    public float panelToggleClickVolume = 1f;

    [Header("入口按钮悬停")]
    [Range(0.4f, 1f)] public float entryHoverBrightness = 0.78f;

    [Range(0.35f, 1f)] public float entryPressedBrightness = 0.65f;

    [Min(0.01f)] public float entryColorTransitionDuration = 0.08f;

    bool _open;
    AchievementGridSlot[] _slots;
    AchievementCardViewerOverlay _runtimeCardViewer;
    AudioSource _panelSfx;
    int _lastClueCount = -1;

#if UNITY_EDITOR
    bool _viewerEnsureScheduled;
#endif

    public bool IsOpen => _open;

    void Awake()
    {
        AutoResolveReferences();
        ScrollRectStaticGridConfigurator.ConfigureForStaticGrid(scrollRect);
        ApplyPanelBackground();
        if (toggleButton != null)
        {
            UiButtonHoverTint.Apply(
                toggleButton,
                entryHoverBrightness,
                entryPressedBrightness,
                entryColorTransitionDuration);
            toggleButton.onClick.AddListener(Toggle);
        }

        if (inventoryRuntime == null)
            inventoryRuntime = FindObjectOfType<InventoryRuntime>();

        ResolveContentRoot();
        EnsureGridLayout();
        BuildGridSlots();

        _lastClueCount = inventoryRuntime != null ? inventoryRuntime.Clues.Count : 0;
        RefreshFromRuntime();

        if (inventoryRuntime != null)
            inventoryRuntime.OnChanged += OnInventoryRuntimeChanged;

        SetOpen(!startClosed);
    }

    void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(Toggle);
        if (inventoryRuntime != null)
            inventoryRuntime.OnChanged -= OnInventoryRuntimeChanged;
    }

    void AutoResolveReferences()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponentInParent<CanvasGroup>();
        if (gameplayHudLayout == null)
            gameplayHudLayout = FindObjectOfType<GameplayHudLayout>();
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        ResolveContentRoot();
        if (panelBackgroundImage == null)
        {
            panelBackgroundImage = GetComponent<Image>();
            if (panelBackgroundImage == null && transform.childCount > 0)
                panelBackgroundImage = transform.GetChild(0).GetComponent<Image>();
        }
    }

    void ApplyPanelBackground()
    {
        var img = panelBackgroundImage;
        if (img == null)
            return;
        if (panelBackgroundSprite != null)
        {
            img.sprite = panelBackgroundSprite;
            img.type = Image.Type.Simple;
        }

        img.color = panelBackgroundTint;
    }

    void ResolveContentRoot()
    {
        if (contentRoot == null && scrollRect != null)
            contentRoot = scrollRect.content;
    }

    void EnsureGridLayout()
    {
        if (contentRoot == null)
            return;

        var vl = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (vl != null)
            Destroy(vl);

        var hl = contentRoot.GetComponent<HorizontalLayoutGroup>();
        if (hl != null)
            Destroy(hl);

        var grid = contentRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = contentRoot.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = cellSize;
        grid.spacing = cellSpacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, gridColumns);
        grid.childAlignment = gridChildAlignment;
        if (gridPadding == null)
            gridPadding = new RectOffset(4, 4, 4, 4);
        grid.padding = gridPadding;

        var fit = contentRoot.GetComponent<ContentSizeFitter>();
        if (fit == null)
            fit = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void BuildGridSlots()
    {
        ResolveContentRoot();
        if (contentRoot == null)
            return;

        for (var i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var n = InventorySlotCount;
        _slots = new AchievementGridSlot[n];

        for (var i = 0; i < n; i++)
        {
            var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(contentRoot, false);
            var bg = slotGo.GetComponent<Image>();
            if (emptySlotSprite != null)
            {
                bg.sprite = emptySlotSprite;
                bg.type = Image.Type.Simple;
            }

            bg.color = emptySlotBackground;
            bg.raycastTarget = true;

            var mountGo = new GameObject("CardMount", typeof(RectTransform));
            mountGo.transform.SetParent(slotGo.transform, false);
            var mountRt = mountGo.GetComponent<RectTransform>();
            StretchFull(mountRt);

            var slot = slotGo.AddComponent<AchievementGridSlot>();
            slot.cardMount = mountRt;
            _slots[i] = slot;
        }
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
    }

    void OnInventoryRuntimeChanged()
    {
        var count = inventoryRuntime != null ? inventoryRuntime.Clues.Count : 0;
        if (openPanelWhenItemAdded && count > _lastClueCount)
            SetOpen(true);
        _lastClueCount = count;
        RefreshFromRuntime();
    }

    public void Toggle()
    {
        PlayPanelToggleClick();
        SetOpen(!_open);
    }

    void EnsurePanelSfxSource()
    {
        if (_panelSfx != null)
            return;
        _panelSfx = GetComponent<AudioSource>();
        if (_panelSfx == null)
            _panelSfx = gameObject.AddComponent<AudioSource>();
        _panelSfx.playOnAwake = false;
        _panelSfx.loop = false;
        _panelSfx.spatialBlend = 0f;
        _panelSfx.ignoreListenerPause = true;
    }

    void PlayPanelToggleClick()
    {
        if (panelToggleClickClip == null || !Application.isPlaying)
            return;
        EnsurePanelSfxSource();
        _panelSfx.PlayOneShot(panelToggleClickClip, Mathf.Clamp01(panelToggleClickVolume));
    }

    public void SetOpen(bool open)
    {
        if (open)
        {
            var hud = gameplayHudLayout != null ? gameplayHudLayout : FindObjectOfType<GameplayHudLayout>();
            hud?.CloseOtherPanelsBeforeInventory();
        }

        _open = open;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = open ? 1f : 0f;
            panelCanvasGroup.interactable = open;
            panelCanvasGroup.blocksRaycasts = open;
        }
        else if (panelFallbackRoot != null)
            panelFallbackRoot.SetActive(open);
        else if (scrollRect != null)
            scrollRect.gameObject.SetActive(open);
        else
        {
            var root = panelBackgroundImage != null ? panelBackgroundImage.gameObject : gameObject;
            root.SetActive(open);
        }
    }

    /// <summary>
    /// 将 <see cref="InventoryRuntime"/> 中的线索填入槽位（最多 16 条）；超出部分仅打日志不在 UI 显示（与成就台槽满行为类似）。
    /// </summary>
    public void RefreshFromRuntime()
    {
        if (contentRoot == null || _slots == null)
            return;
        if (inventoryRuntime == null)
            inventoryRuntime = FindObjectOfType<InventoryRuntime>();

        foreach (var s in _slots)
        {
            if (s?.cardMount == null)
                continue;
            for (var i = s.cardMount.childCount - 1; i >= 0; i--)
                Destroy(s.cardMount.GetChild(i).gameObject);
        }

        if (inventoryRuntime == null)
            return;

        var clues = inventoryRuntime.Clues;
        var maxSlots = InventorySlotCount;
        if (clues.Count > maxSlots)
        {
            Debug.LogWarning(
                $"{nameof(InventoryPanelController)}: 线索数量 {clues.Count} 超过背包槽位 {maxSlots}（{gridColumns}×{gridRows}），仅显示前 {maxSlots} 条。");
        }

        var n = Mathf.Min(clues.Count, maxSlots);
        for (var i = 0; i < n; i++)
        {
            var clue = clues[i];
            var slot = _slots[i];
            if (slot?.cardMount == null)
                continue;

            var go = InstantiateItemCardUi(slot.cardMount, clue.icon, clue.title);
            var rt = go.transform as RectTransform;
            if (rt != null)
                StretchFull(rt);

            ApplyIconAndTitleToCard(go, clue.icon, clue.title);
            var hitTarget = PickCardHitTarget(go, clue.icon);
            var zoom = hitTarget.GetComponent<AchievementCardClickZoom>()
                       ?? hitTarget.AddComponent<AchievementCardClickZoom>();
            zoom.Configure(this, clue.icon, clue.title);
        }
    }

    GameObject InstantiateItemCardUi(Transform mount, Sprite icon, string title)
    {
        if (itemCardPrefab != null &&
            itemCardPrefab.transform is RectTransform &&
            itemCardPrefab.GetComponentInChildren<Image>(true) != null)
            return Instantiate(itemCardPrefab, mount);

        if (itemCardPrefab != null)
        {
            var hasSr = itemCardPrefab.GetComponentInChildren<SpriteRenderer>(true) != null;
            Debug.LogWarning(
                $"{nameof(InventoryPanelController)}: 线索卡 Prefab「{itemCardPrefab.name}」不是 UI 卡。已改用运行时 UI 卡。" +
                (hasSr ? "（检测到 SpriteRenderer）" : string.Empty));
        }

        return CreateRuntimeItemCard(mount, icon, title);
    }

    static GameObject CreateRuntimeItemCard(Transform mount, Sprite icon, string title)
    {
        var cardRoot = new GameObject("ClueCard_Runtime", typeof(RectTransform), typeof(Image));
        cardRoot.transform.SetParent(mount, false);
        var rootImg = cardRoot.GetComponent<Image>();
        rootImg.sprite = icon;
        rootImg.enabled = icon != null;
        rootImg.preserveAspect = true;
        rootImg.color = Color.white;
        rootImg.raycastTarget = true;

        if (!string.IsNullOrEmpty(title))
        {
            var tGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(cardRoot.transform, false);
            var tRt = tGo.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0f);
            tRt.anchorMax = new Vector2(1f, 0.35f);
            tRt.offsetMin = new Vector2(4f, 2f);
            tRt.offsetMax = new Vector2(-4f, -2f);
            var tmp = tGo.GetComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 18f;
            tmp.alignment = TextAlignmentOptions.Bottom;
            tmp.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }

        return cardRoot;
    }

    static GameObject PickCardHitTarget(GameObject cardRoot, Sprite icon)
    {
        if (cardRoot == null)
            return null;

        if (icon != null)
        {
            foreach (var img in cardRoot.GetComponentsInChildren<Image>(true))
            {
                if (img != null && img.sprite == icon && img.color.a > 0.05f)
                    return img.gameObject;
            }
        }

        Image best = null;
        var bestArea = 0;
        foreach (var img in cardRoot.GetComponentsInChildren<Image>(true))
        {
            if (img == null || img.sprite == null || img.color.a <= 0.05f)
                continue;
            if (img.sprite.name == "AchievementViewer_EditorWhite")
                continue;

            var r = img.sprite.textureRect;
            var area = Mathf.RoundToInt(r.width * r.height);
            if (area <= 0)
            {
                var tex = img.sprite.texture;
                area = tex != null ? tex.width * tex.height : 1;
            }

            if (area > bestArea)
            {
                bestArea = area;
                best = img;
            }
        }

        return best != null ? best.gameObject : cardRoot;
    }

    static void ApplyIconAndTitleToCard(GameObject go, Sprite icon, string title)
    {
        var img = go.GetComponentInChildren<Image>();
        if (img != null && icon != null)
        {
            img.sprite = icon;
            img.enabled = true;
        }

        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null && !string.IsNullOrEmpty(title))
            tmp.text = title;
    }

    public void ShowAchievementCardPreview(Sprite sprite, string title)
    {
        var viewer = EnsureAchievementCardViewer();
        if (viewer == null)
            return;
        viewer.Show(sprite, title);
    }

    AchievementCardViewerOverlay EnsureAchievementCardViewer()
    {
        AchievementCardViewerOverlay v = null;
        if (achievementCardViewer != null)
            v = achievementCardViewer;
        else if (_runtimeCardViewer != null)
            v = _runtimeCardViewer;
        else
        {
            var found = FindViewerOnCanvas();
            if (found != null)
            {
                if (Application.isPlaying)
                    _runtimeCardViewer = found;
                else
                    BindViewerReference(found);
                v = found;
            }
            else
            {
                var created = CreateViewerOnCanvas(registerUndo: !Application.isPlaying);
                if (created == null)
                    return null;
                if (Application.isPlaying)
                    _runtimeCardViewer = created;
                else
                    BindViewerReference(created);
                v = created;
            }
        }

        ReparentViewerIfUnderInventoryPanel(v);
        return v;
    }

    void ReparentViewerIfUnderInventoryPanel(AchievementCardViewerOverlay viewer)
    {
        if (viewer == null)
            return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        if (viewer.transform.parent == canvas.transform)
            return;
        if (!viewer.transform.IsChildOf(transform))
            return;

        viewer.transform.SetParent(canvas.transform, false);
        viewer.transform.SetAsLastSibling();
#if UNITY_EDITOR
        EditorUtility.SetDirty(viewer.gameObject);
        EditorUtility.SetDirty(canvas.gameObject);
#endif
    }

    AchievementCardViewerOverlay FindViewerOnCanvas()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        for (var i = 0; i < canvas.transform.childCount; i++)
        {
            var v = canvas.transform.GetChild(i).GetComponent<AchievementCardViewerOverlay>();
            if (v != null)
                return v;
        }

        return null;
    }

    AchievementCardViewerOverlay CreateViewerOnCanvas(bool registerUndo)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        var go = new GameObject("AchievementCardViewerOverlay", typeof(RectTransform));
#if UNITY_EDITOR
        if (registerUndo)
            Undo.RegisterCreatedObjectUndo(go, "Create Achievement Card Viewer");
#endif
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        var comp = go.AddComponent<AchievementCardViewerOverlay>();
#if UNITY_EDITOR
        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(canvas.gameObject);
#endif
        return comp;
    }

    void BindViewerReference(AchievementCardViewerOverlay v)
    {
        achievementCardViewer = v;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(this);
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying || achievementCardViewer != null)
            return;
        if (_viewerEnsureScheduled)
            return;
        if (GetComponentInParent<Canvas>() == null)
            return;

        _viewerEnsureScheduled = true;
        EditorApplication.delayCall += EnsureViewerInEditModeDelayed;
    }

    void EnsureViewerInEditModeDelayed()
    {
        _viewerEnsureScheduled = false;
        if (this == null || Application.isPlaying || achievementCardViewer != null)
            return;
        if (GetComponentInParent<Canvas>() == null)
            return;

        var found = FindViewerOnCanvas();
        if (found != null)
        {
            BindViewerReference(found);
            return;
        }

        var v = CreateViewerOnCanvas(registerUndo: true);
        if (v != null)
            BindViewerReference(v);
    }
#endif
}
