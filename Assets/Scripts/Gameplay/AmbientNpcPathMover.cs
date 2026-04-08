using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 沿 Transform 路点移动的环境 NPC。翻面由<strong>当前路段平面切线</strong>（上一路点 → 当前目标路点）驱动，
/// 避免用「位置到目标残差」在过弯/竖线段落入死区导致不转向；位移在 Update，朝向在 LateUpdate 施加。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(32000)]
public class AmbientNpcPathMover : MonoBehaviour
{
    public enum LoopMode
    {
        Once,
        Loop,
        PingPong
    }

    public enum FaceMoveMode
    {
        None,
        FlipScaleX,
        RotateY,

        /// <summary>仅用 <see cref="SpriteRenderer.flipX"/>，不改 Transform 缩放。须放枚举末尾以免打乱旧场景的 RotateY 序列化。</summary>
        FlipSpriteRendererX
    }

    [Header("路径")]
    [Tooltip("路点世界坐标；空或全空则不动")]
    public List<Transform> waypoints = new List<Transform>();

    [Tooltip("勾选：列表第 0 个即出生/当前位置，首个移动目标是索引 1（至少 2 个点才有位移）")]
    public bool firstWaypointMatchesSpawn;

    [Tooltip(
        "勾选：在 Start 里记录路点世界坐标快照，运行中只沿快照移动。" +
        "可配合下方「拆到场景根」保留编辑层级；不勾选则每帧读路点当前坐标（适合路点跟移动平台）。")]
    public bool freezePathWorldPositionsAtAwake = true;

    [Tooltip(
        "在拍下快照后，把列表里「仍是本物体后代」的路点 SetParent 到场景根并保持世界坐标。")]
    public bool detachWaypointsToSceneRootAfterSnapshot = true;

    [Tooltip("0=Start 当帧立刻拍快照；若出生点仍在其它脚本的 Update 里刷新，可改为 1～2")]
    [Min(0)] public int pathSetupDelayFrames;

    [Header("运动")]
    [Min(0f)] public float moveSpeed = 2f;

    [Tooltip("到达每个路点后的停留时间（秒）")]
    [Min(0f)] public float waitAtEachWaypoint;

    public LoopMode loopMode = LoopMode.Loop;

    [Tooltip("判定到达路点的平面距离（XY）阈值")]
    [Min(0.0001f)] public float arrivalEpsilon = 0.05f;

    [Header("平面（2D）")]
    [Tooltip("为 true 时仅比较 XY 位移，且每帧把 Z 锁定为 Fixed Z")]
    public bool lockZ = true;

    [Tooltip("Lock Z 时的世界 Z；若 Capture Fixed Z From Spawn 勾选，会在 Awake 用当前位置覆盖")]
    public float fixedZ;

    [Tooltip("Awake 时把 Fixed Z 设为当前物体世界 Z")]
    public bool captureFixedZFromSpawnPosition = true;

    [Header("朝向（2D，由路段切线决定）")]
    public FaceMoveMode faceMoveDirection = FaceMoveMode.FlipScaleX;

    [Tooltip("勾选后翻转左右语义与默认相反")]
    public bool invertFacing;

    [Tooltip("关=用世界 X 分量；开=用世界 Y 分量（与路段切线点积）")]
    public bool useWorldYAsFlipLateralAxis;

    [Tooltip("主 SpriteRenderer；空则 GetComponentInChildren(..., true)")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("为 true 时对所有子级 SpriteRenderer 同步 flipX")]
    public bool applySpriteFlipToAllChildren;

    [Tooltip("FlipScaleX 且不用 flipX 时：水平镜像作用到此 Transform；空则用挂脚本的物体")]
    public Transform mirrorScaleTarget;

    [Tooltip("路段切线在翻转轴上投影绝对值小于 max(1e-5, 此值×0.01) 时不更新 flip（纯竖线段横版不翻面）")]
    [Min(0.0001f)] public float faceHorizontalEpsilon = 0.001f;

    [Tooltip("RotateY：路段切线过短时不改旋转")]
    [Min(1e-8f)] public float rotateMinDirectionSqr = 1e-6f;

    [Header("Animator（可选）")]
    [Tooltip("空则 GetComponentInChildren")]
    public Animator animator;

    [Tooltip("启用组件期间临时关闭 applyRootMotion（与 ConstantSpeedPathFollower2D 一致）")]
    public bool disableAnimatorRootMotionWhileFollowing = true;

