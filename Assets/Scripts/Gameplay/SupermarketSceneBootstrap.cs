using System.Collections;
using UnityEngine;

/// <summary>
/// 仅用于超市场景：为主相机挂上 CameraFollow 并绑定地图边界；为无人机挂上 WorldBoundsClamp2D。
/// <see cref="mapBoundsCollider"/> 限制无人机移动；<see cref="cameraBoundsCollider"/> 可选，用于相机夹紧（通常比前者大一圈，避免贴边时画面顶死）。
/// 多边形时无人机按真实形状夹紧，相机使用对应 Collider 的 bounds（轴对齐外包盒）。
/// </summary>
[DefaultExecutionOrder(-50)]
public class SupermarketSceneBootstrap : MonoBehaviour
{
    [Tooltip("无人机 WorldBoundsClamp2D 使用的边界（多边形可精确贴货架形状）。")]
    [SerializeField] Collider2D mapBoundsCollider;

    [Tooltip("CameraFollow 使用的边界；不填则与 mapBoundsCollider 相同（相机与无人机共用一块区域）。")]
    [SerializeField] Collider2D cameraBoundsCollider;

    [SerializeField] float cameraSmoothTime = 0.3f;

    [SerializeField] Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    IEnumerator Start()
    {
        // 等一帧，便于 DontDestroyOnLoad 的无人机/玩家已进入当前场景。
        yield return null;

        for (var i = 0; i < 90 && FindDroneTransform() == null; i++)
            yield return null;

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[SupermarketSceneBootstrap] 未找到 Camera.main（需 Tag 为 MainCamera）。");
            yield break;
        }

        // 与 GameScene 主相机一致：基准采样 + 每帧恢复正交大小 / TargetTexture，避免被其它逻辑误改。
        if (cam.GetComponent<MainCameraBaselineCapture>() == null)
            cam.gameObject.AddComponent<MainCameraBaselineCapture>();
        if (cam.GetComponent<MainCameraPropertyGuard>() == null)
            cam.gameObject.AddComponent<MainCameraPropertyGuard>();

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        var droneTf = FindDroneTransform();
        if (droneTf != null)
            follow.target = droneTf;

        follow.smoothTime = cameraSmoothTime;
        follow.offset = cameraOffset;
        follow.boundsSource = CameraFollow.BoundsSourceMode.Automatic;

        var followCollider = cameraBoundsCollider != null ? cameraBoundsCollider : mapBoundsCollider;
        if (followCollider != null)
        {
            follow.enableBounds = true;
            follow.boundsCollider = followCollider;
        }
        else
        {
            follow.enableBounds = false;
            follow.boundsCollider = null;
            Debug.LogWarning(
                "[SupermarketSceneBootstrap] mapBoundsCollider / cameraBoundsCollider 均未指定：相机跟随已启用，但无地图边界夹紧。");
        }

        if (droneTf != null && mapBoundsCollider != null)
        {
            var clamp = droneTf.GetComponent<WorldBoundsClamp2D>();
            if (clamp == null)
                clamp = droneTf.gameObject.AddComponent<WorldBoundsClamp2D>();
            clamp.boundsCollider = mapBoundsCollider;
            clamp.InvalidateInteriorCache();
        }
        else if (droneTf == null)
        {
            Debug.LogWarning(
                "[SupermarketSceneBootstrap] 未找到 IsoDroneController：相机无法跟随。若从主场景切来，请将带无人机的物体设为 DontDestroyOnLoad，或在超市场景内放置无人机。");
        }
    }

    /// <summary>与 <see cref="CameraFollowDroneTargetRebind"/> 一致：优先 DDOL 玩家实例。</summary>
    static Transform FindDroneTransform()
    {
        if (PersistentGameplayDroneBootstrap.Instance != null)
            return PersistentGameplayDroneBootstrap.Instance.transform;

        var drone = FindObjectOfType<IsoDroneController>();
        return drone != null ? drone.transform : null;
    }
}
