using UnityEngine;

/// <summary>
/// 挂在主相机上：在该相机每次渲染前恢复采样到的参数，防止被其它脚本误改。
/// 小地图副相机不要挂本组件与 <see cref="MainCameraBaselineCapture"/>。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(MainCameraBaselineCapture))]
public sealed class MainCameraPropertyGuard : MonoBehaviour
{
    [Tooltip("为 true 时，每帧在该相机渲染前恢复采样到的 Orthographic Size")]
    public bool lockOrthographicSize = true;

    [Tooltip("为 true 时，强制 Target Texture 与进入 Play 时一致（通常为 None）")]
    public bool lockTargetTexture = true;

    Camera _cam;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        Camera.onPreCull += OnAnyCameraPreCull;
    }

    void OnDisable()
    {
        Camera.onPreCull -= OnAnyCameraPreCull;
    }

    void OnAnyCameraPreCull(Camera cam)
    {
        if (cam != _cam) return;
        MainCameraPropertyGuardRegistry.Restore(_cam, lockOrthographicSize, lockTargetTexture);
    }
}
