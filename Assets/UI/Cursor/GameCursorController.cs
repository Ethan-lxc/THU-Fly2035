using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内自定义鼠标：硬件模式使用 <see cref="Cursor.SetCursor"/>，软件模式使用 UI Image 跟随指针（可平滑）。
/// </summary>
[DisallowMultipleComponent]
public class GameCursorController : MonoBehaviour
{
    public enum CursorPresentationMode
    {
        Hardware,
        Software
    }

    [Header("外观")]
    [Tooltip("鼠标贴图；硬件模式需满足平台对光标纹理的要求")]
    public Texture2D cursorTexture;

    [Tooltip("热点：相对贴图左上角的像素偏移（与 Unity Cursor API 一致）")]
    public Vector2 hotspot;

    [Tooltip("硬件光标模式；一般选 Auto")]
    public CursorMode cursorMode = CursorMode.Auto;

    [Header("模式")]
    [Tooltip("Hardware：系统光标替换。Software：UI 图片跟随鼠标，可缩放或规避部分平台硬件光标问题")]
    public CursorPresentationMode presentation = CursorPresentationMode.Hardware;

    [Tooltip("软件模式：使用的 Canvas；空则运行时查找场景中激活的 Canvas")]
    public Canvas softwareCanvas;

    [Tooltip("软件模式：自定义指针 RectTransform；空则在 softwareCanvas 下自动创建 Image")]
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

    void OnEnable()
    {
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
        if (cursorTexture == null)
        {
            Debug.LogWarning("GameCursorController: 未指定 cursorTexture，已跳过。");
            return;
        }

        TearDownSoftware();

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
        _resolvedCanvas = softwareCanvas != null
            ? softwareCanvas
            : Object.FindObjectOfType<Canvas>();

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

        var w = cursorTexture.width;
        var h = cursorTexture.height;
        var pivot = new Vector2(
            hotspot.x / Mathf.Max(1f, w),
            (h - hotspot.y) / Mathf.Max(1f, h));

        _cursorRect.anchorMin = _cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        _cursorRect.pivot = pivot;
        _cursorRect.sizeDelta = new Vector2(w, h);
        _cursorRect.localScale = Vector3.one;

        if (_cursorImage.sprite != null)
            Destroy(_cursorImage.sprite);

        var sprite = Sprite.Create(
            cursorTexture,
            new Rect(0f, 0f, w, h),
            pivot,
            100f);
        sprite.name = "GameCursor_Runtime";

        _cursorImage.sprite = sprite;
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

    /// <summary>恢复系统默认指针并清理软件模式创建的对象。</summary>
    public void RestoreDefaultCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
        TearDownSoftware();
    }

    void TearDownSoftware()
    {
        var wasSoftware = _softwareActive;
        _softwareActive = false;
        _smoothVelocity = Vector2.zero;

        if (_createdRuntimeGraphic && _cursorRect != null)
        {
            if (_cursorImage != null && _cursorImage.sprite != null)
                Destroy(_cursorImage.sprite);
            Destroy(_cursorRect.gameObject);
            _createdRuntimeGraphic = false;
        }
        else if (wasSoftware && cursorGraphic != null)
            cursorGraphic.gameObject.SetActive(false);

        _cursorRect = null;
        _cursorImage = null;
        _resolvedCanvas = null;
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
