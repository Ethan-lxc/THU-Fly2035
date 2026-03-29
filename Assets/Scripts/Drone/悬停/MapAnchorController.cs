using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 与 <see cref="IsoDroneController"/> 配合：在指定 <see cref="Canvas"/> 下生成 UI 锚点（世界坐标对应屏幕位置），
/// 便于用 Canvas 的 Sort Order / Hierarchy 控制盖住地图与其它 UI；无人机抵达后移除。
/// </summary>
[DisallowMultipleComponent]
public class MapAnchorController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("空则在本物体或场景中查找 IsoDroneController")]
    [SerializeField] IsoDroneController drone;

    [Header("UI — Canvas（必指定其一）")]
    [Tooltip("锚点生成在该 Canvas 下。空则运行时 FindObjectOfType<Canvas>()")]
    public Canvas anchorCanvas;

    [Tooltip("锚点的父节点；空则挂在 Canvas 根 RectTransform 下")]
    public RectTransform anchorParent;

    [Tooltip("将世界坐标转为屏幕坐标时使用；空则 Camera.main")]
    public Camera worldCamera;

    [Header("锚点外观")]
    [Tooltip("优先：根节点带 RectTransform 的 UI 预制体（Image/Animator 等）")]
    public GameObject anchorPrefab;

    [Tooltip("无预制体时：运行时创建带 Image 的 UI")]
    public Sprite anchorSprite;

    [Header("锚点尺寸")]
    [Tooltip("锚点根 RectTransform 的 localScale 均匀缩放")]
    [Min(0.01f)]
    public float anchorUniformScale = 0.35f;

    [Header("UI — 显示在最前")]
    [Tooltip("生成后 SetAsLastSibling，在同父物体子级中最后绘制")]
    public bool bringToFrontOfSiblings = true;

    [Tooltip("在锚点根上添加独立 Canvas 并 Override Sorting，用「Sort Order」盖住同场景其它 UI；关闭则完全依赖父 Canvas 顺序")]
    public bool useOverlayCanvasOnAnchor = true;

    [Tooltip("仅当 useOverlayCanvasOnAnchor：子 Canvas 的 Sorting Order（越大越靠前）")]
    [Range(-32768, 32767)]
    public int overlayCanvasSortingOrder = 4000;

    [Header("动画（预制体上有 Animator 时）")]
    [Tooltip("非空则 Animator.Play(该状态名)；留空则播放默认状态")]
    public string animatorPlayState = "";

    [Header("音效")]
    public AudioClip placeSound;

    [Tooltip("可选：将其 Output Audio Mixer Group 赋给临时播放用的 AudioSource；不再用本组件的 PlayOneShot")]
    public AudioSource audioSource;

    [Tooltip("音量滑块 0～10：0 静音，10 最大（线性）。用临时 AudioSource 播放，避免 PlayOneShot 与父 AudioSource.volume=0 相乘导致无声")]
    [Range(0f, 10f)]
    public float placeSoundVolume = 10f;

    GameObject _currentAnchor;
    RectTransform _anchorRect;
    Vector3 _anchorWorldPos;

    void Awake()
    {
        if (drone == null)
            drone = GetComponent<IsoDroneController>();
        if (drone == null)
            drone = FindObjectOfType<IsoDroneController>();

        if (anchorCanvas == null)
            anchorCanvas = FindObjectOfType<Canvas>();
    }

    void OnEnable()
    {
        if (drone == null) return;
        drone.onDestinationSet.AddListener(OnDestinationSet);
        drone.onArrivedAtDestination.AddListener(OnArrivedAtDestination);
    }

    void OnDisable()
    {
        if (drone == null) return;
        drone.onDestinationSet.RemoveListener(OnDestinationSet);
        drone.onArrivedAtDestination.RemoveListener(OnArrivedAtDestination);
    }

    void LateUpdate()
    {
        if (_anchorRect == null || anchorCanvas == null)
            return;
        SyncAnchorUiToWorld();
    }

    void OnDestinationSet(Vector2 worldPos)
    {
        ClearAnchorInstance();

        if (anchorCanvas == null)
        {
            Debug.LogWarning("MapAnchorController: 请指定 anchorCanvas，或保证场景里至少有一个 Canvas。");
            return;
        }

        var z = drone != null ? drone.transform.position.z : 0f;
        _anchorWorldPos = new Vector3(worldPos.x, worldPos.y, z);

        var parent = anchorParent != null ? anchorParent : anchorCanvas.transform as RectTransform;

        if (anchorPrefab != null)
        {
            var rt = anchorPrefab.GetComponent<RectTransform>();
            if (rt == null)
            {
                Debug.LogWarning("MapAnchorController: anchorPrefab 根节点需要 RectTransform（UI 预制体）。");
                return;
            }

            _currentAnchor = Instantiate(anchorPrefab, parent);
            _anchorRect = _currentAnchor.GetComponent<RectTransform>();
            if (_anchorRect != null)
                _anchorRect.localScale = Vector3.one * Mathf.Max(0.01f, anchorUniformScale);
            ApplyOverlayCanvasIfNeeded(_currentAnchor);
            TryPlayAnimator(_currentAnchor);
        }
        else if (anchorSprite != null)
        {
            _currentAnchor = CreateUiImageAnchor(parent);
            _anchorRect = _currentAnchor.GetComponent<RectTransform>();
            ApplyOverlayCanvasIfNeeded(_currentAnchor);
        }
        else
        {
            Debug.LogWarning("MapAnchorController: 请指定 anchorPrefab 或 anchorSprite。");
            return;
        }

        if (bringToFrontOfSiblings && _currentAnchor != null)
            _currentAnchor.transform.SetAsLastSibling();

        SyncAnchorUiToWorld();
        PlayPlaceSound(_anchorWorldPos);
    }

    void OnArrivedAtDestination()
    {
        ClearAnchorInstance();
    }

    void SyncAnchorUiToWorld()
    {
        if (_anchorRect == null) return;

        var cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return;

        var parent = _anchorRect.parent as RectTransform;
        if (parent == null) return;

        var screen = cam.WorldToScreenPoint(_anchorWorldPos);
        var uiCam = anchorCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : anchorCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out var local))
            return;

        _anchorRect.anchoredPosition = local;
    }

    GameObject CreateUiImageAnchor(RectTransform parent)
    {
        var go = new GameObject("MapAnchorUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one * Mathf.Max(0.01f, anchorUniformScale);
        var img = go.AddComponent<Image>();
        img.sprite = anchorSprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        rt.sizeDelta = new Vector2(anchorSprite.rect.width, anchorSprite.rect.height);
        return go;
    }

    void ApplyOverlayCanvasIfNeeded(GameObject root)
    {
        if (!useOverlayCanvasOnAnchor || root == null) return;
        var cv = root.GetComponent<Canvas>();
        if (cv == null)
            cv = root.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = overlayCanvasSortingOrder;
    }

    void TryPlayAnimator(GameObject root)
    {
        if (string.IsNullOrEmpty(animatorPlayState)) return;
        var anim = root.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.Play(animatorPlayState, 0, 0f);
    }

    void PlayPlaceSound(Vector3 worldPos)
    {
        if (placeSound == null) return;

        // 滑块 0～10 → AudioSource.volume 0～1（Unity 单路音量上限为 1）
        var volume01 = Mathf.Clamp01(placeSoundVolume / 10f);
        if (volume01 <= 0f) return;

        // 不用 PlayOneShot：其 volumeScale 在部分版本里被限制在 0～1，且会乘上引用 AudioSource 的 volume（为 0 时完全无声）
        var go = new GameObject("MapAnchor_PlaceSound");
        go.transform.position = worldPos;
        var src = go.AddComponent<AudioSource>();
        src.clip = placeSound;
        src.volume = volume01;
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        src.loop = false;
        if (audioSource != null)
            src.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        src.Play();
        Destroy(go, placeSound.length + 0.1f);
    }

    void ClearAnchorInstance()
    {
        if (_currentAnchor == null) return;
        Destroy(_currentAnchor);
        _currentAnchor = null;
        _anchorRect = null;
    }

    void OnDestroy()
    {
        ClearAnchorInstance();
    }
}
