using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 沿折线路点 <b>匀速</b>移动 Transform，并驱动 <see cref="Animator"/> 进入跑步等状态（原地跑步动画 + 脚本位移，勿开 Root Motion）。
/// 若物体带 <see cref="Rigidbody2D"/>，默认在启用期间改为 Kinematic 并关闭重力、在 <see cref="FixedUpdate"/> 里 <c>MovePosition</c>，避免受重力下坠。
/// 路点：任意 <see cref="Transform"/>（waypoints 或 waypointRoot 子物体）；启用时会把路点<strong>世界坐标</strong>拷入折线缓存，沿固定折线移动（避免路点挂在角色子级时整条路径跟着跑）。
/// 纯 Empty 可选用 <see cref="PathWaypointGizmo"/>，用贴图当路点可选用 <see cref="PathWaypointSprite2D"/>。
/// </summary>
[DisallowMultipleComponent]
public class ConstantSpeedPathFollower2D : MonoBehaviour
{
    [Header("路线")]
    [Tooltip("按顺序经过的世界坐标点；索引 0 为起点。若已指定 waypointRoot，可留空或作备用")]
    public Transform[] waypoints;

    [Tooltip("指定后运行时用其「直接子物体」按 Hierarchy 从上到下顺序作为路点，无需再往 waypoints 里拖引用")]
    public Transform waypointRoot;

    [Tooltip("世界坐标单位/秒，沿折线总长匀速")]
    [Min(0.01f)]
    public float moveSpeed = 3f;

    [Tooltip("走完全部路点后从最后一点回到第 0 点继续")]
    public bool loop = true;

    [Tooltip("启用时从第 0 个路点位置开始，避免与预制体摆放偏差")]
    public bool snapToFirstWaypointOnEnable = true;

    [Header("物理（2D）")]
    [Tooltip("留空则在本物体上 GetComponent；若角色带 Rigidbody2D，必须用下面选项之一与脚本位移配合，否则会受重力一直往下掉")]
    public Rigidbody2D rigidbody2DOverride;

    [Tooltip("启用本组件期间：暂存并重设为 Kinematic、gravityScale=0，OnDisable 时还原。推荐给「纯脚本沿路移动」")]
    public bool makeKinematicWhileFollowing = true;

    [Header("Animator 位移")]
    [Tooltip("启用期间关闭 applyRootMotion，避免跑步片段带位移把角色拖离折线")]
    public bool disableAnimatorRootMotionWhileFollowing = true;

    [Header("Scene 调试")]
    [Tooltip("空物体路点没有模型，默认看不见；开启后在 Scene 里画球和连线，便于对齐。确认 Scene 窗口右上角 Gizmos 已打开")]
    public bool drawPathGizmos = true;

    public Color pathGizmoColor = new Color(0f, 0.85f, 1f, 0.95f);

    [Min(0.05f)]
    public float pathGizmoRadius = 0.35f;

#if UNITY_EDITOR
    [Tooltip("在 Scene 中路点旁显示 0、1、2… 行走顺序（仅编辑器）")]
    public bool showWaypointIndexLabels = true;
#endif

    [Header("朝向（2D）")]
    [Tooltip("根据水平移动方向翻转 Sprite")]
    public bool flipSpriteToFaceDirection = true;

    [Tooltip("空则在本物体及子物体上查找 SpriteRenderer")]
    public SpriteRenderer spriteRenderer;

    [Header("Animator")]
    [Tooltip("空则 GetComponentInChildren")]
    public Animator animator;

    [Tooltip("为 true 时用参数控制；为 false 则走路点时 Play 指定状态")]
    public bool useParameters = true;

    [Tooltip("移动中为 true；需在 Animator 里同名 bool 参数")]
    public string movingBoolParameter = "IsRunning";

    [Tooltip("可选：写入当前移动速度，便于 Blend Tree；留空则不写")]
    public string speedFloatParameter = "";

