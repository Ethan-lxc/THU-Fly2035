using System.Collections;
using UnityEngine;

/// <summary>
/// 专业或情感降至 0 及以下（含等于 0），或撞墙时：音效 + 全屏失败卡 → 延时 → Space 回档到最近检查点并复位 NPC。
/// 统计类失败仅在 <see cref="GameplayDialoguePanel"/> 无任何对白打开时才触发；对白进行中压低数值会在 NPC 流程结束（面板关闭）后再判定，避免对话中途弹失败。
/// 建议与同物体上的 <see cref="GameplayInteractionCheckpoint"/>、<see cref="FailureCardRevealOverlay"/> 一并挂在场景 <b>FailureHud</b> 下。
/// </summary>
[DefaultExecutionOrder(80)]
public sealed class PlayerFailureController : MonoBehaviour
{
    public enum Kind
    {
        DualStat,
        Professionalism,
        Emotion,
        WallHit
    }

    [Header("引用（可空，Start 从 GameplayHudLayout / 单例补齐）")]
    public PlayerStatsHud statsHud;
    public GameplayInteractionCheckpoint checkpoint;

    [Header("场景表现（URP：失败时世界去色 + 略压暗；UI 大卡仍为彩色）")]
    [Tooltip("空则用 Camera.main；会在此相机上挂或复用 FailureWorldDesaturateEffect")]
    public Camera worldCameraForFailureFx;

    [Tooltip("失败时全屏 Dim 颜色（盖住世界+已变灰的画面，突出大卡）")]
    public Color failureDimColor = new Color(0f, 0f, 0f, 0.78f);

    [Range(-100f, 0f)]
    public float failureWorldSaturation = -100f;

    [Range(-2f, 0f)]
    public float failureWorldPostExposure = -0.45f;

    [Header("流程")]
    [Min(0f)]
    public float delayBeforeSpacePromptSeconds = 2.5f;

    [Tooltip("延时结束后显示；支持鼠标左键或 Space 重生（回档）；由 FailureCardRevealOverlay 显示")]
    public string spacePromptText = "点击屏幕 或 按 Space 重生";

    [Header("双失败")]
    public Sprite dualFailureSprite;
    public string dualFailureTitle = "失控";
    public AudioClip dualFailureSfx;

    [Header("专业失败")]
    public Sprite proFailureSprite;
    public string proFailureTitle = "专业度崩溃";
    public AudioClip proFailureSfx;

    [Header("情感失败")]
    public Sprite emoFailureSprite;
    public string emoFailureTitle = "情感透支";
    public AudioClip emoFailureSfx;

    [Header("撞墙")]
    public Sprite wallFailureSprite;
    public string wallFailureTitle = "碰壁";
    public AudioClip wallFailureSfx;

    AudioSource _sfx;
    bool _failureActive;
    Coroutine _routine;
    FailureWorldDesaturateEffect _cachedDesat;

    /// <summary>对白刚结束，推迟到 <see cref="LateUpdate"/> 再读数，避免与 End/结算同帧顺序问题。</summary>
    bool _queuedStatFailureCheckAfterDialogue;

    void Awake()
    {
        EnsureSfx();
    }

    void Start()
    {
        var hud = FindObjectOfType<GameplayHudLayout>();
        if (hud != null && statsHud == null)
            statsHud = hud.playerStatsHud;

        if (checkpoint == null)
            checkpoint = GetComponent<GameplayInteractionCheckpoint>();
        if (checkpoint == null)
            checkpoint = GameplayInteractionCheckpoint.Instance != null
                ? GameplayInteractionCheckpoint.Instance
                : FindObjectOfType<GameplayInteractionCheckpoint>();

        if (statsHud != null)
            statsHud.StatsChanged += OnStatsChanged;

        GameplayDialoguePanel.DialogueFullyClosed += OnGameplayDialogueFullyClosed;
    }

    void OnDestroy()
    {
        GameplayDialoguePanel.DialogueFullyClosed -= OnGameplayDialogueFullyClosed;
        if (statsHud != null)
            statsHud.StatsChanged -= OnStatsChanged;
    }

    void LateUpdate()
    {
        if (!_queuedStatFailureCheckAfterDialogue || statsHud == null)
            return;
        if (_failureActive)
        {
            _queuedStatFailureCheckAfterDialogue = false;
            return;
        }

        if (IsAnyGameplayDialogueOpen())
            return;

        _queuedStatFailureCheckAfterDialogue = false;
        TryBeginStatFailure(statsHud.Professionalism, statsHud.Emotion);
    }