    [Tooltip("留空则不写入该参数")]
    public string speedFloatParameter = "Speed";

    public string isMovingBoolParameter = "IsMoving";

    [Header("时间")]
    public bool useUnscaledTime;

    [Header("生命周期")]
    public bool playOnEnable = true;

    public bool restartFromFirstWaypointOnEnable = true;

    [Header("事件")]
    [Tooltip("LoopMode=Once 且走完最后一个路点并结束等待后触发一次")]
    public UnityEvent onPathCompletedOnce;

    int _targetIndex;
    int _pingPongStep = 1;
    float _waitRemain;
    bool _pathFinished;
    float _absInitialScaleX = 1f;
    bool _animHasSpeed;
    bool _animHasIsMoving;
    Vector3[] _waypointWorldSnapshot;
    bool _frozenPathSetupComplete;
    SpriteRenderer[] _spriteRenderersForFlipCache;
    bool _warnedMissingSpriteForFlip;
    bool _animatorRootMotionLocked;
    bool _savedAnimatorApplyRootMotion;

    /// <summary>归一化平面切线，从路段起点指向当前目标路点。</summary>
    Vector2 _segmentPlanarDir = Vector2.right;

    Vector3 _pathAnchorAtReset;
    bool _segmentLoopWrapToZero;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        var flipScaleRoot = mirrorScaleTarget != null ? mirrorScaleTarget : transform;
        _absInitialScaleX = Mathf.Abs(flipScaleRoot.localScale.x);
        if (_absInitialScaleX < 1e-5f)
            _absInitialScaleX = 1f;

        if (lockZ && captureFixedZFromSpawnPosition)
            fixedZ = transform.position.z;