    [Tooltip("useParameters 为 false 时：移动中 Animator.Play 的状态名")]
    public string runStateName = "Run";

    [Tooltip("useParameters 为 false 时：停止后切回的状态名（空则不平滑切 Idle）")]
    public string idleStateName = "Idle";

    [Tooltip("useParameters 为 false 时 Play 所在层")]
    public int animatorLayer = 0;

    [Header("生命周期")]
    [Tooltip("OnEnable 时自动开始沿路径走")]
    public bool playOnEnable = true;

    int _targetIndex = 1;
    bool _following;
    Transform[] _path;
    Vector3[] _pathWorld;
    Rigidbody2D _rb;
    bool _physicsPatchApplied;
    RigidbodyType2D _savedBodyType;
    float _savedGravityScale;
    bool _rootMotionPatchApplied;
    bool _savedApplyRootMotion;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        _rb = rigidbody2DOverride != null ? rigidbody2DOverride : GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        _path = BuildPath();
        if (_path == null || _path.Length < 2)
        {
            _pathWorld = null;
            Debug.LogWarning($"{nameof(ConstantSpeedPathFollower2D)}: 至少需要 2 个路点（waypointRoot 子物体或 waypoints 数组）。", this);
            _following = false;
            SetAnimatorMoving(false);
            return;
        }

        BuildPathWorldCache();

        ApplyPhysicsForPathFollow();
        ApplyAnimatorRootMotionLock();

        if (snapToFirstWaypointOnEnable && _pathWorld != null && _pathWorld.Length > 0)
            SnapToPosition(_pathWorld[0]);

