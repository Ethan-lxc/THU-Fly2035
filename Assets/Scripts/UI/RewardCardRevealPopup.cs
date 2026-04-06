using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 奖励卡全屏弹出：先展示固定时长（<see cref="previewDurationSeconds"/>，按非缩放时间计时），再提示空格将卡存入成就台。
/// </summary>
[DisallowMultipleComponent]
public class RewardCardRevealPopup : MonoBehaviour
{
    /// <summary>仅当调用方未指定 storagePrefsKey 且 Inspector 也未填时使用。</summary>
    public const string GenericFallbackStorageKey = "RewardCard_Fallback";

    [Header("UI（可选；为空时在 Awake 下自动生成子物体）")]
    public CanvasGroup rootCanvasGroup;
    public Image dimImage;
    public Image cardImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI hintText;

    [Header("流程")]
    public float previewDurationSeconds = 3f;

    [TextArea(1, 2)]
    public string hintAfterPreview = "按 空格 将奖励存入成就台";

    [Header("音频（预览阶段；Time.timeScale=0 时仍会播放）")]
    [Tooltip("弹出后、按空格前\"等待展示\"这段时间播放的音效；可拖你的 AudioClip")]
    public AudioClip previewWaitClip;

    [Tooltip("勾选：预览时长内循环播放；取消：仅播放一次")]
    public bool loopPreviewAudio = true;

    [Tooltip("空则用本物体上的 AudioSource，再没有则自动添加")]
    public AudioSource previewAudioSource;

    [Header("入库")]
    public AchievementPanelController achievementPanel;

    [Tooltip("当 Show() 未传入 storagePrefsKey 时作为去重键（一般请由各事件传入唯一键）")]
    public string storedPlayerPrefsKey = GenericFallbackStorageKey;

    enum Phase
    {
        Hidden,
        Preview,
        WaitSpace
    }

    Phase _phase = Phase.Hidden;
    float _previewElapsed;
    float _savedTimeScale;
    /// <summary>Show() 时是否由本弹窗把 timeScale 从非零改成 0。若在对话暂停（已是 0）期间打开，则为 false，Close 时不改写 timeScale，避免把外层已恢复的流速再次置 0。</summary>
    bool _ownsTimeScalePause;
    Sprite _sprite;
    string _title;
    string _activeStorageKey;
    bool _persistPlayerPrefsOnStore = true;
    bool _built;
    RectTransform _rt;
    Coroutine _previewAudioStopRoutine;
    AudioSource _resolvedPreviewSource;

    void Awake()
    {
        _rt = transform as RectTransform;
        EnsureBuilt();
        HideImmediate();
    }

    void OnDestroy()
    {
        EndPreviewPhaseAudio();
        if (_phase != Phase.Hidden)
        {
            GameplayModalBlocker.Pop();
            if (_ownsTimeScalePause)
                Time.timeScale = _savedTimeScale;
            _phase = Phase.Hidden;
        }
    }

    void BeginPreviewPhaseAudio()
    {
        EndPreviewPhaseAudio();
        if (previewWaitClip == null)
            return;

        var src = previewAudioSource != null ? previewAudioSource : GetComponent<AudioSource>();
        if (src == null)
            src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.ignoreListenerPause = true;

        _resolvedPreviewSource = src;

        if (loopPreviewAudio)
        {
            src.loop = true;
            src.clip = previewWaitClip;
            src.Play();
            if (_previewAudioStopRoutine != null)
                StopCoroutine(_previewAudioStopRoutine);
            _previewAudioStopRoutine = StartCoroutine(StopPreviewAudioAfterSeconds(previewDurationSeconds));
        }
        else
        {
            src.loop = false;
            src.PlayOneShot(previewWaitClip);
        }
    }

