using System;
using System.Collections;
using Gameplay.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内对白：底图 + 左右头像、打字机、空格推进；全部结束后抉择 Space 或 E 接受 / Esc 拒绝。
/// </summary>
[DisallowMultipleComponent]
public class GameplayDialoguePanel : MonoBehaviour
{
    /// <summary>当场景中所有本类型面板的 <see cref="IsOpen"/> 均为 false 时触发（医院流程 / 外部分支结束等）。</summary>
    public static event System.Action DialogueFullyClosed;

    /// <summary>在关闭对话的代码路径末尾调用：若仍有任一实例处于打开则不会触发。</summary>
    public static void RaiseDialogueFullyClosedIfNoneOpen()
    {
        var panels = UnityEngine.Object.FindObjectsOfType<GameplayDialoguePanel>(false);
        for (var i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].IsOpen)
                return;
        }

        DialogueFullyClosed?.Invoke();
    }

    [Header("UI")]
    public CanvasGroup rootCanvasGroup;

    [Tooltip("整页底图")]
    public Image backgroundImage;

    public Image portraitLeft;
    public Image portraitRight;

    public TextMeshProUGUI bodyText;

    [Tooltip("抉择阶段提示（可与 acceptPrompt 共用或单独）")]
    public TextMeshProUGUI choicePromptText;

    public AudioSource sfxSource;

    [Header("开场独白（推荐：在此配置，无需另挂 GameplayOpeningMonologueController）")]
    [Tooltip("勾选后在本组件 Start 中协程播放；与对白同物体，避免场景里另一物体未激活导致不播。")]
    public bool playOpeningMonologueOnStart;

    [Tooltip("复用 HospitalFetchMedicineEventConfig 仅作台词数据")]
    public HospitalFetchMedicineEventConfig openingMonologueConfig;

    [Min(0f)]
    public float openingMonologueDelaySeconds = 1.15f;

    public bool openingMonologueOnlyOnce = true;

    public string openingMonologuePlayerPrefsKey = "OpeningInnerMonologue_Shown";

    [Tooltip("仅 Unity 编辑器内生效：不读、不写上述 PlayerPrefs，可反复 Play 测开场白。发布包仍会正常读写存档。")]
    public bool editorSkipOpeningMonologuePlayerPrefs = true;

    [Tooltip("每隔多久重试一次 BeginNarrativeSequence（对白可能被其它系统短暂占用）")]
    [Min(0.05f)]
    public float openingMonologueRetryIntervalSeconds = 0.35f;

    [Min(1)]
    public int openingMonologueMaxAttempts = 12;

    [Header("对白行继续提示（内置流程）")]
    [Tooltip("GameplayDialoguePanel 内置对白（BeginSequence 医院线、BeginNarrativeSequence 等）：每句台词结束后在下方显示（choicePromptText）。外接 ExternalSession 分支对白见项目规则。")]
    public string narrativeContinuePromptText = "按 Space 或 E 继续";

    [Tooltip("本句结束后延迟再显示提示（秒，unscaled，不宜过短）")]
    [Min(0f)]
    public float narrativeContinuePromptDelaySeconds = 0.55f;

    [Tooltip("提示显示后是否做透明度闪烁")]
    public bool narrativeContinuePromptBlink = true;

    [Tooltip("闪烁周期（秒），越大越慢")]
    [Min(0.05f)]
    public float narrativeContinuePromptBlinkPeriodSeconds = 1.15f;

    [Tooltip("闪烁时透明度下限（相对基准色）")]
    [Range(0.05f, 1f)]
    public float narrativeContinuePromptBlinkMinAlpha = 0.38f;

    HospitalFetchMedicineEventConfig _config;
    Action _onAccepted;
    Action _onDeclined;
    Action _onNarrativeComplete;
    bool _narrativeSequence;

    int _lineIndex;
    int _visibleChars;
    float _lastTickTime;
    float _typewriterCharsCarry;
    bool _lineComplete;
    float _savedTimeScale = 1f;

    /// <summary>与 <see cref="GameplaySceneEntranceFader"/> 全屏淡入层对齐，叙事对白需压在其上才能被看见。</summary>
    const int EntranceFadeCanvasSortOrder = 32000;

    Canvas _narrativeCanvasSortTarget;
    int _savedNarrativeCanvasSortOrder;
    bool _savedNarrativeCanvasOverrideSorting;
    bool _narrativeCanvasSortPushed;

    /// <summary>叙事提示：在 <see cref="Time.unscaledTime"/> 达到该时刻后再显示；-1 表示未排期。</summary>
    float _narrativePromptRevealAtUnscaledTime = -1f;

    Color _narrativePromptBaseColor = Color.white;

    enum Phase
    {
        Hidden,
        Dialogue,
        Choice
    }

    Phase _phase = Phase.Hidden;

    bool _externalSessionActive;

    /// <summary>医院流程、外部分支对话任一则视为打开。</summary>
    public bool IsOpen => _phase != Phase.Hidden || _externalSessionActive;

    public bool ExternalSessionActive => _externalSessionActive;

    void Awake()
    {
        EnsureSfxSource();

        if (_phase != Phase.Hidden || _externalSessionActive)
            return;

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }
        else
            gameObject.SetActive(false);

        if (choicePromptText != null)
            _narrativePromptBaseColor = choicePromptText.color;
    }

    void Start()
    {
        if (playOpeningMonologueOnStart && openingMonologueConfig != null)
            StartCoroutine(PlayOpeningMonologueRoutine());
    }

    bool ShouldUseOpeningMonologuePlayerPrefs()
    {
        if (!openingMonologueOnlyOnce || string.IsNullOrEmpty(openingMonologuePlayerPrefsKey))
            return false;
#if UNITY_EDITOR
        if (editorSkipOpeningMonologuePlayerPrefs)
            return false;
#endif
        return true;
    }

    IEnumerator PlayOpeningMonologueRoutine()
    {
        if (ShouldUseOpeningMonologuePlayerPrefs() &&
            PlayerPrefs.GetInt(openingMonologuePlayerPrefsKey, 0) != 0)
            yield break;

        if (openingMonologueConfig.dialogueLines == null || openingMonologueConfig.dialogueLines.Length == 0)
            yield break;

        yield return null;
        yield return null;

        if (openingMonologueDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(openingMonologueDelaySeconds);

        for (var attempt = 0; attempt < openingMonologueMaxAttempts; attempt++)
        {
            if (!IsOpen &&
                BeginNarrativeSequence(openingMonologueConfig, OnOpeningMonologueComplete))
                yield break;

            yield return new WaitForSecondsRealtime(openingMonologueRetryIntervalSeconds);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning(
            "GameplayDialoguePanel: 开场独白多次尝试后仍未开始，可能被其它逻辑占用对白面板。",
            this);
#endif
    }

    void OnOpeningMonologueComplete()
    {
        if (!ShouldUseOpeningMonologuePlayerPrefs())
            return;
        PlayerPrefs.SetInt(openingMonologuePlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 场景中未拖 AudioSource 时，打字音效会被 TypewriterTMP 静默跳过；运行时补齐 2D 音源。
    /// </summary>
    void EnsureSfxSource()
    {
        if (sfxSource != null)
            return;

        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 1f;
    }

    void OnDestroy()
    {
        PopNarrativeCanvasSortIfNeeded();

        if (_externalSessionActive)
        {
            GameplayModalBlocker.Pop();
            Time.timeScale = _savedTimeScale;
            _externalSessionActive = false;
        }
        else if (_phase != Phase.Hidden)
        {
            GameplayModalBlocker.Pop();
            Time.timeScale = _savedTimeScale;
        }
    }

    public void BeginSequence(HospitalFetchMedicineEventConfig config, Action onAccepted, Action onDeclined)
    {
        EnsureSfxSource();

        if (_externalSessionActive)
        {
            onDeclined?.Invoke();
            return;
        }

        if (_phase != Phase.Hidden)
        {
            onDeclined?.Invoke();
            return;
        }

        if (config == null || config.dialogueLines == null || config.dialogueLines.Length == 0)
        {
            onDeclined?.Invoke();
            return;
        }

        _config = config;
        _onAccepted = onAccepted;
        _onDeclined = onDeclined;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        GameplayModalBlocker.Push();

        if (choicePromptText != null)
            choicePromptText.gameObject.SetActive(false);

        SetVisible(true);
        _phase = Phase.Dialogue;
        _lineIndex = 0;
        StartCurrentLine();
    }

    /// <summary>
    /// 纯叙事：按 <see cref="HospitalFetchMedicineEventConfig.dialogueLines"/> 逐段打字与空格推进，结束后直接关闭，不进入接受/拒绝抉择阶段（适合开场内心独白等）。
    /// </summary>
    /// <returns>是否成功开始；失败时调用方可在稍后重试。</returns>
    public bool BeginNarrativeSequence(HospitalFetchMedicineEventConfig config, Action onComplete)
    {
        EnsureSfxSource();

        if (_externalSessionActive || _phase != Phase.Hidden)
            return false;

        if (config == null || config.dialogueLines == null || config.dialogueLines.Length == 0)
            return false;

        _config = config;
        _onAccepted = null;
        _onDeclined = null;
        _onNarrativeComplete = onComplete;
        _narrativeSequence = true;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        GameplayModalBlocker.Push();

        if (choicePromptText != null)
            choicePromptText.gameObject.SetActive(false);

        PushNarrativeCanvasAboveEntranceFade();

        SetVisible(true);
        _phase = Phase.Dialogue;
        _lineIndex = 0;
        StartCurrentLine();
        return true;
    }

    void PushNarrativeCanvasAboveEntranceFade()
    {
        PopNarrativeCanvasSortIfNeeded();

        var c = GetComponentInParent<Canvas>();
        if (c == null)
            return;

        _narrativeCanvasSortTarget = c;
        _savedNarrativeCanvasSortOrder = c.sortingOrder;
        _savedNarrativeCanvasOverrideSorting = c.overrideSorting;

        if (c.sortingOrder < EntranceFadeCanvasSortOrder + 1)
        {
            c.overrideSorting = true;
            c.sortingOrder = EntranceFadeCanvasSortOrder + 1;
            _narrativeCanvasSortPushed = true;
        }
    }

    void PopNarrativeCanvasSortIfNeeded()
    {
        if (!_narrativeCanvasSortPushed || _narrativeCanvasSortTarget == null)
        {
            _narrativeCanvasSortPushed = false;
            _narrativeCanvasSortTarget = null;
            return;
        }

        _narrativeCanvasSortTarget.sortingOrder = _savedNarrativeCanvasSortOrder;
        _narrativeCanvasSortTarget.overrideSorting = _savedNarrativeCanvasOverrideSorting;
        _narrativeCanvasSortPushed = false;
        _narrativeCanvasSortTarget = null;
    }

    void SetVisible(bool v)
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = v ? 1f : 0f;
            rootCanvasGroup.interactable = v;
            rootCanvasGroup.blocksRaycasts = v;
        }
        else
            gameObject.SetActive(v);
    }

    void StartCurrentLine()
    {
        _visibleChars = 0;
        _lastTickTime = 0f;
        _typewriterCharsCarry = 0f;
        _lineComplete = false;
        var line = _config.dialogueLines[_lineIndex];
        if (bodyText != null)
            bodyText.text = string.Empty;

        ApplyPortraits(line);
        _narrativePromptRevealAtUnscaledTime = -1f;
        RefreshDialogueLineContinuePrompt();
    }

    void ScheduleDialogueLineContinuePromptReveal()
    {
        _narrativePromptRevealAtUnscaledTime =
            Time.unscaledTime + Mathf.Max(0f, narrativeContinuePromptDelaySeconds);
    }

    void RestoreNarrativePromptBaseColor()
    {
        if (choicePromptText == null)
            return;
        var c = choicePromptText.color;
        c.r = _narrativePromptBaseColor.r;
        c.g = _narrativePromptBaseColor.g;
        c.b = _narrativePromptBaseColor.b;
        c.a = _narrativePromptBaseColor.a;
        choicePromptText.color = c;
    }

    void ApplyNarrativePromptBlinkAlpha()
    {
        if (choicePromptText == null || !choicePromptText.gameObject.activeSelf)
            return;

        if (!narrativeContinuePromptBlink)
        {
            RestoreNarrativePromptBaseColor();
            return;
        }

        var t = Mathf.PingPong(
            Time.unscaledTime * 2f / Mathf.Max(0.05f, narrativeContinuePromptBlinkPeriodSeconds),
            1f);
        var aMul = Mathf.Lerp(narrativeContinuePromptBlinkMinAlpha, 1f, t);
        var col = choicePromptText.color;
        col.r = _narrativePromptBaseColor.r;
        col.g = _narrativePromptBaseColor.g;
        col.b = _narrativePromptBaseColor.b;
        col.a = _narrativePromptBaseColor.a * aMul;
        choicePromptText.color = col;
    }

    /// <summary>内置对白：打字中隐藏；本句结束后延迟再显示「按 Space 继续」并闪烁（与叙事/医院 BeginSequence 共用）。</summary>
    void RefreshDialogueLineContinuePrompt()
    {
        if (choicePromptText == null || _phase != Phase.Dialogue || _config == null)
            return;

        if (!_lineComplete)
        {
            choicePromptText.gameObject.SetActive(false);
            RestoreNarrativePromptBaseColor();
            return;
        }

        if (_narrativePromptRevealAtUnscaledTime < 0f ||
            Time.unscaledTime < _narrativePromptRevealAtUnscaledTime)
        {
            choicePromptText.gameObject.SetActive(false);
            return;
        }

        choicePromptText.gameObject.SetActive(true);
        choicePromptText.text = string.IsNullOrEmpty(narrativeContinuePromptText)
            ? "按 Space 继续"
            : narrativeContinuePromptText;
        ApplyNarrativePromptBlinkAlpha();
    }

    void ApplyPortraits(DialogueLine line)
    {
        if (line.speaker == DialogueSpeaker.Npc)
        {
            var spr = line.overridePortrait != null ? line.overridePortrait : _config.defaultNpcPortrait;
            if (portraitRight != null)
            {
                portraitRight.sprite = spr;
                portraitRight.enabled = spr != null;
            }
            if (portraitLeft != null)
                portraitLeft.enabled = false;
        }
        else
        {
            var spr = line.overridePortrait != null ? line.overridePortrait : _config.defaultPlayerPortrait;
            if (portraitLeft != null)
            {
                portraitLeft.sprite = spr;
                portraitLeft.enabled = spr != null;
            }
            if (portraitRight != null)
                portraitRight.enabled = false;
        }
    }

    void Update()
    {
        if (_externalSessionActive)
            return;

        if (_phase == Phase.Hidden)
            return;

        if (_phase == Phase.Choice)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
                Accept();
            else if (Input.GetKeyDown(KeyCode.Escape))
                Decline();
            return;
        }

        if (_phase != Phase.Dialogue || _config == null)
            return;

        var line = _config.dialogueLines[_lineIndex];
        var full = line.text ?? string.Empty;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
        {
            if (!_lineComplete)
            {
                TypewriterTMP.RevealFullText(bodyText, full);
                _visibleChars = full.Length;
                _typewriterCharsCarry = 0f;
                _lineComplete = true;
                ScheduleDialogueLineContinuePromptReveal();
                RefreshDialogueLineContinuePrompt();
            }
            else
            {
                _lineIndex++;
                if (_lineIndex >= _config.dialogueLines.Length)
                {
                    if (_narrativeSequence)
                        FinishNarrativeSequence();
                    else
                        EnterChoicePhase();
                }
                else
                    StartCurrentLine();
            }
            return;
        }

        if (!_lineComplete && bodyText != null)
        {
            _lineComplete = TypewriterTMP.Step(
                bodyText,
                full,
                ref _visibleChars,
                _config.charsPerSecond,
                _config.minTypewriterTickInterval,
                ref _lastTickTime,
                ref _typewriterCharsCarry,
                _config.typewriterTickClip,
                sfxSource,
                _config.typewriterSfxVolume,
                Time.unscaledDeltaTime);
            if (_lineComplete)
                ScheduleDialogueLineContinuePromptReveal();
        }

        RefreshDialogueLineContinuePrompt();
    }

    void EnterChoicePhase()
    {
        _phase = Phase.Choice;
        if (bodyText != null)
            bodyText.text = string.Empty;
        if (choicePromptText != null)
        {
            RestoreNarrativePromptBaseColor();
            choicePromptText.gameObject.SetActive(true);
            choicePromptText.text = _config.acceptPromptText;
        }
    }

    void FinishNarrativeSequence()
    {
        PopNarrativeCanvasSortIfNeeded();

        _narrativeSequence = false;
        _phase = Phase.Hidden;
        SetVisible(false);
        if (choicePromptText != null)
        {
            choicePromptText.gameObject.SetActive(false);
            RestoreNarrativePromptBaseColor();
        }
        _narrativePromptRevealAtUnscaledTime = -1f;
        GameplayModalBlocker.Pop();
        Time.timeScale = _savedTimeScale;
        var cb = _onNarrativeComplete;
        _onNarrativeComplete = null;
        cb?.Invoke();
        RaiseDialogueFullyClosedIfNoneOpen();
    }

    void Accept()
    {
        _phase = Phase.Hidden;
        SetVisible(false);
        if (choicePromptText != null)
            choicePromptText.gameObject.SetActive(false);
        GameplayModalBlocker.Pop();
        Time.timeScale = _savedTimeScale;
        var cb = _onAccepted;
        _onAccepted = null;
        _onDeclined = null;
        cb?.Invoke();
        RaiseDialogueFullyClosedIfNoneOpen();
    }

    void Decline()
    {
        _phase = Phase.Hidden;
        SetVisible(false);
        if (choicePromptText != null)
            choicePromptText.gameObject.SetActive(false);
        GameplayModalBlocker.Pop();
        Time.timeScale = _savedTimeScale;
        var cb = _onDeclined;
        _onAccepted = null;
        _onDeclined = null;
        cb?.Invoke();
        RaiseDialogueFullyClosedIfNoneOpen();
    }

    public void ForceCloseWithoutCallback()
    {
        if (_phase == Phase.Hidden && !_externalSessionActive)
            return;
        PopNarrativeCanvasSortIfNeeded();
        _phase = Phase.Hidden;
        _externalSessionActive = false;
        _narrativeSequence = false;
        SetVisible(false);
        if (choicePromptText != null)
        {
            choicePromptText.gameObject.SetActive(false);
            RestoreNarrativePromptBaseColor();
        }
        _narrativePromptRevealAtUnscaledTime = -1f;
        GameplayModalBlocker.Pop();
        Time.timeScale = _savedTimeScale;
        _onAccepted = null;
        _onDeclined = null;
        _onNarrativeComplete = null;
        RaiseDialogueFullyClosedIfNoneOpen();
    }

    /// <summary>外部脚本驱动的对话（如同学求助分支），不占用医院流程 Phase。</summary>
    public void ExternalSessionBegin()
    {
        ExternalSessionBegin(interactionOwner: null);
    }

    /// <summary>与 <see cref="ExternalSessionBegin()"/> 相同，并在支持时写入 <see cref="GameplayInteractionCheckpoint"/> 快照。</summary>
    public void ExternalSessionBegin(MonoBehaviour interactionOwner)
    {
        EnsureSfxSource();

        if (_externalSessionActive)
            return;
        if (_phase != Phase.Hidden)
            return;

        if (interactionOwner != null && GameplayInteractionCheckpoint.Instance != null)
            GameplayInteractionCheckpoint.Instance.PushForNpcInteraction(interactionOwner);

        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        GameplayModalBlocker.Push();

        if (choicePromptText != null)
            choicePromptText.gameObject.SetActive(false);

        SetVisible(true);
        _externalSessionActive = true;
    }

    public void ExternalSessionEnd()
    {
        if (!_externalSessionActive)
            return;

        _externalSessionActive = false;
        SetVisible(false);
        if (choicePromptText != null)
            choicePromptText.gameObject.SetActive(false);
        if (bodyText != null)
            bodyText.text = string.Empty;

        GameplayModalBlocker.Pop();
        Time.timeScale = _savedTimeScale;
        RaiseDialogueFullyClosedIfNoneOpen();
    }

    public void ExternalApplyPortraits(DialogueSpeaker speaker, Sprite npcSprite, Sprite droneSprite)
    {
        if (speaker == DialogueSpeaker.Npc)
        {
            var spr = npcSprite;
            if (portraitRight != null)
            {
                portraitRight.sprite = spr;
                portraitRight.enabled = spr != null;
            }
            if (portraitLeft != null)
                portraitLeft.enabled = false;
        }
        else
        {
            var spr = droneSprite;
            if (portraitLeft != null)
            {
                portraitLeft.sprite = spr;
                portraitLeft.enabled = spr != null;
            }
            if (portraitRight != null)
                portraitRight.enabled = false;
        }
    }

    /// <summary>
    /// 仅在右侧槽显示头像；左侧关闭。同学求助等流程可统一用此接口（NPC / 无人机都传各自 Sprite）。
    /// </summary>
    public void ExternalApplyPortraitRightOnly(Sprite sprite)
    {
        if (portraitRight != null)
        {
            portraitRight.sprite = sprite;
            portraitRight.enabled = sprite != null;
        }
        if (portraitLeft != null)
            portraitLeft.enabled = false;
    }

    /// <summary>
    /// 仅左侧显示头像（无人机等）。
    /// </summary>
    public void ExternalApplyPortraitLeftOnly(Sprite sprite)
    {
        if (portraitLeft != null)
        {
            portraitLeft.sprite = sprite;
            portraitLeft.enabled = sprite != null;
        }
        if (portraitRight != null)
            portraitRight.enabled = false;
    }

    /// <summary>确保正文 TMP 参与显示（外部流程里若曾关掉子物体可恢复）。</summary>
    public void ExternalEnsureBodyVisible()
    {
        if (bodyText != null)
            bodyText.gameObject.SetActive(true);
    }

    public void ExternalClearBody()
    {
        if (bodyText != null)
            bodyText.text = string.Empty;
    }

    public bool ExternalTypewriterStep(
        string fullText,
        ref int visibleCharCount,
        ref float charsCarry,
        ref float lastTickTime,
        float charsPerSecond,
        float minTickInterval,
        AudioClip tickClip,
        float sfxVolume)
    {
        EnsureSfxSource();

        if (bodyText == null)
            return true;

        return TypewriterTMP.Step(
            bodyText,
            fullText ?? string.Empty,
            ref visibleCharCount,
            charsPerSecond,
            minTickInterval,
            ref lastTickTime,
            ref charsCarry,
            tickClip,
            sfxSource,
            sfxVolume,
            Time.unscaledDeltaTime);
    }

    public void ExternalSetChoicePrompt(bool active, string text)
    {
        if (choicePromptText == null)
            return;

        choicePromptText.gameObject.SetActive(active);
        if (active && text != null)
            choicePromptText.text = text;
    }
}
