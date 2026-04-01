using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 右中：成就按钮切换成就面板；4×4 固定槽位，入卡顺序从左到右、从上到下。
/// </summary>
public class AchievementPanelController : MonoBehaviour
{
    public const int GridColumns = 4;
    public const int GridRows = 4;
    public const int SlotCount = GridColumns * GridRows;

    [Header("引用")]
    public Button toggleButton;

    [Tooltip("可选；打开成就前关闭暂停/背包。空则打开时 FindObjectOfType")]
    public GameplayHudLayout gameplayHudLayout;

    [Tooltip("成就面板根（建议带 CanvasGroup）")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("无 CanvasGroup 时可用普通 GameObject，仅用 SetActive 显隐")]
    public GameObject panelFallbackRoot;

    [Tooltip("可选；若层级里仍有 Scroll View，仅用于定位 Content，滚动会在运行时关闭")]
    public ScrollRect scrollRect;

    [Tooltip("卡片父节点；空则使用 ScrollRect.content")]
    public RectTransform contentRoot;

    [Tooltip("成就卡欣赏层（半透明罩 + 大图）。编辑模式下若为空，会在保存/选中时自动挂到同 Canvas 下，可直接调 Preview Max Size 等；也可手动拖入")]
    public AchievementCardViewerOverlay achievementCardViewer;

    [Header("网格")]
    public Vector2 cellSize = new Vector2(78f, 96f);
    public Vector2 cellSpacing = new Vector2(6f, 6f);

    [Tooltip("相对 Content 根的四边留白；想更贴边可改小")]
    public RectOffset gridPadding = new RectOffset(4, 4, 4, 4);

    [Tooltip("整块 4×4 网格在 Content 区域内的对齐（占不满时尤明显）")]
    public TextAnchor gridChildAlignment = TextAnchor.UpperLeft;

    [Header("外观（可选图片）")]
    [Tooltip("成就台整块面板背景；指定则赋给本物体或 panelBackgroundImage 上的 Image")]
    public Sprite panelBackgroundSprite;

    [Tooltip("与背景图相乘的颜色；无图时仅作纯色底")]
    public Color panelBackgroundTint = Color.white;

    [Tooltip("空则尝试用本物体或子物体上的 Image 作为面板底图")]
    public Image panelBackgroundImage;

    [Header("空槽占位")]
    public Color emptySlotBackground = new Color(0.15f, 0.15f, 0.18f, 0.85f);

    [Tooltip("空槽底图；不指定则仅用 Empty Slot Background 纯色")]
    public Sprite emptySlotSprite;

    [Header("成就卡")]
    public GameObject achievementCardPrefab;

    [Header("行为")]
    public bool startClosed = true;

    [Tooltip("入库后自动打开成就台，便于立刻看到卡（关闭时 CanvasGroup alpha=0 会隐藏整张面板含新卡）")]
    public bool openPanelWhenCardAdded = true;

    bool _open;
    AchievementGridSlot[] _slots;
    AchievementCardViewerOverlay _runtimeCardViewer;

#if UNITY_EDITOR
    bool _viewerEnsureScheduled;
#endif

    public bool IsOpen => _open;

