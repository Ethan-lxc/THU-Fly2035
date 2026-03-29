using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 正交相机跟随目标；可将相机中心限制在「地图」世界边界内。
/// 先 SmoothDamp 追「未夹紧」的理想点，再夹紧相机位置；若某轴被边界截断则清零该轴速度，
/// 避免 SmoothDamp 在边界上累积速度导致回程时相机不跟。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public enum BoundsSourceMode
    {
        [Tooltip("有 Tilemap 用 Tilemap，否则 Collider，否则 Manual")]
        Automatic = 0,
        [Tooltip("仅使用 Bounds Collider")]
        ColliderOnly = 1,
        [Tooltip("仅使用 Bounds Tilemap")]
        TilemapOnly = 2,
        [Tooltip("仅使用 Manual World Bounds")]
        ManualOnly = 3
    }

    [Header("跟随对象")]
    public Transform target;

    [Header("平滑设置")]
    public float smoothTime = 0.3f;
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("地图边界（世界坐标）")]
    [Tooltip("关闭则不夹紧相机（便于调试）")]
    public bool enableBounds = true;

    [Tooltip("与你设置的边界一致：例如只拖了碰撞体可选 Collider Only")]
    public BoundsSourceMode boundsSource = BoundsSourceMode.Automatic;

    [Tooltip("场景中的 Tilemap（Renderer 包围盒）")]
    public Tilemap boundsTilemap;

    [Tooltip("任意 Collider2D 的 bounds")]
    public Collider2D boundsCollider;

    [Tooltip("手动 Bounds（中心 + 尺寸，世界空间，尺寸需大于 0）")]
    public Bounds manualWorldBounds = new Bounds(Vector3.zero, Vector3.zero);

    private Vector3 currentVelocity = Vector3.zero;
    private Camera _camera;
    private bool _warnedNoBounds;

    void LateUpdate()
    {
        if (target == null) return;

        // 理想跟随点始终为玩家 + 偏移（不先夹紧），保证阻尼目标与玩家同步
        Vector3 desired = target.position + offset;

        Vector3 next = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref currentVelocity,
            smoothTime
        );

        if (enableBounds && TryGetCameraBounds(out Bounds worldBounds))
        {
            Vector3 beforeClamp = next;
            next = ClampCameraPosition(next, worldBounds);

            // 被边界截断的轴上清零速度，防止 velocity 一直顶着边界，往回走时无法跟上
            const float eps = 0.0001f;
            if (Mathf.Abs(next.x - beforeClamp.x) > eps)
                currentVelocity.x = 0f;
            if (Mathf.Abs(next.y - beforeClamp.y) > eps)
                currentVelocity.y = 0f;
        }

        transform.position = next;
    }

    bool TryGetCameraBounds(out Bounds worldBounds)
    {
        worldBounds = default;

        switch (boundsSource)
        {
            case BoundsSourceMode.ColliderOnly:
                return TryCollider(out worldBounds) || TryManual(out worldBounds);
            case BoundsSourceMode.TilemapOnly:
                return TryTilemap(out worldBounds) || TryManual(out worldBounds);
            case BoundsSourceMode.ManualOnly:
                return TryManual(out worldBounds);
            default:
                if (TryTilemap(out worldBounds)) return true;
                if (TryCollider(out worldBounds)) return true;
                return TryManual(out worldBounds);
        }
    }

    bool TryTilemap(out Bounds worldBounds)
    {
        worldBounds = default;
        if (boundsTilemap == null) return false;

        var renderer = boundsTilemap.GetComponent<Renderer>();
        if (renderer != null)
        {
            worldBounds = renderer.bounds;
            return true;
        }

        boundsTilemap.CompressBounds();
        BoundsInt cellBounds = boundsTilemap.cellBounds;
        if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            return false;

        Vector3 min = boundsTilemap.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
        Vector3 max = boundsTilemap.CellToWorld(new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0));
        worldBounds.SetMinMax(
            new Vector3(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), min.z),
            new Vector3(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y), max.z));
        return true;
    }

    bool TryCollider(out Bounds worldBounds)
    {
        worldBounds = default;
        if (boundsCollider == null) return false;
        worldBounds = boundsCollider.bounds;
        return true;
    }

    bool TryManual(out Bounds worldBounds)
    {
        worldBounds = manualWorldBounds;
        if (manualWorldBounds.size.x > 0.0001f && manualWorldBounds.size.y > 0.0001f)
            return true;

        if (!_warnedNoBounds && enableBounds)
        {
            Debug.LogWarning(
                "[CameraFollow] 未找到有效边界。请设置 Bounds Tilemap / Bounds Collider / Manual World Bounds，或关闭 Enable Bounds。",
                this);
            _warnedNoBounds = true;
        }

        return false;
    }

    /// <summary>
    /// 将相机中心位置限制在地图 bounds 内（正交：减去半屏宽高）。
    /// </summary>
    Vector3 ClampCameraPosition(Vector3 cameraPos, Bounds b)
    {
        Camera cam = _camera != null ? _camera : (_camera = GetComponent<Camera>());
        if (cam == null || !cam.orthographic)
            return cameraPos;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;

        if (minX > maxX)
            cameraPos.x = (b.min.x + b.max.x) * 0.5f;
        else
            cameraPos.x = Mathf.Clamp(cameraPos.x, minX, maxX);

        if (minY > maxY)
            cameraPos.y = (b.min.y + b.max.y) * 0.5f;
        else
            cameraPos.y = Mathf.Clamp(cameraPos.y, minY, maxY);

        return cameraPos;
    }
}