        _targetIndex = 1;
        _following = playOnEnable;
        if (_following)
            SetAnimatorMoving(true);
        else
            SetAnimatorMoving(false);
    }

    void OnDisable()
    {
        RestorePhysicsAfterPathFollow();
        RestoreAnimatorRootMotion();
    }

    void BuildPathWorldCache()
    {
        _pathWorld = new Vector3[_path.Length];
        for (int i = 0; i < _path.Length; i++)
        {
            if (_path[i] != null)
                _pathWorld[i] = _path[i].position;
            else
                _pathWorld[i] = i > 0 ? _pathWorld[i - 1] : transform.position;
        }
    }

    void ApplyAnimatorRootMotionLock()
    {
        if (animator == null || !disableAnimatorRootMotionWhileFollowing || _rootMotionPatchApplied)
            return;

        _savedApplyRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = false;
        _rootMotionPatchApplied = true;
    }

    void RestoreAnimatorRootMotion()
    {
        if (animator == null || !_rootMotionPatchApplied)
            return;

        animator.applyRootMotion = _savedApplyRootMotion;
        _rootMotionPatchApplied = false;
    }

    void ApplyPhysicsForPathFollow()
    {
        if (_rb == null || !makeKinematicWhileFollowing || _physicsPatchApplied)
            return;

        _savedBodyType = _rb.bodyType;
        _savedGravityScale = _rb.gravityScale;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.velocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _physicsPatchApplied = true;
    }

    void RestorePhysicsAfterPathFollow()
    {
        if (_rb == null || !_physicsPatchApplied)
            return;

        _rb.bodyType = _savedBodyType;
        _rb.gravityScale = _savedGravityScale;
        _physicsPatchApplied = false;
    }

    void SnapToPosition(Vector3 worldPos)
    {
        transform.position = worldPos;
        if (_rb != null)
        {
            _rb.position = worldPos;
            _rb.velocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
    }

    void Update()
    {
        if (_rb != null)
            return;
        StepMovement(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (_rb == null)
            return;
        StepMovement(Time.fixedDeltaTime);
    }

    void StepMovement(float deltaTime)
    {
        if (!_following || _pathWorld == null || _pathWorld.Length < 2)
            return;

        if (_targetIndex < 0 || _targetIndex >= _pathWorld.Length)
        {
            _following = false;
            SetAnimatorMoving(false);
            return;
        }

        var from = transform.position;
        var destPos = _pathWorld[_targetIndex];
        destPos.z = from.z;

        var newPos = Vector3.MoveTowards(from, destPos, moveSpeed * deltaTime);
        newPos.z = from.z;

        if (flipSpriteToFaceDirection && spriteRenderer != null)
        {
            var dx = destPos.x - from.x;
            if (dx > 0.001f) spriteRenderer.flipX = false;
            else if (dx < -0.001f) spriteRenderer.flipX = true;
        }

        if (_rb != null)
        {
            if (makeKinematicWhileFollowing)
                _rb.velocity = Vector2.zero;
            _rb.MovePosition(newPos);
        }
        else
            transform.position = newPos;

        if ((newPos - destPos).sqrMagnitude > 0.0001f)
            return;

        if (_rb == null)
            transform.position = destPos;
        else
            _rb.MovePosition(destPos);

        if (_targetIndex >= _pathWorld.Length - 1)
        {
            if (loop)
                _targetIndex = 0;
            else
            {
                _following = false;
                SetAnimatorMoving(false);
            }
        }
        else
            _targetIndex++;
    }

    void OnDrawGizmos()
    {
        if (!drawPathGizmos)
            return;

        if (Application.isPlaying && _pathWorld != null && _pathWorld.Length > 0)
        {
            Gizmos.color = pathGizmoColor;
            foreach (var p in _pathWorld)
                Gizmos.DrawWireSphere(p, pathGizmoRadius);

            for (int i = 0; i < _pathWorld.Length - 1; i++)
                Gizmos.DrawLine(_pathWorld[i], _pathWorld[i + 1]);

            if (loop && _pathWorld.Length > 1)
                Gizmos.DrawLine(_pathWorld[^1], _pathWorld[0]);

#if UNITY_EDITOR
            DrawWaypointDiagnostics(_pathWorld);
#endif
        }
        else
        {
            var path = BuildPath();
            if (path == null || path.Length == 0)
                return;

            Gizmos.color = pathGizmoColor;
            foreach (var t in path)
            {
                if (t != null)
                    Gizmos.DrawWireSphere(t.position, pathGizmoRadius);
            }

            for (int i = 0; i < path.Length - 1; i++)
            {
                if (path[i] != null && path[i + 1] != null)
                    Gizmos.DrawLine(path[i].position, path[i + 1].position);
            }

            if (loop && path.Length > 1 && path[path.Length - 1] != null && path[0] != null)
                Gizmos.DrawLine(path[path.Length - 1].position, path[0].position);

#if UNITY_EDITOR
            DrawWaypointDiagnostics(path);
#endif
        }

#if UNITY_EDITOR
        if (showWaypointIndexLabels)
        {
            Handles.color = pathGizmoColor;
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = pathGizmoColor }
            };
            if (Application.isPlaying && _pathWorld != null)
            {
                for (int i = 0; i < _pathWorld.Length; i++)
                    Handles.Label(_pathWorld[i] + Vector3.up * pathGizmoRadius * 1.25f, i.ToString(), labelStyle);
            }
            else
            {
                var pathForLabels = BuildPath();
                if (pathForLabels != null)
                {
                    for (int i = 0; i < pathForLabels.Length; i++)
                    {
                        if (pathForLabels[i] == null)
                            continue;
                        Handles.Label(pathForLabels[i].position + Vector3.up * pathGizmoRadius * 1.25f, i.ToString(), labelStyle);
                    }
                }
            }
        }
#endif
    }

