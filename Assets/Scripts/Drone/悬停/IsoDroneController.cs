using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Gameplay.Events;

public class IsoDroneController : MonoBehaviour
{
    public enum WorldInteractDetectionMode
    {
        OverlapCircle,
        /// <summary>使用本物体上 Trigger 体（如 CapsuleCollider2D）与事件碰撞体重叠；需带 Rigidbody2D。</summary>
        ThisRigidbodyTriggers
    }

    public float moveSpeed = 5f;
    public float smoothTime = 0.2f;
    public float hoverAmplitude = 0.15f;
    public float hoverFrequency = 2.5f;

    [Header("输入")]
    [Tooltip("为 true 时，点击在 UI 上不会下发移动指令（避免点按钮时误移动）")]
    public bool ignoreClicksOverUI = true;

    [Header("世界互动（E）")]
    [Tooltip("开启后在本脚本内处理靠近可互动物体与按 E；无需在 player 上额外挂互动组件")]
    public bool enableWorldInteract;

    public WorldInteractDetectionMode worldInteractDetection = WorldInteractDetectionMode.ThisRigidbodyTriggers;

    [Min(0.1f)]
    [Tooltip("仅在 OverlapCircle 模式下使用")]
    public float worldInteractRadius = 2.2f;

    public LayerMask worldInteractLayers = ~0;

    public KeyCode worldInteractKey = KeyCode.E;

    [Tooltip("为 true 时指针在 UI 上不响应 E")]
    public bool worldInteractIgnoreWhenPointerOverUI = true;

    [Header("事件 — 锚点/任务等")]
    [Tooltip("每次点击地图设置新目的地时触发，参数为世界坐标")]
    public UnityEvent<Vector2> onDestinationSet;

    [Tooltip("无人机到达当前目的地并停稳时触发")]
    public UnityEvent onArrivedAtDestination;

    static readonly List<Collider2D> ScratchPrune = new List<Collider2D>();

    readonly HashSet<Collider2D> _worldOverlap = new HashSet<Collider2D>();

    Vector2 targetWorldPos;
    Vector2 waypoint;
    Vector2 currentVelocity;
    bool isMovingToWaypoint = false;
    bool isMovingToTarget = false;

    Transform visualChild;
    SpriteRenderer spriteRenderer;

    IWorldInteractable _interactCurrent;
    IWorldInteractable _interactLastHighlight;

    PauseMenuController _pauseMenu;
    Rigidbody2D _rb;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (transform.childCount > 0) visualChild = transform.GetChild(0);
        targetWorldPos = transform.position;
        _pauseMenu = FindObjectOfType<PauseMenuController>();
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (GameplayModalBlocker.IsBlockingInput)
                return;
            if (ignoreClicksOverUI && EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                return;
            CalculateIsoPath();
        }

        if (enableWorldInteract)
            WorldInteractUpdate();

