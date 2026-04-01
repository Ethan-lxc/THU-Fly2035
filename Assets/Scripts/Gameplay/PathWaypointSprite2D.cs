using UnityEngine;

/// <summary>
/// 用带 <see cref="SpriteRenderer"/> 的图片当路点（编辑时在 Scene 里看得见）。
/// <see cref="ConstantSpeedPathFollower2D"/> 仍只读 Transform 位置；子物体挂本组件 + 指定 Sprite 即可。
/// UI 世界坐标下的 <see cref="UnityEngine.UI.Image"/> 也可当路点，只要把带 RectTransform 的物体放进 waypoints / waypointRoot 子级，无需本组件。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class PathWaypointSprite2D : MonoBehaviour
{
    [Tooltip("勾选后进入 Play 会关闭 SpriteRenderer，避免正式画面里露出路点贴图（仅 Transform 仍参与路径）")]
    public bool hideSpriteInPlayMode = true;

    SpriteRenderer _spriteRenderer;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
            return;

        if (hideSpriteInPlayMode && Application.isPlaying)
            _spriteRenderer.enabled = false;
    }
}
