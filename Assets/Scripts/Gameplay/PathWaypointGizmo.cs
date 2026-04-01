using UnityEngine;

/// <summary>
/// 挂在路点 Transform 上，在 Scene 里始终绘制线框球，解决纯 Empty「看不见、不好点选」的问题。
/// 不参与位移逻辑，仅编辑辅助；运行时可用其它逻辑禁用本组件。
/// </summary>
[DisallowMultipleComponent]
public class PathWaypointGizmo : MonoBehaviour
{
    [Tooltip("Scene / Game 的 Gizmos 总开关需开启；与 ConstantSpeedPathFollower2D 的青色路径可同时存在")]
    public Color gizmoColor = new Color(1f, 0.5f, 0.05f, 1f);

    [Min(0.05f)]
    public float gizmoRadius = 0.4f;

    [Tooltip("中心再画一小颗半透明实心球，更容易对准")]
    public bool drawFilledCore = true;

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        var p = transform.position;
        Gizmos.DrawWireSphere(p, gizmoRadius);
        if (drawFilledCore)
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.35f);
            Gizmos.DrawSphere(p, gizmoRadius * 0.22f);
        }
    }
}