        ApplyHover();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!enableWorldInteract || worldInteractDetection != WorldInteractDetectionMode.ThisRigidbodyTriggers ||
            other == null)
            return;

        if (_rb != null && other.attachedRigidbody == _rb)
            return;

        if (other.GetComponentInParent<IWorldInteractable>() == null)
            return;

        _worldOverlap.Add(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!enableWorldInteract || worldInteractDetection != WorldInteractDetectionMode.ThisRigidbodyTriggers ||
            other == null)
            return;

        _worldOverlap.Remove(other);
    }

    void WorldInteractUpdate()
    {
        if (GameplayModalBlocker.IsBlockingInput)
        {
            ClearWorldInteractHighlight();
            return;
        }

        if (_pauseMenu != null && _pauseMenu.IsOpen)
        {
            ClearWorldInteractHighlight();
            return;
        }

        _interactCurrent = FindBestWorldInteractable();
        if (_interactCurrent != _interactLastHighlight)
        {
            _interactLastHighlight?.SetProximityHighlight(false);
            _interactLastHighlight = _interactCurrent;
            _interactCurrent?.SetProximityHighlight(true);
        }

        if (_interactCurrent == null || !Input.GetKeyDown(worldInteractKey))
            return;

        if (worldInteractIgnoreWhenPointerOverUI && EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (!_interactCurrent.CanInteract(transform))
            return;

        _interactCurrent.BeginInteract(transform);
    }

    void ClearWorldInteractHighlight()
    {
        if (_interactLastHighlight != null)
        {
            _interactLastHighlight.SetProximityHighlight(false);
            _interactLastHighlight = null;
            _interactCurrent = null;
        }
    }

    IWorldInteractable FindBestWorldInteractable()
    {
        return worldInteractDetection == WorldInteractDetectionMode.OverlapCircle
            ? FindWorldInteractViaOverlapCircle()
            : FindWorldInteractViaTriggers();
    }

    static int GetWorldInteractResolvePriority(IWorldInteractable iw)
    {
        if (iw is IWorldInteractableResolvePriority p)
            return p.WorldInteractResolvePriority;
        return 0;
    }

    IWorldInteractable FindWorldInteractViaOverlapCircle()
    {
        var pos = (Vector2)transform.position;
        var hits = Physics2D.OverlapCircleAll(pos, worldInteractRadius, worldInteractLayers);
        IWorldInteractable best = null;
        var bestDist = float.MaxValue;
        var bestPri = int.MinValue;
        foreach (var h in hits)
        {
            if (h == null)
                continue;
            var iw = h.GetComponentInParent<IWorldInteractable>();
            if (iw == null)
                continue;
            if (!iw.CanInteract(transform))
                continue;
            var d = ((Vector2)h.transform.position - pos).sqrMagnitude;
            var pri = GetWorldInteractResolvePriority(iw);
            if (best == null || pri > bestPri || (pri == bestPri && d < bestDist))
            {
                bestDist = d;
                bestPri = pri;
                best = iw;
            }
        }

        return best;
    }

    IWorldInteractable FindWorldInteractViaTriggers()
    {
        ScratchPrune.Clear();
        foreach (var c in _worldOverlap)
        {
            if (c == null)
                ScratchPrune.Add(c);
        }

        foreach (var c in ScratchPrune)
            _worldOverlap.Remove(c);

        IWorldInteractable best = null;
        var bestDist = float.MaxValue;
        var bestPri = int.MinValue;
        var pos = (Vector2)transform.position;

        foreach (var col in _worldOverlap)
        {
            if (col == null)
                continue;
            if ((worldInteractLayers.value & (1 << col.gameObject.layer)) == 0)
                continue;
            var iw = col.GetComponentInParent<IWorldInteractable>();
            if (iw == null)
                continue;
            if (!iw.CanInteract(transform))
                continue;
            var d = ((Vector2)col.bounds.center - pos).sqrMagnitude;
            var pri = GetWorldInteractResolvePriority(iw);
            if (best == null || pri > bestPri || (pri == bestPri && d < bestDist))
            {
                bestDist = d;
                bestPri = pri;
                best = iw;
            }
        }

        return best;
    }

    void CalculateIsoPath()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        targetWorldPos = new Vector2(mousePos.x, mousePos.y);

        Vector2 delta = targetWorldPos - (Vector2)transform.position;

        // 轴测分解
        float b = (delta.y - 0.5f * delta.x);
        float a = delta.x + b;

        waypoint = (Vector2)transform.position + new Vector2(a, a * 0.5f);

        isMovingToWaypoint = true;
        isMovingToTarget = true;

        onDestinationSet?.Invoke(targetWorldPos);
    }

    /// <summary>场景切换传送后调用：清除未完成的寻路目标，避免仍向旧世界坐标移动。</summary>
    public void SyncMovementStateAfterTeleport()
    {
        var p = (Vector2)transform.position;
        targetWorldPos = p;
        waypoint = p;
        currentVelocity = Vector2.zero;
        isMovingToWaypoint = false;
        isMovingToTarget = false;
    }

    void FixedUpdate()
    {
        if (!isMovingToTarget) return;

        if (isMovingToWaypoint)
        {
            // --- 拐点阶段：使用常量速度，防止减速 ---
            Vector2 dir = (waypoint - (Vector2)transform.position).normalized;
            // 模拟平滑启动，但接近拐点时不减速
            currentVelocity = Vector2.Lerp(currentVelocity, dir * moveSpeed, 0.1f);
            transform.position += (Vector3)currentVelocity * Time.fixedDeltaTime;

            // 到达拐点判定（距离够近就直接切换，不等待减速）
            if (Vector2.Distance(transform.position, waypoint) < 0.2f)
            {
                isMovingToWaypoint = false;
            }
        }
        else
        {
            // --- 终点阶段：使用 SmoothDamp 实现自然减速停止 ---
            transform.position = Vector2.SmoothDamp(
                transform.position,
                targetWorldPos,
                ref currentVelocity,
                smoothTime,
                moveSpeed
            );

            if (Vector2.Distance(transform.position, targetWorldPos) < 0.05f)
            {
                isMovingToTarget = false;
                currentVelocity = Vector2.zero;
                onArrivedAtDestination?.Invoke();
            }
        }

        // 转向处理
        if (currentVelocity.x != 0) spriteRenderer.flipX = currentVelocity.x < 0;
    }

    void ApplyHover()
    {
        if (visualChild != null)
        {
            float yOffset = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            visualChild.localPosition = new Vector3(0, yOffset, 0);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!enableWorldInteract || worldInteractDetection != WorldInteractDetectionMode.OverlapCircle)
            return;
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, worldInteractRadius);
    }
}
