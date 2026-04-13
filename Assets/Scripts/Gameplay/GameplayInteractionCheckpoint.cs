using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在 NPC 开场对白时捕获专业/情感、玩家位置、背包与回退目标；失败时一键还原。
/// 建议与 <see cref="PlayerFailureController"/>、<see cref="FailureCardRevealOverlay"/> 一同挂在场景 <b>FailureHud</b> 上。
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class GameplayInteractionCheckpoint : MonoBehaviour
{
    public static GameplayInteractionCheckpoint Instance { get; private set; }

    [Tooltip("空则 FindObjectOfType")]
    public PlayerStatsHud statsHud;

    [Tooltip("空则 Tag=Player")]
    public Transform playerTransform;

    [Tooltip("空则 InventoryRuntime.Instance 或 Find")]
    public InventoryRuntime inventoryRuntime;

    sealed class Snapshot
    {
        public float Pro;
        public float Emo;
        public Vector3 Position;
        public Quaternion Rotation;
        public List<ClueEntry> Clues;
        public IInteractionRewindTarget RewindTarget;
    }

    Snapshot _last;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        ResolveRefs();
        if (_last == null)
            PushInternal(rewindTarget: null);
    }

    void ResolveRefs()
    {
        if (statsHud == null)
            statsHud = FindObjectOfType<PlayerStatsHud>();
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerTransform = p.transform;
        }

        if (inventoryRuntime == null)
            inventoryRuntime = InventoryRuntime.Instance != null
                ? InventoryRuntime.Instance
                : FindObjectOfType<InventoryRuntime>();
    }

    /// <summary>NPC 开启 External 对白时调用；覆盖上一份快照。</summary>
    public void PushForNpcInteraction(MonoBehaviour owner)
    {
        if (owner is not IInteractionRewindTarget target)
            return;
        PushInternal(target);
    }

    void PushInternal(IInteractionRewindTarget rewindTarget)
    {
        ResolveRefs();
        if (statsHud == null || playerTransform == null)
            return;

        var list = new List<ClueEntry>();
        if (inventoryRuntime != null)
        {
            foreach (var c in inventoryRuntime.Clues)
                list.Add(c);
        }

        _last = new Snapshot
        {
            Pro = statsHud.Professionalism,
            Emo = statsHud.Emotion,
            Position = playerTransform.position,
            Rotation = playerTransform.rotation,
            Clues = list,
            RewindTarget = rewindTarget
        };
    }

    public bool HasSnapshot => _last != null;

    public void RestoreLastSnapshot()
    {
        if (_last == null)
            return;
        ResolveRefs();
        if (statsHud == null || playerTransform == null)
            return;

        statsHud.SetStats(_last.Pro, _last.Emo);
        playerTransform.SetPositionAndRotation(_last.Position, _last.Rotation);
        var rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = Vector2.zero;

        inventoryRuntime?.ReplaceClues(_last.Clues);
        _last.RewindTarget?.ResetToPreInteractState();
    }
}
