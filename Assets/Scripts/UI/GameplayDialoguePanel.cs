using System;
using Gameplay.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内对白：底图 + 左右头像、打字机、空格推进；全部结束后抉择 Space 接受 / Esc 拒绝。
/// </summary>
[DisallowMultipleComponent]
public class GameplayDialoguePanel : MonoBehaviour
{
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

    HospitalFetchMedicineEventConfig _config;
    Action _onAccepted;
    Action _onDeclined;

    int _lineIndex;
    int _visibleChars;
    float _lastTickTime;
    float _typewriterCharsCarry;
    bool _lineComplete;
    float _savedTimeScale = 1f;

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
    }

    void OnDestroy()
    {
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
        if (_externalSessionActive)
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
            if (Input.GetKeyDown(KeyCode.Space))
                Accept();
            else if (Input.GetKeyDown(KeyCode.Escape))
                Decline();
            return;
        }

        if (_phase != Phase.Dialogue || _config == null)
            return;

        var line = _config.dialogueLines[_lineIndex];
        var full = line.text ?? string.Empty;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!_lineComplete)
            {
                TypewriterTMP.RevealFullText(bodyText, full);
                _visibleChars = full.Length;
                _typewriterCharsCarry = 0f;
                _lineComplete = true;
            }
            else
            {
                _lineIndex++;
                if (_lineIndex >= _config.dialogueLines.Length)
                    EnterChoicePhase();
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
        }
    }

    void EnterChoicePhase()
    {
        _phase = Phase.Choice;
        if (bodyText != null)
            bodyText.text = string.Empty;
        if (choicePromptText != null)
        {
            choicePromptText.gameObject.SetActive(true);
            choicePromptText.text = _config.acceptPromptText;
        }
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
    }

    public void ForceCloseWithoutCallback()
    {
        if (_phase == Phase.Hidden && !_externalSessionActive)
            return;
        _phase = Phase.Hidden;
        _externalSessionActive = false;
        SetVisible(false);
        if (choicePromptText != null)
            choicePromptText.gameObject.SetActive(false);
        GameplayModalBlocker.Pop();
        Time.timeScale = _savedTimeScale;
        _onAccepted = null;
        _onDeclined = null;
    }

    /// <summary>外部脚本驱动的对话（如同学求助分支），不占用医院流程 Phase。</summary>
    public void ExternalSessionBegin()
    {
        if (_externalSessionActive)
            return;
        if (_phase != Phase.Hidden)
            return;

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
