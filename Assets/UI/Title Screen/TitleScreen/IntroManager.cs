using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 标题场景：叙事打字机 → 空格确认 → 过渡 + BGM → 主菜单。
/// 分段叙事、每段背景图、淡入淡出、句末闪烁点、同场景重载跳过 Intro 等。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class IntroManager : MonoBehaviour
{
    static bool sEnteredGameFromTitle;

    [Header("场景")]
    [SerializeField] string gameSceneName = "SampleScene";

    [Header("面板引用（Canvas 子物体）")]
    [SerializeField] GameObject introUiRoot;
    [SerializeField] GameObject narrativePanel;
    [SerializeField] GameObject promptPanel;
    [SerializeField] GameObject transitionPanel;
    [SerializeField] GameObject menuPanel;

    [Header("阶段 1 — 叙事")]
    [TextArea(3, 16)]
    [SerializeField] string narrativeSegment1 = "";
    [TextArea(3, 16)]
    [SerializeField] string narrativeSegment2 = "";
    [TextArea(3, 16)]
    [SerializeField] string narrativeSegment3 = "";
    [Tooltip("每一段打字结束后，整段文字继续停留在屏幕上的秒数（此期间句末显示闪烁小点）")]
    [SerializeField] float segmentHoldSeconds = 1.2f;
    [Tooltip("句末小点一次明/暗闪烁周期（秒）")]
    [SerializeField] float dotBlinkPeriodSeconds = 0.45f;
    [TextArea(3, 24)]
    [Tooltip("兼容：仅当上面三段都为空时使用整段叙事")]
    [SerializeField] string narrativeText = "";
    [SerializeField] Image narrativeBackgroundImage;
    [SerializeField] Sprite narrativeBackgroundSprite1;
    [SerializeField] Sprite narrativeBackgroundSprite2;
    [SerializeField] Sprite narrativeBackgroundSprite3;
    [Tooltip("仅 narrativeText 单段模式使用；留空则沿用段 1 的图")]
    [SerializeField] Sprite narrativeBackgroundSpriteLegacy;
    [Tooltip("每段背景从暗到亮（淡入）完成后，再等待多少秒才开始打该段文字（文字在此之前保持透明）")]
    [SerializeField] float backgroundLeadInSeconds = 0.6f;
    [Header("背景淡入 — 首图单独")]
    [Tooltip("仅第一张背景图由暗变亮的时长（秒）；0 为瞬间亮起")]
    [SerializeField] float firstBackgroundFadeInSeconds = 0.45f;
    [Tooltip("第二段及之后每张背景由暗变亮的时长（秒）；0 为瞬间亮起")]
    [SerializeField] float backgroundFadeInSeconds = 0.45f;
    [Tooltip("每段结束前背景由亮到暗的时长（秒），下一段再淡入新图；0 为瞬间变暗")]
    [SerializeField] float backgroundFadeOutSeconds = 0.45f;
    [SerializeField] TextMeshProUGUI narrativeBody;
    [Tooltip("留空则通过 UiCjkFontProvider 使用系统中文或 Resources/CjkUiFont")]
    [SerializeField] TMP_FontAsset narrativeFontOverride;
    [SerializeField] float charsPerSecond = 28f;
    [SerializeField] AudioClip typewriterTickClip;
    [Range(0f, 1f)]
    [SerializeField] float typewriterSfxVolume = 0.85f;
    [Tooltip("两次打字音效之间的最短间隔（秒）")]
    [SerializeField] float minTypewriterTickInterval = 0.04f;
    [SerializeField] float delayAfterNarrativeSeconds = 0.35f;

    [Header("阶段 2 — 确认")]
    [SerializeField] TextMeshProUGUI promptBody;
    [TextArea(1, 4)]
    [SerializeField] string promptMessage = "按 空格 确认任务";
    [SerializeField] KeyCode confirmKey = KeyCode.Space;

    [Header("阶段 3 — 过渡与 BGM")]
    [SerializeField] AudioClip bgmClip;
    [Range(0f, 1f)]
    [SerializeField] float bgmVolume = 0.7f;
    [SerializeField] bool loopBgm = true;
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;
    [SerializeField] float transitionHoldSeconds = 1.2f;
    [Tooltip("进入主菜单前是否停止 BGM")]
    [SerializeField] bool stopBgmWhenShowingMainMenu;
    [SerializeField] CanvasGroup transitionCanvasGroup;
    [SerializeField] float transitionFadeInDuration = 0.35f;
    [SerializeField] float transitionFadeOutDuration = 0.45f;

    [Header("阶段 4 — 主菜单")]
    [SerializeField] Button startButton;
    [SerializeField] Button quitButton;

    [Header("调试")]
    [SerializeField] bool debugSkipToMenu;

    Coroutine _flowCo;
    bool _loadingGame;
    float _lastTickTime = -999f;

    void Awake()
    {
        var active = SceneManager.GetActiveScene();
        if (active.name != gameSceneName)
            sEnteredGameFromTitle = false;

        if (sEnteredGameFromTitle && active.name == gameSceneName)
        {
            DisableIntroUiRoot();
            Destroy(gameObject);
            return;
        }

        EnsureAudioSources();
        ApplyFonts();
    }

    void Start()
    {
        EnsureEventSystem();

        if (debugSkipToMenu)
        {
            ShowPanelOnly(menuPanel);
            WireMenuButtons();
            return;
        }

        if (narrativePanel == null || narrativeBody == null)
        {
            Debug.LogError("[IntroManager] 请指定 narrativePanel 与 narrativeBody（TextMeshProUGUI）。", this);
            enabled = false;
            return;
        }

        _flowCo = StartCoroutine(RunIntroFlow());
    }

    void OnDestroy()
    {
        if (_flowCo != null)
            StopCoroutine(_flowCo);
    }

    void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = loopBgm;
        }

        if (sfxSource == null)
        {
            var go = new GameObject("IntroSfx");
            go.transform.SetParent(transform, false);
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }
    }

    void ApplyFonts()
    {
        var cjk = UiCjkFontProvider.GetOrCreateRuntimeCjkFont();
        var primary = narrativeFontOverride;
        if (primary == null && TMP_Settings.defaultFontAsset != null)
            primary = TMP_Settings.defaultFontAsset;
        if (primary == null)
            primary = cjk;

        if (narrativeBody != null && primary != null)
        {
            narrativeBody.richText = true;
            narrativeBody.font = primary;
            UiCjkFontProvider.EnsureCjkFallback(narrativeBody.font, cjk);
        }

        if (promptBody != null && primary != null)
        {
            promptBody.font = primary;
            UiCjkFontProvider.EnsureCjkFallback(promptBody.font, cjk);
        }

        if (menuPanel != null && primary != null)
        {
            foreach (var tmp in menuPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                tmp.font = primary;
                UiCjkFontProvider.EnsureCjkFallback(tmp.font, cjk);
            }
        }

        if (cjk != null)
        {
            var warm = string.Concat(
                narrativeSegment1 ?? "",
                narrativeSegment2 ?? "",
                narrativeSegment3 ?? "",
                narrativeText ?? "",
                promptMessage ?? "");
            UiCjkFontProvider.WarmAtlas(cjk, warm);
        }
    }

    IEnumerator RunIntroFlow()
    {
        PrimeNarrativeBackgroundBeforeReveal();
        ShowPanelOnly(narrativePanel);
        narrativeBody.text = "";
        yield return NarrativeSegmentsRoutine();

        if (delayAfterNarrativeSeconds > 0f)
            yield return new WaitForSecondsRealtime(delayAfterNarrativeSeconds);

        if (promptPanel != null && promptBody != null)
        {
            narrativePanel.SetActive(false);
            promptPanel.SetActive(true);
            promptBody.text = promptMessage;
        }
        else
        {
            narrativePanel.SetActive(true);
            if (promptBody != null)
                promptBody.text = promptMessage;
        }

        yield return null;
        while (!Input.GetKeyDown(confirmKey))
            yield return null;

        if (promptPanel != null)
            promptPanel.SetActive(false);

        if (transitionPanel != null)
            transitionPanel.SetActive(true);

        PlayBgm();

        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.alpha = 0f;
            yield return FadeCanvasGroup(transitionCanvasGroup, 0f, 1f, transitionFadeInDuration);
            var hold = Mathf.Max(0f, transitionHoldSeconds - transitionFadeInDuration - transitionFadeOutDuration);
            if (hold > 0f)
                yield return new WaitForSecondsRealtime(hold);
            yield return FadeCanvasGroup(transitionCanvasGroup, 1f, 0f, transitionFadeOutDuration);
        }
        else
            yield return new WaitForSecondsRealtime(transitionHoldSeconds);

        if (transitionPanel != null)
            transitionPanel.SetActive(false);

        if (stopBgmWhenShowingMainMenu)
            StopBgmIfConfigured();

        ShowPanelOnly(menuPanel);
        WireMenuButtons();
    }

    IEnumerator NarrativeSegmentsRoutine()
    {
        var chunks = BuildNarrativeChunks();
        for (var i = 0; i < chunks.Count; i++)
        {
            var text = chunks[i].text;
            var segmentKey = chunks[i].originalSegmentIndex;

            narrativeBody.text = "";
            narrativeBody.alpha = 0f;

            ApplyNarrativeBackgroundForSegment(segmentKey);
            yield return FadeNarrativeBackgroundIn(i == 0);

            if (backgroundLeadInSeconds > 0f)
                yield return WaitUnscaledSeconds(backgroundLeadInSeconds);

            narrativeBody.alpha = 1f;
            yield return TypewriterRoutine(text);
            yield return SegmentHoldWithBlinkingDot(text);
            narrativeBody.text = "";

            if (i < chunks.Count - 1)
                yield return FadeNarrativeBackgroundOut();
        }

        yield return FadeNarrativeBackgroundOut();
    }

    void PrimeNarrativeBackgroundBeforeReveal()
    {
        if (narrativeBackgroundImage == null)
            return;
        if (narrativeBackgroundImage.sprite != null)
            narrativeBackgroundImage.color = Color.black;
        else
            narrativeBackgroundImage.color = new Color(0f, 0f, 0f, 1f);
    }

    static IEnumerator WaitUnscaledSeconds(float seconds)
    {
        if (seconds <= 0f)
            yield break;
        var start = Time.unscaledTime;
        while (Time.unscaledTime - start < seconds)
            yield return null;
    }

    IEnumerator FadeNarrativeBackgroundIn(bool isFirstSegment)
    {
        if (narrativeBackgroundImage == null)
            yield break;

        var img = narrativeBackgroundImage;
        if (img.sprite == null)
        {
            img.color = new Color(0f, 0f, 0f, 1f);
            yield break;
        }

        var dur = isFirstSegment ? firstBackgroundFadeInSeconds : backgroundFadeInSeconds;
        if (dur <= 0f)
        {
            img.color = Color.white;
            yield break;
        }

        img.color = Color.black;
        yield return null;

        var start = Time.unscaledTime;
        while (Time.unscaledTime - start < dur)
        {
            var k = Mathf.Clamp01((Time.unscaledTime - start) / dur);
            var s = Mathf.SmoothStep(0f, 1f, k);
            img.color = Color.Lerp(Color.black, Color.white, s);
            yield return null;
        }

        img.color = Color.white;
    }

    IEnumerator FadeNarrativeBackgroundOut()
    {
        if (narrativeBackgroundImage == null)
            yield break;

        var img = narrativeBackgroundImage;
        if (img.sprite == null)
        {
            img.color = new Color(0f, 0f, 0f, 1f);
            yield break;
        }

        if (backgroundFadeOutSeconds <= 0f)
        {
            img.color = Color.black;
            yield break;
        }

        var from = img.color;
        yield return null;

        var start = Time.unscaledTime;
        var dur = backgroundFadeOutSeconds;
        while (Time.unscaledTime - start < dur)
        {
            var k = Mathf.Clamp01((Time.unscaledTime - start) / dur);
            var s = Mathf.SmoothStep(0f, 1f, k);
            img.color = Color.Lerp(from, Color.black, s);
            yield return null;
        }

        img.color = Color.black;
    }

    List<(string text, int originalSegmentIndex)> BuildNarrativeChunks()
    {
        var result = new List<(string, int)>();
        var a = narrativeSegment1 != null ? narrativeSegment1.Trim() : "";
        var b = narrativeSegment2 != null ? narrativeSegment2.Trim() : "";
        var c = narrativeSegment3 != null ? narrativeSegment3.Trim() : "";

        if (!string.IsNullOrEmpty(a) || !string.IsNullOrEmpty(b) || !string.IsNullOrEmpty(c))
        {
            if (!string.IsNullOrEmpty(a))
                result.Add((a, 0));
            if (!string.IsNullOrEmpty(b))
                result.Add((b, 1));
            if (!string.IsNullOrEmpty(c))
                result.Add((c, 2));
            return result;
        }

        var legacy = narrativeText != null ? narrativeText.Trim() : "";
        if (!string.IsNullOrEmpty(legacy))
            result.Add((legacy, -1));
        return result;
    }

    void ApplyNarrativeBackgroundForSegment(int originalSegmentIndex)
    {
        if (narrativeBackgroundImage == null)
            return;

        Sprite sp;
        if (originalSegmentIndex < 0)
            sp = narrativeBackgroundSpriteLegacy != null ? narrativeBackgroundSpriteLegacy : narrativeBackgroundSprite1;
        else
        {
            sp = originalSegmentIndex switch
            {
                0 => narrativeBackgroundSprite1,
                1 => narrativeBackgroundSprite2,
                2 => narrativeBackgroundSprite3,
                _ => narrativeBackgroundSprite3
            };
        }

        narrativeBackgroundImage.sprite = sp;
        narrativeBackgroundImage.color = sp != null ? Color.black : new Color(0f, 0f, 0f, 1f);
    }

    IEnumerator SegmentHoldWithBlinkingDot(string baseText)
    {
        if (string.IsNullOrEmpty(baseText) || narrativeBody == null)
            yield break;

        if (segmentHoldSeconds <= 0f)
            yield break;

        var period = Mathf.Max(0.08f, dotBlinkPeriodSeconds);
        var elapsed = 0f;

        while (elapsed < segmentHoldSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.PingPong(elapsed * 2f / period, 1f);
            var aByte = (int)(t * 255f);
            var aHex = aByte.ToString("X2");
            narrativeBody.text = baseText + "<color=#FFFFFF" + aHex + ">·</color>";
            yield return null;
        }

        narrativeBody.text = baseText;
    }

    IEnumerator TypewriterRoutine(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var delay = charsPerSecond > 0.01f ? 1f / charsPerSecond : 0f;
        var sb = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            sb.Append(ch);
            narrativeBody.text = sb.ToString();
            PlayTypewriterTick();
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }
    }

    void PlayTypewriterTick()
    {
        if (typewriterTickClip == null || sfxSource == null)
            return;

        if (Time.unscaledTime - _lastTickTime < minTypewriterTickInterval)
            return;

        _lastTickTime = Time.unscaledTime;
        sfxSource.PlayOneShot(typewriterTickClip, typewriterSfxVolume);
    }

    void PlayBgm()
    {
        if (bgmClip == null || musicSource == null)
            return;

        musicSource.clip = bgmClip;
        musicSource.volume = bgmVolume;
        musicSource.loop = loopBgm;
        musicSource.Play();
    }

    void StopBgmIfConfigured()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
    }

    static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null || duration <= 0f)
        {
            if (cg != null)
                cg.alpha = to;
            yield break;
        }

        var t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        cg.alpha = to;
    }

    void ShowPanelOnly(GameObject only)
    {
        if (narrativePanel != null)
            narrativePanel.SetActive(narrativePanel == only);
        if (promptPanel != null)
            promptPanel.SetActive(promptPanel == only);
        if (transitionPanel != null)
            transitionPanel.SetActive(transitionPanel == only);
        if (menuPanel != null)
            menuPanel.SetActive(menuPanel == only);
    }

    void WireMenuButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClicked);
        }
        else
            Debug.LogWarning("[IntroManager] 未指定 startButton。", this);

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    void DisableIntroUiRoot()
    {
        var root = introUiRoot;
        if (root == null)
        {
            var go = GameObject.Find("IntroCanvas");
            if (go != null)
                root = go;
        }

        if (root != null)
            root.SetActive(false);
    }

    void OnStartClicked()
    {
        if (_loadingGame)
            return;

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[IntroManager] gameSceneName 为空。", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                "[IntroManager] 无法加载场景 \"" + gameSceneName + "\"。请加入 Build Settings。",
                this);
            return;
        }

        _loadingGame = true;
        DisableIntroUiRoot();
        sEnteredGameFromTitle = true;
        SceneManager.LoadScene(gameSceneName);
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
