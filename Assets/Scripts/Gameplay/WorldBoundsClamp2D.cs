using UnityEngine;

/// <summary>
/// 将物体 XY 限制在 Collider2D 内。支持 BoxCollider2D（带 padding）以及 PolygonCollider2D / CompositeCollider2D 等任意形状。
/// 多边形：用 OverlapPoint 判定在内/外；在外时用与「内部采样点」之间的二分，把位置拉回多边形内（非轴对齐盒）。
/// 相机 <see cref="CameraFollow"/> 仍使用 collider.bounds 的轴对齐盒做保守夹紧；若需相机也沿多边形边缘收紧，可另挂仅用于相机的边界或用手动 Bounds。
/// </summary>
public class WorldBoundsClamp2D : MonoBehaviour
{
    public Collider2D boundsCollider;

    [Tooltip("仅对 BoxCollider2D 生效：相对矩形边界内缩。多边形请直接在编辑器里把顶点内移，或留 0。")]
    [SerializeField] float padding = 0.35f;

    Vector2 _interiorPoint;
    bool _hasInteriorPoint;

    void OnEnable()
    {
        InvalidateInteriorCache();
    }

    void OnValidate()
    {
        InvalidateInteriorCache();
    }

    public void InvalidateInteriorCache()
    {
        _hasInteriorPoint = false;
    }

    void LateUpdate()
    {
        if (boundsCollider == null)
            return;

        var p = (Vector2)transform.position;
        var z = transform.position.z;

        if (boundsCollider is BoxCollider2D && padding > 0.0001f)
        {
            var b = boundsCollider.bounds;
            p.x = Mathf.Clamp(p.x, b.min.x + padding, b.max.x - padding);
            p.y = Mathf.Clamp(p.y, b.min.y + padding, b.max.y - padding);
            transform.position = new Vector3(p.x, p.y, z);
            return;
        }

        EnsureInteriorSample();
        if (!_hasInteriorPoint)
            return;

        if (boundsCollider.OverlapPoint(p))
        {
            transform.position = new Vector3(p.x, p.y, z);
            return;
        }

        // 当前在外：沿 p → 多边形内部一点二分，落到边界内侧
        var lo = p;
        var hi = _interiorPoint;
        if (!boundsCollider.OverlapPoint(hi))
            return;

        for (var i = 0; i < 16; i++)
        {
            var mid = (lo + hi) * 0.5f;
            if (boundsCollider.OverlapPoint(mid))
                hi = mid;
            else
                lo = mid;
        }

        p = hi;
        transform.position = new Vector3(p.x, p.y, z);
    }

    void EnsureInteriorSample()
    {
        if (_hasInteriorPoint)
            return;

        _interiorPoint = SampleInteriorPoint(boundsCollider);
        _hasInteriorPoint = boundsCollider.OverlapPoint(_interiorPoint);
    }

    /// <summary>在 collider 世界 AABB 内网格采样，找到第一个 OverlapPoint 为真的点作为「向内」投影的终点参考。</summary>
    public static Vector2 SampleInteriorPoint(Collider2D c)
    {
        if (c == null)
            return Vector2.zero;

        var b = c.bounds;
        var center = (Vector2)b.center;
        if (c.OverlapPoint(center))
            return center;

        const int n = 16;
        for (var ix = 0; ix <= n; ix++)
        {
            var tx = ix / (float)n;
            for (var iy = 0; iy <= n; iy++)
            {
                var ty = iy / (float)n;
                var p = new Vector2(
                    Mathf.Lerp(b.min.x, b.max.x, tx),
                    Mathf.Lerp(b.min.y, b.max.y, ty));
                if (c.OverlapPoint(p))
                    return p;
            }
        }

        return center;
    }
}