        RefreshAnimatorParameterCache();
        ResetPathState();
        _frozenPathSetupComplete = !freezePathWorldPositionsAtAwake;
    }

    void OnDisable()
    {
        RestoreAnimatorRootMotionLock();
    }

    void Start()
    {
        if (!freezePathWorldPositionsAtAwake)
        {
            RefreshSegmentFacingDir();
            return;
        }

        if (pathSetupDelayFrames <= 0)
        {
            ApplyFrozenPathSnapshotAndDetach();
            _frozenPathSetupComplete = true;
            RefreshSegmentFacingDir();
        }
        else
            StartCoroutine(CoDelayedFrozenPathSetup());
    }

    IEnumerator CoDelayedFrozenPathSetup()
    {
        for (var i = 0; i < pathSetupDelayFrames; i++)
            yield return null;
        ApplyFrozenPathSnapshotAndDetach();
        _frozenPathSetupComplete = true;
        RefreshSegmentFacingDir();
    }

    void RefreshAnimatorParameterCache()
    {
        _animHasSpeed = false;
        _animHasIsMoving = false;
        if (animator == null)
            return;
        if (!string.IsNullOrEmpty(speedFloatParameter))
            _animHasSpeed = AnimatorHasParameter(animator, speedFloatParameter, AnimatorControllerParameterType.Float);
        if (!string.IsNullOrEmpty(isMovingBoolParameter))
            _animHasIsMoving = AnimatorHasParameter(animator, isMovingBoolParameter, AnimatorControllerParameterType.Bool);
    }

    void OnEnable()
    {
        TryApplyAnimatorRootMotionLock();
        if (!restartFromFirstWaypointOnEnable)
        {
            if (!freezePathWorldPositionsAtAwake || _frozenPathSetupComplete)
                RefreshSegmentFacingDir();
            return;
        }

        ResetPathState();
        if (!freezePathWorldPositionsAtAwake || _frozenPathSetupComplete)
            RefreshSegmentFacingDir();
    }

    void TryApplyAnimatorRootMotionLock()
    {
        if (animator == null || !disableAnimatorRootMotionWhileFollowing || _animatorRootMotionLocked)
            return;
        _savedAnimatorApplyRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = false;
        _animatorRootMotionLocked = true;
    }

    void RestoreAnimatorRootMotionLock()
    {
        if (animator == null || !_animatorRootMotionLocked)
            return;
        animator.applyRootMotion = _savedAnimatorApplyRootMotion;
        _animatorRootMotionLocked = false;
    }

    void ResetPathState()
    {
        _pathFinished = false;
        _pingPongStep = 1;
        _waitRemain = 0f;
        _segmentLoopWrapToZero = false;
        _pathAnchorAtReset = transform.position;

        if (waypoints == null || waypoints.Count == 0)
            _targetIndex = 0;
        else if (firstWaypointMatchesSpawn && waypoints.Count > 1)
            _targetIndex = 1;
        else
            _targetIndex = 0;
    }

    void Update()
    {
        if (!playOnEnable || !isActiveAndEnabled)
        {
            DriveAnimator(false, 0f);
            return;
        }

        if (freezePathWorldPositionsAtAwake && !_frozenPathSetupComplete)
        {
            DriveAnimator(false, 0f);
            return;
        }

        if (_pathFinished || waypoints == null || waypoints.Count == 0 || moveSpeed <= 0f)
        {
            DriveAnimator(false, 0f);
            return;
        }

        if (!TryResolveTargetIndex(out var activeIndex))
        {
            DriveAnimator(false, 0f);
            return;
        }

        _targetIndex = activeIndex;

        var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_waitRemain > 0f)
        {
            _waitRemain -= dt;
            DriveAnimator(false, 0f);
            return;
        }

        var pos = transform.position;
        var target = GetWaypointWorld(_targetIndex);
        var step = moveSpeed * dt;
        var newPos = Vector3.MoveTowards(pos, target, step);
        if (lockZ)
            newPos.z = fixedZ;

        transform.position = newPos;

        var dist = PlanarDistance(newPos, target);
        if (dist <= arrivalEpsilon)
            OnArrivedAtWaypoint();
        else
            DriveAnimator(true, moveSpeed);
    }

    void LateUpdate()
    {
        ApplyFacingFromSegmentDir();
    }

    void RefreshSegmentFacingDir()
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            _segmentPlanarDir = Vector2.right;
            return;
        }

        if (!TryResolveTargetIndex(out var k))
        {
            _segmentPlanarDir = Vector2.right;
            return;
        }

        var n = waypoints.Count;
        var to = GetWaypointPlanar(k);
        var from = GetSegmentStartPlanar(k, n);
        var d = to - from;

        if (d.sqrMagnitude < 1e-10f)
        {
            if (_segmentPlanarDir.sqrMagnitude > 1e-10f)
                return;

            Debug.LogWarning(
                $"{nameof(AmbientNpcPathMover)}: 路段长度过短（路点可能重合），使用默认朝向。对象={name}",
                this);
            _segmentPlanarDir = Vector2.right;
            return;
        }

        _segmentPlanarDir = d.normalized;
        if (_segmentLoopWrapToZero && k == 0)
            _segmentLoopWrapToZero = false;
    }

    Vector2 GetWaypointPlanar(int i)
    {
        var w = GetWaypointWorld(i);
        return new Vector2(w.x, w.y);
    }

    Vector2 GetSegmentStartPlanar(int k, int n)
    {
        if (loopMode == LoopMode.PingPong && _pingPongStep < 0 && k >= 0 && k < n - 1)
            return GetWaypointPlanar(k + 1);

        if (k > 0)
            return GetWaypointPlanar(k - 1);

        if (loopMode == LoopMode.Loop && n > 1 && _segmentLoopWrapToZero && k == 0)
            return GetWaypointPlanar(n - 1);

        return new Vector2(_pathAnchorAtReset.x, _pathAnchorAtReset.y);
    }

    void ApplyFacingFromSegmentDir()
    {
        if (faceMoveDirection == FaceMoveMode.None)
            return;
        if (!playOnEnable || !isActiveAndEnabled)
            return;
        if (freezePathWorldPositionsAtAwake && !_frozenPathSetupComplete)
            return;
        if (_pathFinished || waypoints == null || waypoints.Count == 0 || moveSpeed <= 0f)
            return;
        if (!TryResolveTargetIndex(out _))
            return;

        var refAxis = useWorldYAsFlipLateralAxis ? Vector2.up : Vector2.right;
        var lateral = Vector2.Dot(_segmentPlanarDir, refAxis);
        if (invertFacing)
            lateral = -lateral;

        var lateralDead = Mathf.Max(1e-5f, faceHorizontalEpsilon * 0.01f);
        if (Mathf.Abs(lateral) < lateralDead)
            return;

        switch (faceMoveDirection)
        {
            case FaceMoveMode.FlipSpriteRendererX:
            case FaceMoveMode.FlipScaleX:
                ApplyFlipFromLateralSign(lateral);
                break;
            case FaceMoveMode.RotateY:
            {
                Vector3 dir3;
                if (lockZ)
                    dir3 = new Vector3(_segmentPlanarDir.x, 0f, _segmentPlanarDir.y);
                else
                    dir3 = new Vector3(_segmentPlanarDir.x, 0f, _segmentPlanarDir.y);

                if (invertFacing)
                    dir3 = -dir3;

                if (dir3.sqrMagnitude > rotateMinDirectionSqr)
                    transform.rotation = Quaternion.LookRotation(dir3.normalized, Vector3.up);
                break;
            }
        }
    }

    void ApplyFlipFromLateralSign(float lateral)
    {
        var flipX = lateral < 0f;
        if (ShouldUseSpriteFlip())
            WriteSpriteFlipXAll(flipX);
        else
        {
            if (faceMoveDirection == FaceMoveMode.FlipSpriteRendererX && !_warnedMissingSpriteForFlip)
            {
                Debug.LogWarning($"{nameof(AmbientNpcPathMover)}: 未找到 SpriteRenderer，已回退为缩放镜像。对象={name}", this);
                _warnedMissingSpriteForFlip = true;
            }

            ApplyScaleFromFlipXBool(flipX);
        }
    }

    void WriteSpriteFlipXAll(bool flipX)
    {
        EnsureSpriteRendererForFacing();
        EnsureSpriteFlipCache();

        if (applySpriteFlipToAllChildren && _spriteRenderersForFlipCache != null && _spriteRenderersForFlipCache.Length > 0)
        {
            foreach (var r in _spriteRenderersForFlipCache)
            {
                if (r != null)
                    r.flipX = flipX;
            }

            return;
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = flipX;
    }

    void ApplyScaleFromFlipXBool(bool flipX)
    {
        var flipRoot = mirrorScaleTarget != null ? mirrorScaleTarget : transform;
        var s = flipRoot.localScale;
        s.x = flipX ? -_absInitialScaleX : _absInitialScaleX;
        flipRoot.localScale = s;
    }

    void EnsureSpriteRendererForFacing()
    {
        if (spriteRenderer != null)
            return;
        if (faceMoveDirection != FaceMoveMode.FlipScaleX && faceMoveDirection != FaceMoveMode.FlipSpriteRendererX)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    bool ShouldUseSpriteFlip()
    {
        EnsureSpriteFlipCache();
        if (applySpriteFlipToAllChildren && _spriteRenderersForFlipCache != null && _spriteRenderersForFlipCache.Length > 0)
            return true;
        return spriteRenderer != null;
    }

    void EnsureSpriteFlipCache()
    {
        if (!applySpriteFlipToAllChildren)
            return;
        if (_spriteRenderersForFlipCache != null)
            return;
        _spriteRenderersForFlipCache = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void OnArrivedAtWaypoint()
    {
        _waitRemain = waitAtEachWaypoint;
        DriveAnimator(false, 0f);

        if (waypoints == null || waypoints.Count == 0)
            return;

        var n = waypoints.Count;

        switch (loopMode)
        {
            case LoopMode.Once:
                if (_targetIndex >= n - 1)
                {
                    _pathFinished = true;
                    onPathCompletedOnce?.Invoke();
                    return;
                }

                _targetIndex = NextIndexForward(_targetIndex);
                break;

            case LoopMode.Loop:
            {
                var oldIdx = _targetIndex;
                _targetIndex = (_targetIndex + 1) % n;
                _segmentLoopWrapToZero = n > 1 && oldIdx == n - 1 && _targetIndex == 0;
                if (!TryResolveTargetIndex(out var loopIdx))
                {
                    _pathFinished = true;
                    return;
                }

                _targetIndex = loopIdx;
                break;
            }

            case LoopMode.PingPong:
                var next = _targetIndex + _pingPongStep;
                if (next >= n)
                {
                    if (n <= 1)
                    {
                        _pathFinished = true;
                        return;
                    }

                    _pingPongStep = -1;
                    next = _targetIndex + _pingPongStep;
                }
                else if (next < 0)
                {
                    _pingPongStep = 1;
                    next = _targetIndex + _pingPongStep;
                }

                _targetIndex = next;
                if (!TryResolveTargetIndex(out var ppIdx))
                {
                    _pathFinished = true;
                    return;
                }

                _targetIndex = ppIdx;
                break;
        }

        if (!TryResolveTargetIndex(out var resolved))
            return;

        _targetIndex = resolved;
        RefreshSegmentFacingDir();
    }

    int NextIndexForward(int src)
    {
        var i = src + 1;
        while (i < waypoints.Count && waypoints[i] == null)
            i++;
        return i < waypoints.Count ? i : src;
    }

    bool TryResolveTargetIndex(out int index)
    {
        index = _targetIndex;
        var guard = 0;
        var max = waypoints != null ? waypoints.Count + 2 : 0;
        while (guard++ < max && index >= 0 && index < waypoints.Count && waypoints[index] == null)
            index++;
        if (waypoints == null || index < 0 || index >= waypoints.Count || waypoints[index] == null)
            return false;
        return true;
    }

    void ApplyFrozenPathSnapshotAndDetach()
    {
        RebuildWaypointWorldSnapshotFromTransforms();
        if (freezePathWorldPositionsAtAwake && detachWaypointsToSceneRootAfterSnapshot)
            DetachListedWaypointsPreserveWorldPose();
    }

    void RebuildWaypointWorldSnapshotFromTransforms()
    {
        if (!freezePathWorldPositionsAtAwake || waypoints == null)
        {
            _waypointWorldSnapshot = null;
            return;
        }

        _waypointWorldSnapshot = new Vector3[waypoints.Count];
        for (var i = 0; i < waypoints.Count; i++)
        {
            var t = waypoints[i];
            _waypointWorldSnapshot[i] = t != null ? t.position : Vector3.zero;
        }
    }

    void DetachListedWaypointsPreserveWorldPose()
    {
        if (waypoints == null)
            return;

        var self = transform;
        var toDetach = new List<Transform>();
        foreach (var t in waypoints)
        {
            if (t == null || t == self)
                continue;
            if (t.IsChildOf(self))
                toDetach.Add(t);
        }

        if (toDetach.Count == 0)
            return;

        toDetach.Sort((a, b) => GetTransformDepthBelow(b, self).CompareTo(GetTransformDepthBelow(a, self)));

        foreach (var t in toDetach)
        {
            if (t == null)
                continue;
            t.SetParent(null, true);
        }
    }

    static int GetTransformDepthBelow(Transform t, Transform ancestor)
    {
        var d = 0;
        var p = t;
        while (p != null && p != ancestor)
        {
            d++;
            p = p.parent;
        }

        return p == ancestor ? d : -1;
    }

    Vector3 GetWaypointWorld(int i)
    {
        if (waypoints == null || i < 0 || i >= waypoints.Count || waypoints[i] == null)
            return transform.position;

        var p = freezePathWorldPositionsAtAwake && _waypointWorldSnapshot != null && i < _waypointWorldSnapshot.Length
            ? _waypointWorldSnapshot[i]
            : waypoints[i].position;
        if (lockZ)
            p.z = fixedZ;
        return p;
    }

    float PlanarDistance(Vector3 a, Vector3 b)
    {
        if (lockZ)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        return Vector3.Distance(a, b);
    }

    void DriveAnimator(bool moving, float speed)
    {
        if (animator == null)
            return;

        if (_animHasIsMoving)
            animator.SetBool(isMovingBoolParameter, moving);

        if (_animHasSpeed)
            animator.SetFloat(speedFloatParameter, moving ? speed : 0f);
    }

    static bool AnimatorHasParameter(Animator anim, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in anim.parameters)
        {
            if (p.type == type && p.name == name)
                return true;
        }

        return false;
    }

    /// <summary>从当前状态重新开始走路线（路径快照不会重拍）。</summary>
    public void RestartPath()
    {
        ResetPathState();
        RefreshSegmentFacingDir();
    }

    /// <summary>按当前路点 Transform 再拍一次世界坐标并重新开始走。</summary>
    public void RestartPathAndRecaptureWorldPositions()
    {
        RebuildWaypointWorldSnapshotFromTransforms();
        if (freezePathWorldPositionsAtAwake && detachWaypointsToSceneRootAfterSnapshot)
            DetachListedWaypointsPreserveWorldPose();
        ResetPathState();
        RefreshSegmentFacingDir();
    }
}