#if UNITY_EDITOR
    void DrawWaypointDiagnostics(Transform[] path)
    {
        int valid = 0;
        Vector3? first = null;
        var allCoincident = true;
        foreach (var t in path)
        {
            if (t == null)
                continue;
            valid++;
            if (first == null)
                first = t.position;
            else if ((t.position - first.Value).sqrMagnitude > 1e-6f)
                allCoincident = false;
        }

        if (valid < 2)
        {
            Handles.color = new Color(1f, 0.85f, 0f, 1f);
            var tip = waypointRoot != null && waypointRoot.childCount < 2
                ? $"Waypoint Root「{waypointRoot.name}」下需要至少 2 个子物体；当前子数={waypointRoot.childCount}（或填 waypoints 数组 Size≥2）"
                : "至少需要 2 个有效路点：在 Waypoint Root 下加子 Empty，或在 waypoints 里拖满 2 个以上引用";
            Handles.Label(transform.position + Vector3.up * (pathGizmoRadius * 1.5f + 0.5f), tip, EditorStyles.wordWrappedMiniLabel);
            return;
        }

        if (allCoincident)
        {
            Handles.color = new Color(1f, 0.85f, 0f, 1f);
            Handles.Label(transform.position + Vector3.up * (pathGizmoRadius * 1.5f + 0.5f),
                "多个路点世界坐标叠在一起：请选中各子物体（W 移动工具）拉开 X/Y，才看得见折线与多颗球");
        }
    }

    void DrawWaypointDiagnostics(Vector3[] worldPath)
    {
        if (worldPath == null || worldPath.Length == 0)
            return;

        int valid = worldPath.Length;
        var allCoincident = true;
        for (int i = 1; i < worldPath.Length; i++)
        {
            if ((worldPath[i] - worldPath[0]).sqrMagnitude > 1e-6f)
            {
                allCoincident = false;
                break;
            }
        }

        if (valid < 2)
        {
            Handles.color = new Color(1f, 0.85f, 0f, 1f);
            Handles.Label(transform.position + Vector3.up * (pathGizmoRadius * 1.5f + 0.5f),
                "至少需要 2 个有效路点。", EditorStyles.wordWrappedMiniLabel);
            return;
        }

        if (allCoincident)
        {
            Handles.color = new Color(1f, 0.85f, 0f, 1f);
            Handles.Label(transform.position + Vector3.up * (pathGizmoRadius * 1.5f + 0.5f),
                "多个路点世界坐标叠在一起：请拉开 X/Y");
        }
    }
#endif

    Transform[] BuildPath()
    {
        if (waypointRoot != null)
        {
            int n = waypointRoot.childCount;
            if (n < 2)
                return waypoints;

            var list = new Transform[n];
            for (int i = 0; i < n; i++)
                list[i] = waypointRoot.GetChild(i);
            return list;
        }

        return waypoints;
    }

    /// <summary>从当前位置、当前路点索引重新开始走（不重置 Transform，除非你先 snap）。</summary>
    public void StartFollowing()
    {
        if (_pathWorld == null || _pathWorld.Length < 2) return;
        _targetIndex = Mathf.Clamp(_targetIndex, 1, _pathWorld.Length - 1);
        _following = true;
        SetAnimatorMoving(true);
    }

    /// <summary>停在当前坐标并关掉移动动画参数/状态。</summary>
    public void StopFollowing()
    {
        _following = false;
        SetAnimatorMoving(false);
    }

    void SetAnimatorMoving(bool moving)
    {
        if (animator == null) return;

        if (useParameters)
        {
            if (!string.IsNullOrEmpty(movingBoolParameter))
                animator.SetBool(movingBoolParameter, moving);
            if (moving && !string.IsNullOrEmpty(speedFloatParameter))
                animator.SetFloat(speedFloatParameter, moveSpeed);
        }
        else
        {
            if (moving && !string.IsNullOrEmpty(runStateName))
                animator.Play(runStateName, animatorLayer, 0f);
            else if (!moving && !string.IsNullOrEmpty(idleStateName))
                animator.Play(idleStateName, animatorLayer, 0f);
        }
    }
}