    IEnumerator StopPreviewAudioAfterSeconds(float seconds)
    {
        var t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_resolvedPreviewSource != null && _resolvedPreviewSource.clip == previewWaitClip && _resolvedPreviewSource.isPlaying)
            _resolvedPreviewSource.Stop();
        _previewAudioStopRoutine = null;
    }

    void EndPreviewPhaseAudio()
    {
        if (_previewAudioStopRoutine != null)
        {
            StopCoroutine(_previewAudioStopRoutine);
            _previewAudioStopRoutine = null;
        }

        if (_resolvedPreviewSource != null && _resolvedPreviewSource.clip == previewWaitClip)
            _resolvedPreviewSource.Stop();
        _resolvedPreviewSource = null;
    }

    /// <summary>打开弹出层并暂停游戏时间（与对话面板一致）。</summary>
    /// <param name="storagePrefsKey">入库去重 PlayerPrefs 键；为空则用 Inspector 的 <see cref="storedPlayerPrefsKey"/>，再退化为 <see cref="GenericFallbackStorageKey"/>。</param>
    /// <param name="persistPlayerPrefsOnStore">为 false 时按空格仍会尝试入库 UI，但不读写 PlayerPrefs（适合与 NPC 调试勾选配合）。</param>
    public void Show(Sprite cardSprite, string cardTitle, string storagePrefsKey = null, bool persistPlayerPrefsOnStore = true)
    {
        EnsureBuilt();
        _sprite = cardSprite;
        _title = cardTitle ?? string.Empty;
        _persistPlayerPrefsOnStore = persistPlayerPrefsOnStore;
        if (string.IsNullOrEmpty(storagePrefsKey))
            storagePrefsKey = string.IsNullOrEmpty(storedPlayerPrefsKey)
                ? GenericFallbackStorageKey
                : storedPlayerPrefsKey;
        _activeStorageKey = storagePrefsKey;
        if (cardImage != null)
        {
            cardImage.sprite = _sprite;
            cardImage.enabled = _sprite != null;
        }

        if (titleText != null)
        {
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(_title));
            titleText.text = _title;
        }

        if (hintText != null)
            hintText.text = string.Empty;

        _phase = Phase.Preview;
        _previewElapsed = 0f;
        _savedTimeScale = Time.timeScale;
        _ownsTimeScalePause = !Mathf.Approximately(Time.timeScale, 0f);
        GameplayModalBlocker.Push();
        if (_ownsTimeScalePause)
            Time.timeScale = 0f;

        rootCanvasGroup.alpha = 1f;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;

        BeginPreviewPhaseAudio();
    }

    void Update()
    {
        if (_phase == Phase.Hidden)
            return;

        if (_phase == Phase.Preview)
        {
            _previewElapsed += Time.unscaledDeltaTime;
            if (_previewElapsed >= previewDurationSeconds)
            {
                EndPreviewPhaseAudio();
                _phase = Phase.WaitSpace;
                if (hintText != null)
                    hintText.text = hintAfterPreview;
            }

            return;
        }

        if (_phase == Phase.WaitSpace && Input.GetKeyDown(KeyCode.Space))
            StoreAndClose();
    }

    void StoreAndClose()
    {
        var key = string.IsNullOrEmpty(_activeStorageKey)
            ? (string.IsNullOrEmpty(storedPlayerPrefsKey) ? GenericFallbackStorageKey : storedPlayerPrefsKey)
            : _activeStorageKey;

        if (_persistPlayerPrefsOnStore && PlayerPrefs.GetInt(key, 0) != 0)
        {
            Debug.Log(
                $"{nameof(RewardCardRevealPopup)}: PlayerPrefs 键 \"{key}\" 已标记为入库过，本次按空格不再重复添加到成就台（测试请删该键或换新 storagePrefsKey）。");
            Close();
            return;
        }

        if (achievementPanel == null)
            achievementPanel = FindObjectOfType<AchievementPanelController>(true);

        if (achievementPanel != null && _sprite != null)
        {
            achievementPanel.AddAchievementCard(_sprite, _title);
            if (_persistPlayerPrefsOnStore)
            {
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
            }
        }
        else if (achievementPanel == null)
            Debug.LogWarning($"{nameof(RewardCardRevealPopup)}: 未找到 {nameof(AchievementPanelController)}，成就卡无法入库；未写入 PlayerPrefs，可修好引用后再按空格重试。");
        else if (_sprite == null)
            Debug.LogWarning($"{nameof(RewardCardRevealPopup)}: 奖励 Sprite 为空；未写入 PlayerPrefs。");

        Close();
    }

    void Close()
    {
        EndPreviewPhaseAudio();
        _activeStorageKey = null;
        HideImmediate();
        GameplayModalBlocker.Pop();
        if (_ownsTimeScalePause)
            Time.timeScale = _savedTimeScale;
        _phase = Phase.Hidden;
    }

    void HideImmediate()
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }
    }

    void EnsureBuilt()
    {
        if (_built)
            return;

        if (_rt != null && (_rt.anchorMin != Vector2.zero || _rt.anchorMax != Vector2.one))
        {
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
        }

        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
                rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (dimImage == null)
        {
            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dimGo.transform.SetParent(transform, false);
            var dimRt = dimGo.GetComponent<RectTransform>();
            StretchFull(dimRt);
            dimImage = dimGo.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.72f);
            dimImage.raycastTarget = true;
        }

        if (cardImage == null || hintText == null)
            BuildCardStack();

        if (achievementPanel == null)
            achievementPanel = FindObjectOfType<AchievementPanelController>(true);

        _built = true;
    }

    void BuildCardStack()
    {
        var stack = transform.Find("RewardStack");
        if (stack == null)
        {
            var stackGo = new GameObject("RewardStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
            stack = stackGo.transform;
            stack.SetParent(transform, false);
            var stackRt = stack.GetComponent<RectTransform>();
            stackRt.anchorMin = stackRt.anchorMax = new Vector2(0.5f, 0.5f);
            stackRt.sizeDelta = new Vector2(420f, 560f);
            stackRt.anchoredPosition = Vector2.zero;
            var v = stack.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleCenter;
            v.spacing = 18f;
            v.childControlHeight = false;
            v.childControlWidth = false;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = false;
        }

        if (cardImage == null)
        {
            var cardGo = new GameObject("CardIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardGo.transform.SetParent(stack, false);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(300f, 380f);
            cardImage = cardGo.GetComponent<Image>();
            cardImage.preserveAspect = true;
            cardImage.color = Color.white;
            cardImage.raycastTarget = false;
        }

        if (titleText == null)
        {
            var tGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(stack, false);
            var tRt = tGo.GetComponent<RectTransform>();
            tRt.sizeDelta = new Vector2(400f, 48f);
            titleText = tGo.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 28f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                titleText.font = TMP_Settings.defaultFontAsset;
        }

        if (hintText == null)
        {
            var hGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hGo.transform.SetParent(stack, false);
            var hRt = hGo.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(520f, 72f);
            hintText = hGo.GetComponent<TextMeshProUGUI>();
            hintText.fontSize = 22f;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(1f, 0.95f, 0.7f);
            if (TMP_Settings.defaultFontAsset != null)
                hintText.font = TMP_Settings.defaultFontAsset;
        }
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }
}
