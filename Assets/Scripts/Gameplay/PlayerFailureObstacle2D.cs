using UnityEngine;

/// <summary>
/// 2D 触发体：玩家进入时触发 <see cref="PlayerFailureController"/> 撞墙失败（沿用最近对白检查点回档）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class PlayerFailureObstacle2D : MonoBehaviour
{
    public PlayerFailureController failureController;

    [Tooltip("与玩家碰撞体 Tag 一致，默认 Player")]
    public string playerTag = "Player";

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null)
            c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(playerTag))
            return;

        var ctrl = failureController != null
            ? failureController
            : FindObjectOfType<PlayerFailureController>();

        ctrl?.TriggerFailure(PlayerFailureController.Kind.WallHit);
    }
}
