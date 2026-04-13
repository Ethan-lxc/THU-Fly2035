using UnityEngine;

/// <summary>
/// 主场景相机上的 <see cref="CameraFollow"/> 序列化 target 在「持久化玩家 + 再次加载 GameScene」时会指向被删掉的副本；
/// 在 Start 中改为跟随 <see cref="PersistentGameplayDroneBootstrap.Instance"/>（与 IsoDroneController 同物体）。
/// </summary>
[DefaultExecutionOrder(20)]
public class CameraFollowDroneTargetRebind : MonoBehaviour
{
    void Start()
    {
        var follow = GetComponent<CameraFollow>();
        if (follow == null)
            return;

        if (PersistentGameplayDroneBootstrap.Instance != null)
        {
            follow.target = PersistentGameplayDroneBootstrap.Instance.transform;
            return;
        }

        var drone = FindObjectOfType<IsoDroneController>();
        if (drone != null)
            follow.target = drone.transform;
    }
}
