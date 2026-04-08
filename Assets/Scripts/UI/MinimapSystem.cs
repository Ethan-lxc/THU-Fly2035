using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 小地图：副摄像机渲染到 <see cref="RenderTexture"/>，由 <see cref="RawImage"/> 显示。
/// 适用于 URP；可在运行时自创建 RT 与摄像机（也可在 Inspector 指定）。
/// </summary>
[DisallowMultipleComponent]
public class MinimapSystem : MonoBehaviour
{
    public enum ProjectionMode
    {
        [Tooltip("与主摄像机相同朝向，沿其 forward 反方向拉远（适合 2D XY 或沿用主相机视向）")]
        MatchMainCamera,

        [Tooltip("世界 +Y 高处俯视 XZ（适合 3D 顶视小地图）")]
        TopDownWorldY
    }

    [Header("跟随")]
    [Tooltip("小地图中心跟随的目标（一般为玩家）")]
    public Transform followTarget;

    [Tooltip("未指定时尝试查找 Tag=Player 的物体")]
    public bool tryAutoFindPlayer = true;

    [Header("相机")]
    [Tooltip("空则在本物体下创建子物体 MinimapCamera")]
    public Camera minimapCamera;

    public ProjectionMode projectionMode = ProjectionMode.MatchMainCamera;

    [Min(0.5f)] public float orthographicSize = 12f;

    [Tooltip("MatchMainCamera：沿主相机 forward 的反方向拉开的距离；TopDownWorldY：相对目标的高度偏移")]
    [Min(0.1f)] public float cameraDistanceOrHeight = 18f;

    [Tooltip("影响副相机能看到哪些层；建议排除 UI")]
    public LayerMask minimapCullingMask = ~(1 << 5);

    public Color backgroundColor = new Color(0.06f, 0.09f, 0.14f, 1f);

    [Header("RenderTexture")]
    [Min(32)] public int renderTextureWidth = 256;

    [Min(32)] public int renderTextureHeight = 256;

    [Tooltip("若为空，Awake 时创建运行时 RT")]
    public RenderTexture minimapRenderTexture;

    [Header("UI")]
    [Tooltip("把你做好的小地图框里的 RawImage 拖到这里；留空则尝试在子级中查找")]
    public RawImage minimapRawImage;

    [Header("更新")]
    public bool updateInLateUpdate = true;

    RenderTexture _runtimeOwnedTexture;
    Camera _cachedMainCamera;
    bool _destroyedCameraWithThis;

    void Awake()
    {
        _cachedMainCamera = Camera.main;

        if (tryAutoFindPlayer && followTarget == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                followTarget = go.transform;
        }

        EnsureRenderTexture();
        EnsureMinimapCameraAndUrp();
        WireRawImage();
        ApplyStaticCameraSettings();
    }

    void Start()
    {
        // 便于你在其他脚本里稍晚再指定 RawImage，或与 Prefab 实例化顺序配合
        WireRawImage();
    }

    /// <summary>运行时改绑显示目标（一般在 Awake 之后调用）。</summary>
    public void SetDisplayRawImage(RawImage raw)
    {
        minimapRawImage = raw;
        WireRawImage();
    }

    void EnsureRenderTexture()
    {
        if (minimapRenderTexture != null)
            return;

        var w = Mathf.Max(32, renderTextureWidth);
        var h = Mathf.Max(32, renderTextureHeight);
        _runtimeOwnedTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
        {
            name = "MinimapRT_Runtime",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _runtimeOwnedTexture.Create();
        minimapRenderTexture = _runtimeOwnedTexture;
    }

    void EnsureMinimapCameraAndUrp()
    {
        RejectMainCameraAsMinimap();

        if (minimapCamera == null)
        {
            var camGo = new GameObject("MinimapCamera");
            camGo.transform.SetParent(transform, false);
            minimapCamera = camGo.AddComponent<Camera>();
            _destroyedCameraWithThis = true;

            var listener = camGo.GetComponent<AudioListener>();
            if (listener != null)
                Destroy(listener);
        }

        if (minimapCamera.GetComponent<UniversalAdditionalCameraData>() == null)
            minimapCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthographicSize;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = backgroundColor;
        minimapCamera.cullingMask = minimapCullingMask;
        minimapCamera.depth = -10f;
        minimapCamera.targetTexture = minimapRenderTexture;
        minimapCamera.allowHDR = false;
        minimapCamera.allowMSAA = false;
        minimapCamera.useOcclusionCulling = false;

        var urp = minimapCamera.GetUniversalAdditionalCameraData();
        if (urp != null)
            urp.renderType = CameraRenderType.Base;
    }

    void RejectMainCameraAsMinimap()
    {
        if (minimapCamera == null)
            return;

        var main = Camera.main;
        var isMain =
            (main != null && minimapCamera == main) ||
            minimapCamera.gameObject.CompareTag("MainCamera");

        if (isMain)
        {
            Debug.LogWarning(
                "MinimapSystem：Minimap Camera 不能是主相机/带 MainCamera 标签的相机（会把主画面渲到 RT 或篡改主相机参数）。已清空引用并改用子物体副相机。",
                this);
            minimapCamera = null;
        }
    }

    void WireRawImage()
    {
        if (minimapRawImage == null)
            minimapRawImage = GetComponentInChildren<RawImage>(true);

        if (minimapRawImage != null)
            minimapRawImage.texture = minimapRenderTexture;
    }

    void ApplyStaticCameraSettings()
    {
        if (minimapCamera == null)
            return;

        minimapCamera.orthographicSize = orthographicSize;
        minimapCamera.cullingMask = minimapCullingMask;
        minimapCamera.backgroundColor = backgroundColor;
    }

    void LateUpdate()
    {
        if (!updateInLateUpdate)
            return;

        if (followTarget == null || minimapCamera == null)
            return;

        SyncCameraTransform();
    }

    void SyncCameraTransform()
    {
        var t = followTarget.position;

        if (projectionMode == ProjectionMode.TopDownWorldY)
        {
            minimapCamera.transform.position = t + Vector3.up * cameraDistanceOrHeight;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return;
        }

        if (_cachedMainCamera != null)
        {
            var back = -_cachedMainCamera.transform.forward;
            minimapCamera.transform.position = t + back * cameraDistanceOrHeight;
            minimapCamera.transform.rotation = _cachedMainCamera.transform.rotation;
        }
        else
        {
            minimapCamera.transform.position = t + new Vector3(0f, 0f, -cameraDistanceOrHeight);
            minimapCamera.transform.rotation = Quaternion.identity;
        }
    }

    void OnValidate()
    {
        RejectMainCameraAsMinimap();

        if (minimapCamera != null && minimapRenderTexture != null)
            minimapCamera.targetTexture = minimapRenderTexture;

        if (minimapCamera != null && Application.isPlaying)
            ApplyStaticCameraSettings();
    }

    void OnDestroy()
    {
        if (_runtimeOwnedTexture != null)
        {
            _runtimeOwnedTexture.Release();
            Destroy(_runtimeOwnedTexture);
            _runtimeOwnedTexture = null;
        }

        if (_destroyedCameraWithThis && minimapCamera != null)
        {
            Destroy(minimapCamera.gameObject);
            minimapCamera = null;
        }
    }
}