    static bool IsAnyGameplayDialogueOpen()
    {
        var panels = UnityEngine.Object.FindObjectsOfType<GameplayDialoguePanel>(false);
        for (var i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].IsOpen)
                return true;
        }

        return false;
    }

    void OnGameplayDialogueFullyClosed()
    {
        if (_failureActive)
            return;
        _queuedStatFailureCheckAfterDialogue = true;
    }

    void EnsureSfx()
    {
        if (_sfx != null)
            return;
        _sfx = GetComponent<AudioSource>();
        if (_sfx == null)
            _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.loop = false;
        _sfx.spatialBlend = 0f;
        _sfx.ignoreListenerPause = true;
    }

    void OnStatsChanged(float pro, float emo)
    {
        if (_failureActive)
            return;
        if (statsHud == null)
            return;
        if (IsAnyGameplayDialogueOpen())
            return;

        TryBeginStatFailure(pro, emo);
    }

    void TryBeginStatFailure(float pro, float emo)
    {
        if (_failureActive)
            return;

        var dual = pro <= 0f && emo <= 0f;
        var proOnly = pro <= 0f && emo > 0f;
        var emoOnly = emo <= 0f && pro > 0f;

        if (dual)
        {
            BeginFailure(Kind.DualStat);
            return;
        }

        if (proOnly)
            BeginFailure(Kind.Professionalism);
        else if (emoOnly)
            BeginFailure(Kind.Emotion);
    }

    /// <summary>由 <see cref="PlayerFailureObstacle2D"/> 或调试调用；撞墙不受对白延迟影响。</summary>
    public void TriggerFailure(Kind kind)
    {
        BeginFailure(kind);
    }

    void BeginFailure(Kind kind)
    {
        if (_failureActive)
            return;
        _queuedStatFailureCheckAfterDialogue = false;
        if (_routine != null)
            StopCoroutine(_routine);
        _failureActive = true;
        _routine = StartCoroutine(CoFailureFlow(kind));
    }

    IEnumerator CoFailureFlow(Kind kind)
    {
        GameplayModalBlocker.Push();
        Time.timeScale = 0f;

        var cam = worldCameraForFailureFx != null ? worldCameraForFailureFx : Camera.main;
        if (cam != null)
        {
            _cachedDesat = cam.GetComponent<FailureWorldDesaturateEffect>();
            if (_cachedDesat == null)
                _cachedDesat = cam.gameObject.AddComponent<FailureWorldDesaturateEffect>();
            _cachedDesat.targetCamera = cam;
            _cachedDesat.ApplyTuning(failureWorldSaturation, failureWorldPostExposure);
            _cachedDesat.SetWorldFailureActive(true);
        }

        var sprite = GetSprite(kind);
        var title = GetTitle(kind);
        var clip = GetSfx(kind);

        EnsureSfx();
        if (clip != null)
            _sfx.PlayOneShot(clip, 1f);

        var overlay = FailureCardRevealOverlay.GetOrCreate();
        if (overlay != null)
        {
            yield return StartCoroutine(overlay.RunPresentation(
                sprite,
                title,
                failureDimColor,
                delayBeforeSpacePromptSeconds,
                spacePromptText));
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(PlayerFailureController)}: 无法创建 {nameof(FailureCardRevealOverlay)}（未找到 Popups / Canvas）。请仍按 Space 或点击以回档。");
            if (delayBeforeSpacePromptSeconds > 0f)
                yield return new WaitForSecondsRealtime(delayBeforeSpacePromptSeconds);
            while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetMouseButtonDown(0))
                yield return null;
        }

        if (checkpoint != null)
            checkpoint.RestoreLastSnapshot();

        _cachedDesat?.SetWorldFailureActive(false);

        GameplayModalBlocker.Pop();

        if (Time.timeScale < 0.001f)
            Time.timeScale = 1f;

        _failureActive = false;
        _routine = null;
    }

    Sprite GetSprite(Kind k) => k switch
    {
        Kind.DualStat => dualFailureSprite,
        Kind.Professionalism => proFailureSprite,
        Kind.Emotion => emoFailureSprite,
        Kind.WallHit => wallFailureSprite,
        _ => null
    };

    string GetTitle(Kind k) => k switch
    {
        Kind.DualStat => dualFailureTitle,
        Kind.Professionalism => proFailureTitle,
        Kind.Emotion => emoFailureTitle,
        Kind.WallHit => wallFailureTitle,
        _ => string.Empty
    };

    AudioClip GetSfx(Kind k) => k switch
    {
        Kind.DualStat => dualFailureSfx,
        Kind.Professionalism => proFailureSfx,
        Kind.Emotion => emoFailureSfx,
        Kind.WallHit => wallFailureSfx,
        _ => null
    };
}
