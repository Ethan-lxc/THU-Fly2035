using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内自定义鼠标：硬件模式使用 <see cref="Cursor.SetCursor"/>，软件模式使用 UI Image 跟随指针（可平滑）。
/// 软件模式默认使用独立全屏 Overlay Canvas，避免挂在嵌套 HUD Canvas 下因 Scaler/坐标导致不跟手或看不到。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public class GameCursorController : MonoBehaviour
{
    public enum CursorPresentationMode
    {
        Hardware,
        Software
    }

    [Header("外观")]
    [Tooltip("鼠标图标（Texture2D）。与 Cursor Sprite 二选一；Importer 可用 Default，最大边长建议 ≤256（硬件模式平台限制）。")]
    public Texture2D cursorTexture;

    [Tooltip("鼠标图标（Sprite，例如从 PNG 设为 Single 后拖入）。与 Cursor Texture 二选一；若指定则强制使用软件光标（UI 跟随），避免图块/图集在硬件光标下错位。")]
    public Sprite cursorSprite;

    [Tooltip("热点 / 点击锚点：相对贴图左上角的像素偏移（与 Unity Cursor.SetCursor 一致）。软件模式 + Sprite 时可在 Inspector 看 Sprite 的 Pivot；也可用右键组件「将热点设为贴图中心」。")]
    public Vector2 hotspot;

    [Tooltip("硬件光标模式；一般选 Auto")]
    public CursorMode cursorMode = CursorMode.Auto;

    [Header("模式")]
    [Tooltip("Hardware：系统光标替换。Software：UI 图片跟随鼠标，可缩放或规避部分平台硬件光标问题")]
    public CursorPresentationMode presentation = CursorPresentationMode.Hardware;

    [Tooltip("软件模式：为 true 时使用本脚本创建的独立全屏 Overlay（推荐）。为 false 时用 Software Canvas 或场景里第一个 Canvas 作为父级。")]
    [SerializeField] bool useDedicatedOverlayCanvas = true;

    [Tooltip("独立 Overlay 的 sortingOrder，需高于 HUD、全屏淡入遮罩（例如 32000）")]
    [SerializeField] int dedicatedOverlaySortOrder = 32760;

    [Tooltip("软件模式：父级 Canvas；仅在关闭「独立 Overlay」时使用；也可用于仅参考排序")]
    public Canvas softwareCanvas;

    [Tooltip("软件模式：自定义指针 RectTransform；空则在目标 Canvas 下自动创建 Image")]
    public RectTransform cursorGraphic;

    [Tooltip("软件模式：跟随平滑时间（秒），0 表示每帧紧贴鼠标")]
    [Min(0f)]
    public float followSmoothTime = 0.08f;

    [Tooltip("软件模式：是否隐藏系统指针")]
    public bool hideSystemCursor = true;

    [Header("生命周期")]
    [Tooltip("启用组件时是否立即应用自定义指针")]
    public bool applyOnEnable = true;

    [Tooltip("禁用组件时是否恢复系统默认指针")]
    public bool restoreOnDisable = true;

    bool _softwareActive;
    bool _createdRuntimeGraphic;
    Canvas _resolvedCanvas;
    RectTransform _cursorRect;
    Image _cursorImage;

    Vector2 _smoothVelocity;

    Canvas _dedicatedCanvas;
    GameObject _dedicatedRoot;

    void OnEnable()
    {
        if (applyOnEnable)
            ApplyCursor();
    }

    void Start()
    {
        // 排在 GameplayHudLayout 等 Awake/OnEnable 之后，再应用一次，避免首次坐标基于未拉伸的 Rect。
        if (applyOnEnable)
            ApplyCursor();
    }

    void OnDisable()
    {
        if (restoreOnDisable)
            RestoreDefaultCursor();
    }

    void LateUpdate()
    {
        if (!_softwareActive || _cursorRect == null || _resolvedCanvas == null)
            return;

        var root = _resolvedCanvas.transform as RectTransform;
        var cam = _resolvedCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _resolvedCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, Input.mousePosition, cam, out var localPoint))
            return;

        if (followSmoothTime <= 0f)
            _cursorRect.anchoredPosition = localPoint;
        else
            _cursorRect.anchoredPosition = Vector2.SmoothDamp(
                _cursorRect.anchoredPosition,
                localPoint,
                ref _smoothVelocity,
                followSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
    }

    /// <summary>应用当前 Inspector 设置（可在运行时从其它脚本调用）。</summary>
    public void ApplyCursor()
    {
        if (cursorSprite == null && cursorTexture == null)
        {
            Debug.LogWarning("GameCursorController: 请在 Inspector 指定 Cursor Texture 或 Cursor Sprite。");
            return;
        }

        TearDownSoftware();

        if (cursorSprite != null)
        {
            ApplySoftware();
            return;
        }

        if (presentation == CursorPresentationMode.Hardware)
            ApplyHardware();
        else
            ApplySoftware();
    }

    void ApplyHardware()
    {
        Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
        Cursor.visible = true;
        _softwareActive = false;
    }

    void ApplySoftware()
    {
        if (useDedicatedOverlayCanvas)
        {
            EnsureDedicatedOverlayCanvas();
            _resolvedCanvas = _dedicatedCanvas;
        }
        else
        {
            _resolvedCanvas = softwareCanvas != null
                ? softwareCanvas
                : Object.FindObjectOfType<Canvas>();
        }

        if (_resolvedCanvas == null)
        {
            Debug.LogWarning("GameCursorController: 软件模式需要场景中存在 Canvas，或请指定 softwareCanvas。");
            return;
        }

        EnsureSoftwareGraphic();

        if (_cursorRect == null)
            return;

        _cursorRect.SetAsLastSibling();
        _softwareActive = true;
        _smoothVelocity = Vector2.zero;

        if (hideSystemCursor)
            Cursor.visible = false;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    void EnsureDedicatedOverlayCanvas()
    {
        if (_dedicatedCanvas != null)
        {
            _dedicatedRoot.SetActive(true);
            _dedicatedCanvas.sortingOrder = dedicatedOverlaySortOrder;
            return;
        }

        _dedicatedRoot = new GameObject("GameCursorOverlay");
        _dedicatedRoot.transform.SetParent(null);
        var canvas = _dedicatedRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = dedicatedOverlaySortOrder;
        _dedicatedRoot.AddComponent<GraphicRaycaster>();
        var rt = _dedicatedRoot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        _dedicatedCanvas = canvas;
    }

    void EnsureSoftwareGraphic()
    {
        if (cursorGraphic != null)
        {
            _cursorRect = cursorGraphic;
            _cursorRect.gameObject.SetActive(true);
            _cursorImage = _cursorRect.GetComponent<Image>();
            if (_cursorImage == null)
                _cursorImage = _cursorRect.gameObject.AddComponent<Image>();
        }
        else
        {
            var parent = _resolvedCanvas.transform as RectTransform;
            var go = new GameObject("SoftwareCursor", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _cursorRect = go.GetComponent<RectTransform>();
            _cursorImage = go.AddComponent<Image>();
            _createdRuntimeGraphic = true;
        }

        _cursorRect.anchorMin = _cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        _cursorRect.localScale = Vector3.one;

        DestroyRuntimeSpriteIfAny();

        if (cursorSprite != null)
        {
            var r = cursorSprite.rect;
            var pivotNorm = new Vector2(
                cursorSprite.pivot.x / Mathf.Max(1f, r.width),
                cursorSprite.pivot.y / Mathf.Max(1f, r.height));
            _cursorRect.pivot = pivotNorm;
            _cursorRect.sizeDelta = new Vector2(r.width, r.height);
            _cursorImage.sprite = cursorSprite;
        }
        else
        {
            var w = cursorTexture.width;
            var h = cursorTexture.height;
            var pivotNorm = new Vector2(
                hotspot.x / Mathf.Max(1f, w),
                (h - hotspot.y) / Mathf.Max(1f, h));
            _cursorRect.pivot = pivotNorm;
            _cursorRect.sizeDelta = new Vector2(w, h);
            var sprite = Sprite.Create(
                cursorTexture,
                new Rect(0f, 0f, w, h),
                pivotNorm,
                100f);
            sprite.name = "GameCursor_Runtime";
            _cursorImage.sprite = sprite;
        }

        _cursorImage.raycastTarget = false;
        _cursorImage.preserveAspect = true;

        var baseCam = _resolvedCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _resolvedCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _resolvedCanvas.transform as RectTransform,
                Input.mousePosition,
                baseCam,
                out var initialLocal))
            initialLocal = Vector2.zero;

        _cursorRect.anchoredPosition = initialLocal;
    }

    void DestroyRuntimeSpriteIfAny()
    {
        if (_cursorImage == null) return;
        var s = _cursorImage.sprite;
        if (s != null && s.name == "GameCursor_Runtime")
            Destroy(s);
    }

    /// <summary>恢复系统默认指针并清理软件模式创建的对象。</summary>
    public void RestoreDefaultCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
        TearDownSoftware();
    }

    void DestroyDedicatedOverlay()
    {
        if (_dedicatedRoot != null)
        {
            Destroy(_dedicatedRoot);
            _dedicatedRoot = null;
            _dedicatedCanvas = null;
        }
    }

    void TearDownSoftware()
    {
        var wasSoftware = _softwareActive;
        _softwareActive = false;
        _smoothVelocity = Vector2.zero;

        if (_createdRuntimeGraphic && _cursorRect != null)
        {
            if (_cursorImage != null && _cursorImage.sprite != null &&
                _cursorImage.sprite.name == "GameCursor_Runtime")
                Destroy(_cursorImage.sprite);
            Destroy(_cursorRect.gameObject);
            _createdRuntimeGraphic = false;
        }
        else if (wasSoftware && cursorGraphic != null)
            cursorGraphic.gameObject.SetActive(false);

        _cursorRect = null;
        _cursorImage = null;
        _resolvedCanvas = null;
        DestroyDedicatedOverlay();
    }

    void OnDestroy()
    {
        DestroyDedicatedOverlay();
    }

#if UNITY_EDITOR
    [ContextMenu("将热点设为贴图中心")]
    void ContextSetHotspotCenter()
    {
        if (cursorTexture == null) return;
        hotspot = new Vector2(cursorTexture.width * 0.5f, cursorTexture.height * 0.5f);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