    void Awake()
    {
        AutoResolveReferences();
        ConfigureScrollRectForStaticGrid();
        ApplyPanelBackground();
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
        ResolveContentRoot();
        EnsureGridLayout();
        BuildGridSlots();
        SetOpen(!startClosed);
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

    /// <summary>固定 4×4 网格一屏显示，关闭拖拽/惯性滚动并隐藏滚动条。</summary>
    void ConfigureScrollRectForStaticGrid()
    {
        if (scrollRect == null)
            return;

        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.elasticity = 0f;
        scrollRect.inertia = false;

        if (scrollRect.horizontalScrollbar != null)
            scrollRect.horizontalScrollbar.gameObject.SetActive(false);
        if (scrollRect.verticalScrollbar != null)
            scrollRect.verticalScrollbar.gameObject.SetActive(false);

        if (scrollRect.viewport != null)
        {
            var vp = scrollRect.viewport;
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.pivot = new Vector2(0f, 1f);
            vp.offsetMin = Vector2.zero;
            vp.offsetMax = Vector2.zero;
        }

        var scrollRt = scrollRect.transform as RectTransform;
        if (scrollRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRt);
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

    void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(Toggle);
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
        grid.constraintCount = GridColumns;
        grid.childAlignment = gridChildAlignment;
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

        while (contentRoot.childCount > 0)
            DestroyImmediate(contentRoot.GetChild(0).gameObject);

        _slots = new AchievementGridSlot[SlotCount];

        for (var i = 0; i < SlotCount; i++)
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

    public void Toggle()
    {
        SetOpen(!_open);
    }

    public void SetOpen(bool open)
    {
        if (open)
        {
            var hud = gameplayHudLayout != null ? gameplayHudLayout : FindObjectOfType<GameplayHudLayout>();
            hud?.CloseOtherPanelsBeforeAchievement();
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

    /// <summary>将一张卡放入首个空槽（从左到右、从上到下）。槽满则忽略并打日志。</summary>
    public void AddAchievementCard(Sprite icon, string title = "")
    {
        ResolveContentRoot();
        if (contentRoot == null || _slots == null)
        {
            Debug.LogWarning(
                $"{nameof(AchievementPanelController)}: 无法入库，请检查 Content Root 是否在 Awake 时已解析（当前 contentRoot missing 或网格未生成）。");
            return;
        }

        AchievementGridSlot target = null;
        foreach (var s in _slots)
        {
            if (s != null && !s.IsOccupied)
            {
                target = s;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"{nameof(AchievementPanelController)}: 成就槽已满（{SlotCount}），无法再放入新卡。");
            return;
        }

        var go = InstantiateAchievementCardUi(target.cardMount, icon, title);

        var rt = go.transform as RectTransform;
        if (rt != null)
            StretchFull(rt);

        ApplyIconAndTitleToCard(go, icon, title);
        var hitTarget = PickAchievementCardHitTarget(go, icon);
        var zoom = hitTarget.GetComponent<AchievementCardClickZoom>()
                   ?? hitTarget.AddComponent<AchievementCardClickZoom>();
        zoom.Configure(this, icon, title);

        if (openPanelWhenCardAdded)
            SetOpen(true);
    }

    /// <summary>必须使用 UI：根为 RectTransform 且含 Image（任意子级）。世界空间 SpriteRenderer Prefab 会退回运行时 UI 卡。</summary>
    GameObject InstantiateAchievementCardUi(Transform mount, Sprite icon, string title)
    {
        if (achievementCardPrefab != null &&
            achievementCardPrefab.transform is RectTransform &&
            achievementCardPrefab.GetComponentInChildren<Image>(true) != null)
            return Instantiate(achievementCardPrefab, mount);

        if (achievementCardPrefab != null)
        {
            var hasSr = achievementCardPrefab.GetComponentInChildren<SpriteRenderer>(true) != null;
            Debug.LogWarning(
                $"{nameof(AchievementPanelController)}: 成就卡 Prefab「{achievementCardPrefab.name}」不是 UI 卡（需要根节点为 RectTransform 且含 Image）。已改用运行时 UI 卡；请在 Prefab 中改为 Canvas/UI Image，或清空 Prefab 字段。" +
                (hasSr ? "（检测到 SpriteRenderer：该资源适用于场景/2D，不适用于 UI 槽位）" : string.Empty));
        }

        return CreateRuntimeAchievementCard(mount, icon, title);
    }

    static GameObject CreateRuntimeAchievementCard(Transform mount, Sprite icon, string title)
    {
        var cardRoot = new GameObject("AchievementCard_Runtime", typeof(RectTransform), typeof(Image));
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

    /// <summary>点击欣赏用的物体：与入库 icon 同 Sprite 的 Image；否则用面积最大的有效图，避免点到白底板。</summary>
    static GameObject PickAchievementCardHitTarget(GameObject cardRoot, Sprite icon)
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

        if (best != null)
            return best.gameObject;

        return cardRoot;
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

    /// <summary>全屏半透明遮罩 + 居中放大预览；点击遮罩或大图关闭。</summary>
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
                    BindAchievementViewerReference(found);
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
                    BindAchievementViewerReference(created);
                v = created;
            }
        }

        ReparentAchievementViewerIfUnderAchievementPanel(v);
        return v;
    }

    /// <summary>欣赏层若在成就面板子级，会受面板 CanvasGroup.alpha 影响整层透明；挂到同 Canvas 根下。</summary>
    void ReparentAchievementViewerIfUnderAchievementPanel(AchievementCardViewerOverlay viewer)
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

    /// <summary>在同 Canvas 下查找已存在的欣赏层（直接子物体上的组件）。</summary>
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

    void BindAchievementViewerReference(AchievementCardViewerOverlay v)
    {
        achievementCardViewer = v;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(this);
#endif
    }

#if UNITY_EDITOR
    /// <summary>编辑模式：若无引用则在 Canvas 下生成欣赏层并序列化，便于退出 Play 后仍能调和保存。</summary>
    void OnValidate()
    {
        if (Application.isPlaying || achievementCardViewer != null)
            return;
        if (_viewerEnsureScheduled)
            return;
        if (GetComponentInParent<Canvas>() == null)
            return;

        _viewerEnsureScheduled = true;
        EditorApplication.delayCall += EnsureAchievementViewerInEditModeDelayed;
    }

    void EnsureAchievementViewerInEditModeDelayed()
    {
        _viewerEnsureScheduled = false;
        if (this == null || Application.isPlaying || achievementCardViewer != null)
            return;
        if (GetComponentInParent<Canvas>() == null)
            return;

        var found = FindViewerOnCanvas();
        if (found != null)
        {
            BindAchievementViewerReference(found);
            return;
        }

        var v = CreateViewerOnCanvas(registerUndo: true);
        if (v != null)
            BindAchievementViewerReference(v);
    }
#endif
}
