using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 四阶段开始界面编排（单脚本 + 协程状态机）：
/// <list type="number">
/// <item><b>阶段 1 — 叙事：</b>三段可编辑文本；每段背景图由暗→亮→（延时）→黑色半透明对话框淡入→打字机（带点）→停留→字与背景由亮→暗→下一段。</item>
/// <item><b>阶段 2 — 任务确认：</b>黑屏/提示文案，指定按键确认；整屏由亮变暗（CanvasGroup 遮罩）。</item>
/// <item><b>阶段 3 — 过渡：</b>可选开头黑屏占位 → 由暗变亮 → 预留动画/占位等待 → BGM 与可选 Animator → 由亮变暗 → 淡出清屏；过渡面板 CanvasGroup + 自定义 BGM。</item>
/// <item><b>阶段 4 — 主菜单：</b>由暗至明淡入；开始按钮可闪烁；开始/退出；BGM 可延续；按钮独立点击音效。</item>
/// </list>
/// <para>场景约定：<b>阶段 1～4 的 UI 与编排</b>应仅放在开场场景（如 <c>TitleScene</c>）中，与可玩内容场景（<see cref="gameSceneName"/>，如 <c>GameScene</c>）分离，避免 UI 射线与游戏内输入/地面点击互相穿透。点击开始后由 <see cref="OnStartClicked"/> 加载游戏场景。</para>
/// 汉字：主字体由 narrativeFontOverride 或 TMP 默认字体担任；中文字形经 <see cref="UiCjkFontProvider"/> 挂入 fallback。
/// </summary>
public class IntroManager : MonoBehaviour
{
    /// <summary>动态字集预热时追加的常见全角标点（与正文一并 TryAdd）</summary>
    const string CjkWarmupExtraPunctuation = "，。！？、；：「」『』《》（）…—·";

    #region Scene

    [Header("全局 · 场景与根节点")]
    [Tooltip("开始游戏要加载的场景名；与当前场景同名则只关闭 UI")]
    public string gameSceneName = "GameScene";

    [Tooltip("通常为 IntroCanvas；下挂阶段 1～4 UI；应置于开场场景 TitleScene")]
    public GameObject introUiRoot;

    [Tooltip("IntroCanvas 最底层全屏黑块，挡住背后 3D 场景；显示主菜单前保持；空则运行时创建")]
    public Image introGameBlocker;

    [Header("加载游戏场景 · 显示进度（可选）")]
    [Tooltip("IntroCanvas 下 DIY 的 Slider；场景中常保持勾选 Active 以便编辑。Awake 会先隐藏，仅在点击开始后、异步加载关卡时再显示")]
    public Slider loadingSceneProgressSlider;

    [Tooltip("可选：加载百分比文案；同样在开场流程中隐藏，加载时显示")]
    public TextMeshProUGUI loadingSceneProgressText;

    [Tooltip("可选：加载时全屏背景色/图；在 IntroCanvas 下设为 Stretch 全屏，颜色在 Image 上调整；层级需在 Slider 之上（Hierarchy 里排在 Slider 前面）以便条与字叠在上面")]
    public Image loadingSceneBackdrop;

    [Tooltip("进入游戏场景后全屏由暗变亮时长（秒），不受 timeScale 影响；≤0 关闭")] 
    [Min(0f)]
    public float gameSceneEntranceFadeSeconds = 0.85f;

    #endregion

    #region Phase 1 — Narrative

    [Header("阶段 1 — 叙事 · 引用")]
    public GameObject narrativePanel;

    [Tooltip("全屏或铺满的背景图")]
    public Image narrativeBackgroundImage;

    [Tooltip("叙事阶段最底层全黑衬底，避免背景图淡出时透视到游戏画面；空则运行时创建")]
    public Image narrativeBlackBacking;

    [Tooltip("是否在叙事阶段使用黑色衬底（关闭则段间可能看到场景）")]
    public bool useNarrativeBlackBackingLayer = true;

    [Tooltip("若为空则在运行时生成对话框根节点（CanvasGroup）")]
    public CanvasGroup narrativeDialogGroup;

    [Tooltip("叙事正文 TextMeshPro（场景中可命名为 NarrativeText）；锚点与位置以本物体 RectTransform 为准")]
    public TextMeshProUGUI narrativeBody;

    [Tooltip("打字时右下角跳动的小点；可空，运行时生成")]
    public TextMeshProUGUI typingDot;

    [Header("阶段 1 — 叙事 · 文本与背景图")]
    [TextArea(2, 8)] public string narrativeSegment1;
    [TextArea(2, 8)] public string narrativeSegment2;
    [TextArea(2, 8)] public string narrativeSegment3;

    public Sprite narrativeBackgroundSprite1;
    public Sprite narrativeBackgroundSprite2;
    public Sprite narrativeBackgroundSprite3;

    [Tooltip("未单独指定某段 Sprite 时的备用图")]
    public Sprite narrativeBackgroundSpriteLegacy;

    [Tooltip("有 Sprite 时是否保持宽高比")]
    [SerializeField] bool _preserveBackgroundAspect = true;

    [Header("阶段 1 — 叙事 · 时间与节奏（秒）")]
    [Tooltip("第一段：背景由暗变亮")]
    public float firstBackgroundFadeInSeconds = 0.45f;

    [Tooltip("第二、三段：背景由暗变亮")]
    public float backgroundFadeInSeconds = 0.45f;

    [Tooltip("背景亮起到对话框出现前的等待")]
    public float segmentDialogDelaySeconds = 0.5f;

    [Tooltip("黑色半透明对话框淡入")]
    public float dialogFadeInSeconds = 0.35f;

    [Tooltip("打字结束后对话框淡出")]
    public float dialogFadeOutSeconds = 0.4f;

    [Tooltip("打完字后，字与背景同时停留")]
    public float segmentHoldSeconds = 0.8f;

    [Tooltip("本段结束前背景由亮变暗（可与对话框淡出并行）")]
    public float backgroundFadeOutSeconds = 0.45f;

    [Tooltip("三段叙事结束到阶段 2 的间隔")]
    public float delayAfterNarrativeSeconds = 0.35f;

    [Header("阶段 1 — 叙事 · 对话框外观")]
    [Tooltip("对话框整体最大不透明度")]
    [Range(0f, 1f)] public float dialogMaxAlpha = 0.72f;

    [Tooltip("对话框底图颜色（通常黑半透明）")]
    public Color dialogBackdropColor = new Color(0f, 0f, 0f, 0.92f);

    [Tooltip("对话框相对父级边距（像素）")]
    public float dialogEdgePadding = 48f;

    [Tooltip("对话框（半透明底 + 正文）在叙事面板上的竖直范围：底对齐父级底边，顶边 = 父级高度×该值（约 0.33=下三分之一；1=占满整屏高度）")]
    [Range(0.1f, 1f)] public float narrativeDialogHeightScreenFraction = 1f / 3f;

    [Header("阶段 1 — 叙事 · 打字机与音效")]
    [Tooltip("打字速度（字/秒）")]
    public float charsPerSecond = 10f;

    [Tooltip("打字点闪烁周期")]
    public float dotBlinkPeriodSeconds = 0.45f;

    public AudioClip typewriterTickClip;
    [Range(0f, 1f)] public float typewriterSfxVolume = 0.85f;
    public float minTypewriterTickInterval = 0.04f;

    [Header("阶段 1 — 叙事 · 汉字 / TMP 字体")]
    [Tooltip("中文字体（TMP），挂到主字体的 fallback；空则使用 UiCjkFontProvider 运行时解析的 CJK")]
    public TMP_FontAsset narrativeChineseFontAsset;

    [Tooltip("主显示字体（如 Roboto/LiberationSans）；空则使用 TMP_Settings 默认字体，再空则整段 UI 仅用中文字体")]
    public TMP_FontAsset narrativeFontOverride;

    [Tooltip("可选：额外参与动态字集预热的字符串（与三段正文一并预热）")]
    public string narrativeText;

    [Header("阶段 1 — 叙事 · 正文样式")]
    [Tooltip("叙事正文颜色（建议在半透明黑底上用浅色）")]
    public Color narrativeTextColor = Color.white;

    [Tooltip(">0 时覆盖叙事 TMP 字号；0 保留场景/预制体上的设置")]
    [Min(0f)] public float narrativeFontSize = 0f;

    [Tooltip("正文对齐；中文多行叙事常用左上或顶对齐两端")]
    public TextAlignmentOptions narrativeTextAlignment = TextAlignmentOptions.TopLeft;

    [Tooltip("保留场景中 NarrativeText（narrativeBody）的锚点与 RectTransform；关闭后可在宽度异常时自动铺满父级")]
    public bool preserveNarrativeTextRectTransform = true;

    [Tooltip("正文相对父级内边距：x=左 y=下 z=右 w=上（像素）。运行时生成对话框或自动修复布局时使用")]
    public Vector4 narrativeTextInset = new Vector4(32f, 80f, 32f, 32f);

    [Header("阶段 1 — 叙事 · 打字点样式")]
    [Tooltip("打字点基准色（闪烁时只改透明度）")]
    public Color typingDotColor = Color.white;

    [Tooltip("紧跟在最后一个已打出字符右侧的偏移（像素）")]
    [Min(0f)] public float typingDotTrailingOffset = 4f;

    #endregion

    #region Phase 2 — Prompt

    [Header("阶段 2 — 黑屏提示 · 引用")]
    public GameObject promptPanel;
    public TextMeshProUGUI promptBody;

    [Tooltip("若为空则在 IntroCanvas 下生成全屏变暗遮罩")]
    public CanvasGroup phase2DimOverlay;

    [Header("阶段 2 — 黑屏提示 · 内容与按键")]
    public string promptMessage = "按 空格 确认任务";
    public KeyCode confirmKey = KeyCode.Space;

    [Header("阶段 2 — 黑屏提示 · 由暗变亮")]
    [Tooltip("进入阶段 2 时全屏遮罩的起始不透明度（1=全黑，再淡出到目标，形成由暗变亮）")]
    [Range(0f, 1f)] public float promptEnterOverlayStartAlpha = 1f;

    [Tooltip("遮罩从起始透明度过渡到 promptDimTargetAlpha 的时长（秒）")]
    public float promptFadeToDimDuration = 0.6f;

    [Tooltip("过渡结束后的目标不透明度（0=透明 1=全黑；通常小于起始值，画面相对变亮）")]
    [Range(0f, 1f)] public float promptDimTargetAlpha = 0.65f;

    [Tooltip("按键后遮罩淡出时长；≤0 则与 promptFadeToDimDuration 相同")]
    public float promptDimFadeOutDuration = 0f;

    [Header("阶段 2 — 黑屏提示 · 提示文案闪烁")]
    [Tooltip("是否让 promptMessage 文字闪烁")]
    public bool promptMessageBlink = true;

    [Tooltip("提示文字闪烁周期（秒）")]
    public float promptMessageBlinkPeriodSeconds = 1.2f;

    [Tooltip("闪烁时文字不透明度下限（相对 promptTextColor.a）")]
    [Range(0.05f, 1f)] public float promptMessageBlinkMinAlpha = 0.35f;

    [Header("阶段 2 — 黑屏提示 · 正文样式")]
    [Tooltip("提示文案颜色")]
    public Color promptTextColor = Color.white;

    [Tooltip(">0 时覆盖提示 TMP 字号")]
    [Min(0f)] public float promptFontSize = 0f;

    [Tooltip("提示文案对齐")]
    public TextAlignmentOptions promptTextAlignment = TextAlignmentOptions.Center;

    #endregion

    #region Phase 3 — Transition + BGM

    [Header("阶段 3 — 过渡 · 引用")]
    public GameObject transitionPanel;
    public CanvasGroup transitionCanvasGroup;

    [Tooltip("可选：过渡时播放的 Animator")]
    public Animator transitionAnimator;

    [Tooltip("过渡开始时 SetTrigger；空则跳过")]
    public string transitionAnimatorTrigger = "";

    [Header("阶段 3 — 过渡 · 由暗变亮 → 占位 → 由亮变暗")]
    [Tooltip("进入阶段 3 时遮罩起始不透明度（1=全黑遮挡，再由暗变亮）")]
    [Range(0f, 1f)] public float transitionEnterStartAlpha = 1f;

    [Tooltip("由暗变亮结束时的不透明度（0=遮罩几乎透明即「亮」；占位黑屏可设为 1 且将下方时长设为 0）")]
    [Range(0f, 1f)] public float transitionAfterBrightenAlpha = 0f;

    [FormerlySerializedAs("transitionFadeInDuration")]
    [Tooltip("遮罩从起始过渡到「变亮目标」的时长（秒）；≤0 则立即跳转")]
    public float transitionFadeDarkToBrightDuration = 0.35f;

    [Tooltip("变亮后停留时长（秒）：动画未就绪时作黑屏占位；就绪后可在此阶段接 Timeline/Animator 或子类逻辑")]
    public float transitionHoldSeconds = 1.2f;

    [Tooltip("占位结束后的目标不透明度（通常 1=再次压暗全屏）")]
    [Range(0f, 1f)] public float transitionEndDarkAlpha = 1f;

    [Tooltip("由亮变暗：当前 Alpha → 结束暗场 Alpha 的时长（秒）")]
    public float transitionFadeBrightToDarkDuration = 0.45f;

    [FormerlySerializedAs("transitionFadeOutDuration")]
    [Tooltip("最后由暗场淡出为透明并关闭过渡面板的时长（秒）")]
    public float transitionFadeOutClearDuration = 0.45f;

    [Header("阶段 3 — 过渡 · 黑屏占位（无动画时）")]
    [Tooltip("开启后：BGM/Animator 触发后，先将过渡遮罩保持全黑并停留指定时长，再执行「由暗变亮」及后续流程，用于代替尚未做好的过场动画")]
    public bool transitionBlackScreenEnabled;

    [Tooltip("黑屏停留时长（秒）；仅在上项开启且 transitionCanvasGroup 存在时生效；≤0 则不停留")]
    [Min(0f)]
    public float transitionBlackScreenSeconds = 2f;

    [Header("阶段 3 — BGM")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    public bool loopBgm = true;

    [Tooltip("播放 BGM 的 AudioSource；可留空——若已指定 bgmClip，运行时会自动在本物体上新增独立 AudioSource（与打字机/按钮音效用的 AudioSource 分离）")]
    public AudioSource musicSource;

    [Tooltip("仅在进入阶段 3（过渡）时开始播放 BGM")]
    public bool playBgmAtTransitionStart = true;

    #endregion

    #region Phase 4 — Menu

    [Header("阶段 4 — 主菜单 · 引用")]
    public GameObject menuPanel;
    public Button startButton;
    public Button quitButton;

    [Header("阶段 4 — 主菜单 · 由暗至明")]
    [Tooltip("主菜单根节点上的 CanvasGroup；空则运行时在 MenuPanel 上自动添加")]
    public CanvasGroup menuPanelCanvasGroup;

    [Tooltip("主菜单从全透明淡入至完全显示的时长（秒）；≤0 则立即显示")]
    [Min(0f)]
    public float menuFadeInFromDarkDuration = 0.55f;

    [Header("阶段 4 — 主菜单 · 开始按钮闪烁")]
    [Tooltip("进入主菜单后是否让「开始」按钮明暗闪烁以引导点击")]
    public bool startButtonBlinkEnabled = true;

    [Tooltip("开始按钮闪烁周期（秒）")]
    public float startButtonBlinkPeriodSeconds = 1.2f;

    [Tooltip("闪烁时按钮 CanvasGroup 的 alpha 下限")]
    [Range(0.05f, 1f)]
    public float startButtonBlinkMinAlpha = 0.35f;

    [Header("阶段 4 — 主菜单 · 按钮音效")]
    public AudioClip startButtonClickClip;
    public AudioClip quitButtonClickClip;
    [Range(0f, 1f)] public float menuButtonClickVolume = 1f;

    [Tooltip("进入主菜单时是否停止 BGM（否则与需求「接着放」时保持 false）")]
    public bool stopBgmWhenShowingMainMenu;

    [Tooltip("点击「开始」加载关卡前是否停止 BGM")]
    public bool stopBgmOnStartGame = true;

    #endregion

    #region Audio

    [Header("共享 · 音效播放")]
    [Tooltip("打字机与菜单按钮等一次性音效；未赋值时在本物体上添加 AudioSource")]
    public AudioSource sfxSource;

    #endregion

    #region Debug

    [Header("调试")]
    [Tooltip("跳过 1～3 阶段，直接进入主菜单（连线测试用）")]
    public bool debugSkipToMenu;

    #endregion

    static Sprite s_whiteSprite;

    float _lastTypewriterTickTime = -999f;
    bool _typingDotActive;
    Coroutine _typingDotCoroutine;

    bool _promptBlinkActive;
    Coroutine _promptBlinkCoroutine;

    bool _startButtonBlinkActive;
    Coroutine _startButtonBlinkCoroutine;
    CanvasGroup _startButtonBlinkCanvasGroup;

    void Awake()
    {
        EnsureIntroCanvasVisible();
        EnsureIntroGameBlocker();
        EnsureNarrativeDialogHierarchy();
        EnsureTypingDotParentAndStyle();
        EnsurePhase2DimOverlay();
        ApplyFonts();

        if (debugSkipToMenu)
        {
            // 与正常流程一致：保持全屏黑底，直到 ShowMainMenuEntrance 淡入完成后再关，避免主菜单 alpha=0 时闪一帧关卡
            if (narrativePanel != null) narrativePanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(false);
            if (promptPanel != null) promptPanel.SetActive(false);
            if (transitionPanel != null) transitionPanel.SetActive(false);
            SetIntroGameBlockerVisible(true);
        }
        else
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (promptPanel != null) promptPanel.SetActive(false);
            if (transitionPanel != null) transitionPanel.SetActive(false);
            SetIntroGameBlockerVisible(true);
        }

        HideLoadingSceneProgressUi();
    }

    void ResolveLoadingUiReferences()
    {
        if (introUiRoot == null) return;
        if (loadingSceneProgressSlider == null)
        {
            var tr = introUiRoot.transform.Find("Slider");
            if (tr != null)
                loadingSceneProgressSlider = tr.GetComponent<Slider>();
        }

        if (loadingSceneProgressText == null)
        {
            var tr = introUiRoot.transform.Find("LoadingPercent");
            if (tr != null)
                loadingSceneProgressText = tr.GetComponent<TextMeshProUGUI>();
        }

        if (loadingSceneBackdrop == null)
        {
            var tr = introUiRoot.transform.Find("LoadingBackdrop");
            if (tr != null)
                loadingSceneBackdrop = tr.GetComponent<Image>();
        }
    }

    void HideLoadingSceneProgressUi()
    {
        ResolveLoadingUiReferences();
        if (loadingSceneBackdrop != null)
            loadingSceneBackdrop.gameObject.SetActive(false);
        if (loadingSceneProgressSlider != null)
            loadingSceneProgressSlider.gameObject.SetActive(false);
        if (loadingSceneProgressText != null)
            loadingSceneProgressText.gameObject.SetActive(false);
    }

    IEnumerator HideLoadingUiDeferred()
    {
        yield return null;
        HideLoadingSceneProgressUi();
    }

    void Start()
    {
        // 必须先保证 SFX 占用第一个 AudioSource，再为 BGM AddComponent，否则 GetComponent<AudioSource> 会误把 BGM 源当成音效源
        EnsureSfxSource();
        ConfigureMusicSourceOnly();
        WireButtons();

        HideLoadingSceneProgressUi();
        StartCoroutine(HideLoadingUiDeferred());

        if (debugSkipToMenu)
        {
            ShowMenuImmediate();
            return;
        }

        StartCoroutine(RunIntro());
    }

    void OnDestroy()
    {
        StopAllCoroutines();
        StopStartButtonBlink();
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    void WireButtons()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    void EnsureIntroCanvasVisible()
    {
        if (introUiRoot == null) return;
        var rt = introUiRoot.GetComponent<RectTransform>();
        if (rt != null && rt.localScale.sqrMagnitude < 0.0001f)
            rt.localScale = Vector3.one;
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 整段开场期间挡住 IntroCanvas 背后场景；叙事面板关掉的瞬间也依赖此层，避免闪一帧关卡。
    /// </summary>
    void EnsureIntroGameBlocker()
    {
        if (introUiRoot == null) return;
        if (introGameBlocker == null)
        {
            var go = new GameObject("IntroGameBlocker", typeof(RectTransform));
            go.transform.SetParent(introUiRoot.transform, false);
            StretchFull(go.GetComponent<RectTransform>());
            var img = go.AddComponent<Image>();
            img.sprite = GetOrCreateWhiteSprite();
            img.color = Color.black;
            img.raycastTarget = false;
            introGameBlocker = img;
        }

        var c = introGameBlocker.color;
        c.r = c.g = c.b = 0f;
        c.a = 1f;
        introGameBlocker.color = c;
        introGameBlocker.transform.SetAsFirstSibling();
    }

    void SetIntroGameBlockerVisible(bool visible)
    {
        if (introGameBlocker == null) return;
        introGameBlocker.gameObject.SetActive(visible);
    }

    void EnsureNarrativeDialogHierarchy()
    {
        if (narrativePanel == null) return;
        var panelRt = narrativePanel.GetComponent<RectTransform>();
        if (panelRt == null) return;

        if (narrativeDialogGroup == null)
        {
            var dialogGo = new GameObject("NarrativeDialog", typeof(RectTransform));
            dialogGo.transform.SetParent(panelRt, false);
            narrativeDialogGroup = dialogGo.AddComponent<CanvasGroup>();
            narrativeDialogGroup.alpha = 0f;
            narrativeDialogGroup.blocksRaycasts = false;
            narrativeDialogGroup.interactable = false;

            var dialogRt = dialogGo.GetComponent<RectTransform>();
            ApplyNarrativeDialogRegionLayoutTo(dialogRt);

            var backdrop = new GameObject("DialogBackdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            backdrop.SetParent(dialogRt, false);
            StretchFull(backdrop);
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.sprite = GetOrCreateWhiteSprite();
            backdropImg.color = dialogBackdropColor;
            backdropImg.raycastTarget = false;

            if (narrativeBody != null)
            {
                narrativeBody.transform.SetParent(dialogRt, false);
                ApplyNarrativeBodyStretchWithInset(narrativeBody.rectTransform);
            }
        }

        if (typingDot == null && narrativeDialogGroup != null)
        {
            var dotGo = new GameObject("TypingDot", typeof(RectTransform));
            var parent = narrativeBody != null ? narrativeBody.transform : narrativeDialogGroup.transform;
            dotGo.transform.SetParent(parent, false);
            var dotRt = dotGo.GetComponent<RectTransform>();
            dotRt.localScale = Vector3.one;
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 1f);
            dotRt.pivot = new Vector2(0f, 0.5f);
            dotRt.anchoredPosition = Vector2.zero;
            dotRt.sizeDelta = new Vector2(48f, 48f);
            typingDot = dotGo.AddComponent<TextMeshProUGUI>();
            typingDot.text = "·";
            typingDot.fontSize = 42f;
            typingDot.alignment = TextAlignmentOptions.MidlineLeft;
            typingDot.enableWordWrapping = false;
            typingDot.gameObject.SetActive(false);
        }

        if (narrativeDialogGroup != null)
            ApplyNarrativeDialogRegionLayout();

        ApplyNarrativeLayerOrder();
    }

    /// <summary>
    /// 将叙事对话框（含半透明底图）限制在叙事面板底部的一条横向带内（默认下三分之一屏）。
    /// </summary>
    void ApplyNarrativeDialogRegionLayout()
    {
        if (narrativeDialogGroup == null) return;
        ApplyNarrativeDialogRegionLayoutTo(narrativeDialogGroup.GetComponent<RectTransform>());
    }

    void ApplyNarrativeDialogRegionLayoutTo(RectTransform dialogRt)
    {
        if (dialogRt == null) return;
        var h = Mathf.Clamp(narrativeDialogHeightScreenFraction, 0.05f, 1f);
        dialogRt.anchorMin = new Vector2(0f, 0f);
        dialogRt.anchorMax = new Vector2(1f, h);
        dialogRt.pivot = new Vector2(0.5f, 0.5f);
        dialogRt.anchoredPosition = Vector2.zero;
        var pad = dialogEdgePadding;
        dialogRt.offsetMin = new Vector2(pad, pad);
        dialogRt.offsetMax = new Vector2(-pad, -pad);
    }

    /// <summary>
    /// 叙事面板内绘制顺序：全黑衬底（始终不透明）→ 背景图 → 对话框。
    /// 否则每段结束背景 alpha=0 时会透视到 NarrativePanel 背后的游戏画面。
    /// </summary>
    void ApplyNarrativeLayerOrder()
    {
        if (narrativePanel == null) return;

        if (useNarrativeBlackBackingLayer)
        {
            if (narrativeBlackBacking == null)
            {
                var go = new GameObject("NarrativeBlackBacking", typeof(RectTransform));
                go.transform.SetParent(narrativePanel.transform, false);
                StretchFull(go.GetComponent<RectTransform>());
                var img = go.AddComponent<Image>();
                img.sprite = GetOrCreateWhiteSprite();
                img.color = Color.black;
                img.raycastTarget = false;
                narrativeBlackBacking = img;
            }

            narrativeBlackBacking.gameObject.SetActive(true);
            var blk = narrativeBlackBacking.color;
            blk.r = blk.g = blk.b = 0f;
            blk.a = 1f;
            narrativeBlackBacking.color = blk;
            narrativeBlackBacking.transform.SetAsFirstSibling();
            if (narrativeBackgroundImage != null)
                narrativeBackgroundImage.transform.SetSiblingIndex(1);
            if (narrativeDialogGroup != null)
                narrativeDialogGroup.transform.SetSiblingIndex(narrativeBackgroundImage != null ? 2 : 1);
        }
        else if (narrativeBackgroundImage != null)
        {
            narrativeBackgroundImage.transform.SetAsFirstSibling();
        }
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    void EnsurePhase2DimOverlay()
    {
        if (phase2DimOverlay != null) return;
        if (introUiRoot == null) return;
        var rootRt = introUiRoot.GetComponent<RectTransform>();
        if (rootRt == null) return;

        var go = new GameObject("Phase2DimOverlay", typeof(RectTransform));
        go.transform.SetParent(rootRt, false);
        StretchFull(go.GetComponent<RectTransform>());
        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
        var img = go.AddComponent<Image>();
        img.sprite = GetOrCreateWhiteSprite();
        img.color = Color.black;
        img.raycastTarget = false;
        phase2DimOverlay = cg;
        go.transform.SetAsLastSibling();
    }

    void ConfigureMusicSourceOnly()
    {
        if (bgmClip == null) return;
        EnsureMusicSource();
        if (musicSource == null) return;
        musicSource.clip = bgmClip;
        musicSource.loop = loopBgm;
        musicSource.volume = bgmVolume;
    }

    /// <summary>
    /// 在已指定 <see cref="bgmClip"/> 且未手动指定 <see cref="musicSource"/> 时，为本物体追加独立 2D AudioSource，避免与 <see cref="sfxSource"/> 共用。
    /// </summary>
    void EnsureMusicSource()
    {
        if (bgmClip == null) return;
        if (musicSource != null) return;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
    }

    void PlayBgmIfNeeded()
    {
        if (!playBgmAtTransitionStart) return;
        if (bgmClip == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("IntroManager: 已勾选「进入阶段 3 播放 BGM」，但未指定 bgmClip，无法播放。请在 Inspector 中拖入音频片段。");
#endif
            return;
        }

        EnsureMusicSource();
        if (musicSource == null) return;

        musicSource.enabled = true;
        musicSource.mute = false;
        musicSource.clip = bgmClip;
        musicSource.loop = loopBgm;
        musicSource.volume = bgmVolume;
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    void EnsureSfxSource()
    {
        if (sfxSource != null) return;
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.loop = false;
    }

    static Sprite GetOrCreateWhiteSprite()
    {
        if (s_whiteSprite != null) return s_whiteSprite;
        var tex = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        s_whiteSprite.name = "IntroManager_RuntimeWhite";
        return s_whiteSprite;
    }

    static void EnsureImageCanRender(Image image)
    {
        if (image == null || image.sprite != null) return;
        image.sprite = GetOrCreateWhiteSprite();
    }

    void ApplyFonts()
    {
        var cjkAsset = narrativeChineseFontAsset != null
            ? narrativeChineseFontAsset
            : UiCjkFontProvider.GetOrCreateRuntimeCjkFontAsset();

        TMP_FontAsset primary = narrativeFontOverride != null
            ? narrativeFontOverride
            : (TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : cjkAsset);

        if (primary != null && cjkAsset != null && primary != cjkAsset)
            UiCjkFontProvider.EnsureCjkFallback(primary, cjkAsset);

        if (primary != null)
        {
            ApplyIntroFontToPanelTmp(narrativePanel, primary);
            ApplyIntroFontToPanelTmp(promptPanel, primary);
            ApplyIntroFontToPanelTmp(menuPanel, primary);
            ApplyIntroFontToPanelTmp(transitionPanel, primary);
            WarmDynamicAtlas(cjkAsset);
        }
        else
            Debug.LogWarning("IntroManager: 无法解析主字体。请指定 narrativeFontOverride，或在 Project Settings / TextMeshPro 中配置默认字体；中文依赖 narrativeChineseFontAsset 或 UiCjkFontProvider 运行时字体。");

        if (primary != null && cjkAsset == null)
            Debug.LogWarning("IntroManager: 未加载到中文字体 fallback（请指定 narrativeChineseFontAsset，或将 CjkUiFont 等 TMP 资源放入 Resources）。");

        ApplyNarrativeTextDisplaySettings();
        ApplyPromptTextDisplaySettings();
        ApplyTypingDotDisplaySettings();
    }

    static void ApplyIntroFontToPanelTmp(GameObject panelRoot, TMP_FontAsset primaryFont)
    {
        if (panelRoot == null || primaryFont == null) return;
        var list = panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (var i = 0; i < list.Length; i++)
        {
            var tmp = list[i];
            tmp.font = primaryFont;
            tmp.raycastTarget = false;
        }
    }

    void ApplyNarrativeTextDisplaySettings()
    {
        if (narrativeBody == null) return;
        narrativeBody.isRightToLeftText = false;
        narrativeBody.enableWordWrapping = true;
        narrativeBody.overflowMode = TextOverflowModes.Overflow;
        narrativeBody.alignment = narrativeTextAlignment;
        narrativeBody.color = narrativeTextColor;
        if (narrativeFontSize > 0.5f)
            narrativeBody.fontSize = narrativeFontSize;
        EnsureNarrativeBodyLayoutStable();
    }

    void ApplyNarrativeBodyStretchWithInset(RectTransform rt)
    {
        if (rt == null) return;
        StretchFull(rt);
        rt.offsetMin = new Vector2(narrativeTextInset.x, narrativeTextInset.y);
        rt.offsetMax = new Vector2(-narrativeTextInset.z, -narrativeTextInset.w);
    }

    /// <summary>
    /// 横排 LTR + 有效宽度：父级未布局完成时 TMP 宽度为 0 会一字一行（像竖排）。
    /// 若允许，则按 narrativeTextInset 在对话框内铺满。
    /// </summary>
    void EnsureNarrativeBodyLayoutStable()
    {
        if (narrativeBody == null) return;
        narrativeBody.isRightToLeftText = false;

        var rt = narrativeBody.rectTransform;
        var parentRt = rt.parent as RectTransform;
        Canvas.ForceUpdateCanvases();
        if (parentRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);

        if (parentRt == null)
        {
            narrativeBody.ForceMeshUpdate(true);
            return;
        }

        var parentW = Mathf.Abs(parentRt.rect.width);
        var selfW = Mathf.Abs(rt.rect.width);
        var needWidthFix = parentW > 48f && (selfW < 24f || (selfW > 1f && selfW < parentW * 0.15f));

        if (needWidthFix && !preserveNarrativeTextRectTransform)
            ApplyNarrativeBodyStretchWithInset(rt);
        else if (needWidthFix && preserveNarrativeTextRectTransform)
            Debug.LogWarning("IntroManager: NarrativeText 区域宽度过小，可能出现一字一行。请在场景中拉宽 RectTransform，或取消勾选 preserveNarrativeTextRectTransform。");

        narrativeBody.ForceMeshUpdate(true);
    }

    void ApplyPromptTextDisplaySettings()
    {
        if (promptBody == null) return;
        promptBody.enableWordWrapping = true;
        promptBody.overflowMode = TextOverflowModes.Overflow;
        promptBody.alignment = promptTextAlignment;
        promptBody.color = promptTextColor;
        if (promptFontSize > 0.5f)
            promptBody.fontSize = promptFontSize;
    }

    void ApplyTypingDotDisplaySettings()
    {
        if (typingDot == null) return;
        var c = typingDotColor;
        c.a = typingDot.color.a;
        typingDot.color = c;
        EnsureTypingDotParentAndStyle();
    }

    void EnsureTypingDotParentAndStyle()
    {
        if (typingDot == null || narrativeBody == null) return;
        if (typingDot.transform.parent != narrativeBody.transform)
            typingDot.transform.SetParent(narrativeBody.transform, false);
        typingDot.transform.localScale = Vector3.one;
        var dotRt = typingDot.rectTransform;
        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 1f);
        dotRt.pivot = new Vector2(0f, 0.5f);
        var fs = narrativeFontSize > 0.5f ? narrativeFontSize : narrativeBody.fontSize;
        var side = Mathf.Clamp(fs * 1.15f, 28f, 96f);
        dotRt.sizeDelta = new Vector2(side, side);
        typingDot.fontSize = Mathf.Clamp(fs * 0.85f, 18f, 72f);
        typingDot.alignment = TextAlignmentOptions.MidlineLeft;
        typingDot.enableWordWrapping = false;
    }

    void UpdateTypingDotPositionAtTextEnd()
    {
        if (typingDot == null || narrativeBody == null) return;
        EnsureTypingDotParentAndStyle();
        narrativeBody.ForceMeshUpdate(true);
        var info = narrativeBody.textInfo;
        var dotRt = typingDot.rectTransform;
        dotRt.SetAsLastSibling();

        if (info.characterCount < 1 || string.IsNullOrEmpty(narrativeBody.text))
        {
            PlaceTypingDotAtTextStart(dotRt);
            return;
        }

        var idx = info.characterCount - 1;
        while (idx >= 0)
        {
            var ch = info.characterInfo[idx];
            if (ch.isVisible && !IsLineBreakCharacter(ch))
                break;
            idx--;
        }

        if (idx < 0)
        {
            PlaceTypingDotAtTextStart(dotRt);
            return;
        }

        var c = info.characterInfo[idx];
        var midY = (c.topLeft.y + c.bottomLeft.y) * 0.5f;
        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 1f);
        dotRt.pivot = new Vector2(0f, 0.5f);
        dotRt.anchoredPosition = new Vector2(c.xAdvance + typingDotTrailingOffset, midY);
    }

    static bool IsLineBreakCharacter(TMP_CharacterInfo ch)
    {
        var c = ch.character;
        return c == '\n' || c == '\r' || c == '\u2028' || c == '\u2029';
    }

    void PlaceTypingDotAtTextStart(RectTransform dotRt)
    {
        var m = narrativeBody.margin;
        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 1f);
        dotRt.pivot = new Vector2(0f, 0.5f);
        dotRt.anchoredPosition = new Vector2(m.x + typingDotTrailingOffset, -m.y - typingDotTrailingOffset);
    }

    void WarmDynamicAtlas(TMP_FontAsset fontToWarm)
    {
        if (fontToWarm == null) return;
        var sb = new StringBuilder(512);
        AppendIfNotEmpty(sb, narrativeSegment1);
        AppendIfNotEmpty(sb, narrativeSegment2);
        AppendIfNotEmpty(sb, narrativeSegment3);
        AppendIfNotEmpty(sb, promptMessage);
        AppendIfNotEmpty(sb, narrativeText);
        sb.Append(CjkWarmupExtraPunctuation);
        sb.Append('·');
        if (sb.Length == 0) return;
        UiCjkFontProvider.WarmAtlas(fontToWarm, sb.ToString());
    }

    static void AppendIfNotEmpty(StringBuilder sb, string s)
    {
        if (!string.IsNullOrEmpty(s)) sb.Append(s);
    }

    IEnumerator WaitRealtimeSeconds(float seconds)
    {
        if (seconds <= 0f) yield break;
        var deadline = Time.unscaledTime + seconds;
        while (Time.unscaledTime < deadline)
            yield return null;
    }

    float PromptFadeOutSeconds => promptDimFadeOutDuration > 0f ? promptDimFadeOutDuration : promptFadeToDimDuration;

    IEnumerator RunIntro()
    {
        EnsureIntroGameBlocker();
        SetIntroGameBlockerVisible(true);

        if (phase2DimOverlay != null)
            phase2DimOverlay.alpha = 0f;

        if (narrativePanel != null) narrativePanel.SetActive(true);
        if (promptPanel != null) promptPanel.SetActive(false);
        if (transitionPanel != null) transitionPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);

        ApplyNarrativeLayerOrder();

        yield return null;
        Canvas.ForceUpdateCanvases();
        ApplyNarrativeTextDisplaySettings();

        var segments = new[] { narrativeSegment1, narrativeSegment2, narrativeSegment3 };
        var sprites = new[] { narrativeBackgroundSprite1, narrativeBackgroundSprite2, narrativeBackgroundSprite3 };

        for (var i = 0; i < segments.Length; i++)
        {
            if (string.IsNullOrEmpty(segments[i])) continue;
            var sp = sprites[i];
            if (sp == null && narrativeBackgroundSpriteLegacy != null)
                sp = narrativeBackgroundSpriteLegacy;
            yield return PlaySegment(segments[i], sp, i == 0);
        }

        yield return WaitRealtimeSeconds(delayAfterNarrativeSeconds);

        if (narrativePanel != null) narrativePanel.SetActive(false);

        if (phase2DimOverlay != null)
            phase2DimOverlay.alpha = promptEnterOverlayStartAlpha;

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            if (promptBody != null)
            {
                promptBody.text = promptMessage;
                ApplyPromptTextDisplaySettings();
            }
        }

        _promptBlinkActive = true;
        if (promptBody != null && promptMessageBlink)
        {
            if (_promptBlinkCoroutine != null) StopCoroutine(_promptBlinkCoroutine);
            _promptBlinkCoroutine = StartCoroutine(BlinkPromptMessage());
        }

        if (phase2DimOverlay != null && promptFadeToDimDuration > 0f)
            yield return FadeCanvasGroup(phase2DimOverlay, phase2DimOverlay.alpha, promptDimTargetAlpha, promptFadeToDimDuration);
        else if (phase2DimOverlay != null)
            phase2DimOverlay.alpha = promptDimTargetAlpha;

        yield return WaitForConfirmKey();

        StopPromptBlink();

        if (promptPanel != null) promptPanel.SetActive(false);

        if (phase2DimOverlay != null && PromptFadeOutSeconds > 0f)
            yield return FadeCanvasGroup(phase2DimOverlay, phase2DimOverlay.alpha, 0f, PromptFadeOutSeconds);
        else if (phase2DimOverlay != null)
            phase2DimOverlay.alpha = 0f;

        yield return DoTransition();

        if (stopBgmWhenShowingMainMenu && musicSource != null && musicSource.isPlaying)
            musicSource.Stop();

        yield return ShowMainMenuEntrance();
    }

    IEnumerator WaitForConfirmKey()
    {
        while (!Input.GetKeyDown(confirmKey))
            yield return null;
    }

    IEnumerator BlinkPromptMessage()
    {
        if (promptBody == null) yield break;
        var half = Mathf.Max(0.05f, promptMessageBlinkPeriodSeconds * 0.5f);
        while (_promptBlinkActive)
        {
            var c = promptTextColor;
            promptBody.color = c;
            yield return WaitRealtimeSeconds(half);
            c.a = promptTextColor.a * promptMessageBlinkMinAlpha;
            promptBody.color = c;
            yield return WaitRealtimeSeconds(half);
        }
    }

    void StopPromptBlink()
    {
        _promptBlinkActive = false;
        if (_promptBlinkCoroutine != null)
        {
            StopCoroutine(_promptBlinkCoroutine);
            _promptBlinkCoroutine = null;
        }
        if (promptBody != null)
            promptBody.color = promptTextColor;
    }

    void EnsureMenuPanelCanvasGroup()
    {
        if (menuPanel == null) return;
        if (menuPanelCanvasGroup == null)
            menuPanelCanvasGroup = menuPanel.GetComponent<CanvasGroup>();
        if (menuPanelCanvasGroup == null)
            menuPanelCanvasGroup = menuPanel.AddComponent<CanvasGroup>();
    }

    void EnsureStartButtonBlinkCanvasGroup()
    {
        if (startButton == null) return;
        _startButtonBlinkCanvasGroup = startButton.GetComponent<CanvasGroup>();
        if (_startButtonBlinkCanvasGroup == null)
            _startButtonBlinkCanvasGroup = startButton.gameObject.AddComponent<CanvasGroup>();
    }

    IEnumerator ShowMainMenuEntrance()
    {
        EnsureMenuPanelCanvasGroup();
        // 不可先关 IntroGameBlocker：主菜单 CanvasGroup 仍为 0 时会透视到背后 3D 场景，出现一帧闪屏。
        // 保持全屏黑底直至主菜单淡入结束，再移除 blocker。
        if (menuPanel != null)
            menuPanel.SetActive(true);

        if (menuPanelCanvasGroup != null)
        {
            menuPanelCanvasGroup.alpha = 0f;
            menuPanelCanvasGroup.interactable = false;
            menuPanelCanvasGroup.blocksRaycasts = false;
            if (menuFadeInFromDarkDuration > 0f)
                yield return FadeCanvasGroup(menuPanelCanvasGroup, 0f, 1f, menuFadeInFromDarkDuration);
            else
                menuPanelCanvasGroup.alpha = 1f;
            menuPanelCanvasGroup.interactable = true;
            menuPanelCanvasGroup.blocksRaycasts = true;
        }

        SetIntroGameBlockerVisible(false);

        StopStartButtonBlink();
        if (startButtonBlinkEnabled && startButton != null)
        {
            _startButtonBlinkActive = true;
            if (_startButtonBlinkCoroutine != null)
                StopCoroutine(_startButtonBlinkCoroutine);
            _startButtonBlinkCoroutine = StartCoroutine(BlinkStartButton());
        }
    }

    IEnumerator BlinkStartButton()
    {
        EnsureStartButtonBlinkCanvasGroup();
        if (_startButtonBlinkCanvasGroup == null) yield break;
        var half = Mathf.Max(0.05f, startButtonBlinkPeriodSeconds * 0.5f);
        while (_startButtonBlinkActive)
        {
            _startButtonBlinkCanvasGroup.alpha = 1f;
            yield return WaitRealtimeSeconds(half);
            _startButtonBlinkCanvasGroup.alpha = startButtonBlinkMinAlpha;
            yield return WaitRealtimeSeconds(half);
        }
    }

    void StopStartButtonBlink()
    {
        _startButtonBlinkActive = false;
        if (_startButtonBlinkCoroutine != null)
        {
            StopCoroutine(_startButtonBlinkCoroutine);
            _startButtonBlinkCoroutine = null;
        }
        if (_startButtonBlinkCanvasGroup != null)
            _startButtonBlinkCanvasGroup.alpha = 1f;
        else if (startButton != null)
        {
            var cg = startButton.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = 1f;
        }
    }

    IEnumerator PlaySegment(string text, Sprite bgSprite, bool isFirst)
    {
        if (narrativeBody != null) narrativeBody.text = string.Empty;

        if (narrativeBackgroundImage != null)
        {
            ApplyNarrativeLayerOrder();

            if (bgSprite != null)
            {
                narrativeBackgroundImage.sprite = bgSprite;
                narrativeBackgroundImage.preserveAspect = _preserveBackgroundAspect;
                var c = Color.white;
                c.a = 0f;
                narrativeBackgroundImage.color = c;
            }
            else
            {
                EnsureImageCanRender(narrativeBackgroundImage);
                var c = narrativeBackgroundImage.color;
                c.r = c.g = c.b = 0f;
                c.a = 0f;
                narrativeBackgroundImage.color = c;
            }

            var fadeIn = isFirst ? firstBackgroundFadeInSeconds : backgroundFadeInSeconds;
            if (fadeIn > 0f)
                yield return FadeImageAlpha(narrativeBackgroundImage, 0f, 1f, fadeIn);
            else
            {
                var opaque = narrativeBackgroundImage.color;
                opaque.a = 1f;
                narrativeBackgroundImage.color = opaque;
            }
        }

        yield return WaitRealtimeSeconds(segmentDialogDelaySeconds);

        if (narrativeDialogGroup != null)
        {
            narrativeDialogGroup.alpha = 0f;
            if (dialogFadeInSeconds > 0f)
                yield return FadeCanvasGroup(narrativeDialogGroup, 0f, dialogMaxAlpha, dialogFadeInSeconds);
            else
                narrativeDialogGroup.alpha = dialogMaxAlpha;
        }

        _typingDotActive = true;
        if (typingDot != null)
        {
            typingDot.gameObject.SetActive(true);
            UpdateTypingDotPositionAtTextEnd();
            if (_typingDotCoroutine != null) StopCoroutine(_typingDotCoroutine);
            _typingDotCoroutine = StartCoroutine(BlinkTypingDot());
        }

        yield return Typewriter(text);

        _typingDotActive = false;
        if (_typingDotCoroutine != null)
        {
            StopCoroutine(_typingDotCoroutine);
            _typingDotCoroutine = null;
        }
        if (typingDot != null)
            typingDot.gameObject.SetActive(false);

        yield return WaitRealtimeSeconds(segmentHoldSeconds);

        if (narrativeDialogGroup != null && dialogFadeOutSeconds > 0f)
            yield return FadeCanvasGroup(narrativeDialogGroup, narrativeDialogGroup.alpha, 0f, dialogFadeOutSeconds);
        else if (narrativeDialogGroup != null)
            narrativeDialogGroup.alpha = 0f;

        if (narrativeBackgroundImage != null && backgroundFadeOutSeconds > 0f)
            yield return FadeImageAlpha(narrativeBackgroundImage, narrativeBackgroundImage.color.a, 0f, backgroundFadeOutSeconds);
    }

    IEnumerator BlinkTypingDot()
    {
        if (typingDot == null) yield break;
        var half = Mathf.Max(0.05f, dotBlinkPeriodSeconds * 0.5f);
        while (_typingDotActive)
        {
            UpdateTypingDotPositionAtTextEnd();
            var col = typingDotColor;
            col.a = 1f;
            typingDot.color = col;
            yield return WaitRealtimeSeconds(half);
            col.a = 0.25f;
            typingDot.color = col;
            yield return WaitRealtimeSeconds(half);
        }
    }

    IEnumerator Typewriter(string full)
    {
        if (narrativeBody == null) yield break;
        if (string.IsNullOrEmpty(full)) yield break;

        var sb = new StringBuilder(Mathf.Min(full.Length * 2, 8192));
        var si = new StringInfo(full);
        var n = si.LengthInTextElements;
        var interval = 1f / Mathf.Max(0.01f, charsPerSecond);
        var nextCharTime = Time.unscaledTime;

        for (var i = 0; i < n; i++)
        {
            var el = si.SubstringByTextElements(i, 1);
            sb.Append(el);
            narrativeBody.SetText(sb);
            UpdateTypingDotPositionAtTextEnd();
            if (!ShouldSkipTypewriterTick(el))
                PlayTypewriterTick();
            nextCharTime += interval;
            while (Time.unscaledTime < nextCharTime)
                yield return null;
        }
    }

    static bool ShouldSkipTypewriterTick(string textElement)
    {
        if (string.IsNullOrEmpty(textElement)) return true;
        foreach (var c in textElement)
        {
            if (!char.IsWhiteSpace(c) && !char.IsControl(c))
                return false;
        }
        return true;
    }

    void PlayTypewriterTick()
    {
        if (typewriterTickClip == null) return;
        EnsureSfxSource();
        if (sfxSource == null) return;
        if (Time.unscaledTime - _lastTypewriterTickTime < minTypewriterTickInterval) return;
        _lastTypewriterTickTime = Time.unscaledTime;
        sfxSource.PlayOneShot(typewriterTickClip, typewriterSfxVolume);
    }

    IEnumerator FadeImageAlpha(Image img, float from, float to, float duration)
    {
        if (img == null || duration <= 0f) yield break;
        var c = img.color;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }

    /// <summary>阶段 3：过渡面板已显示且起始 Alpha 已设置；随后会触发 Animator、播放 BGM、由暗变亮。</summary>
    protected virtual void OnTransitionPhase3Enter() { }

    /// <summary>由暗变亮（遮罩变透明）结束。</summary>
    protected virtual void OnTransitionDarkToBrightComplete() { }

    /// <summary>占位/默认「动画」等待结束，即将由亮变暗；可在此启动后续逻辑或配合 Timeline。</summary>
    protected virtual void OnTransitionHoldComplete() { }

    /// <summary>由亮变暗结束，即将淡出清屏。</summary>
    protected virtual void OnTransitionBrightToDarkComplete() { }

    /// <summary>阶段 3 协程结束，过渡面板将关闭。</summary>
    protected virtual void OnTransitionPhase3Exit() { }

    IEnumerator DoTransition()
    {
        if (transitionPanel != null)
            transitionPanel.SetActive(true);

        if (transitionCanvasGroup != null)
            transitionCanvasGroup.alpha = transitionEnterStartAlpha;

        OnTransitionPhase3Enter();

        if (transitionAnimator != null && !string.IsNullOrEmpty(transitionAnimatorTrigger))
            transitionAnimator.SetTrigger(transitionAnimatorTrigger);

        PlayBgmIfNeeded();

        if (transitionCanvasGroup != null)
        {
            if (transitionBlackScreenEnabled && transitionBlackScreenSeconds > 0f)
            {
                transitionCanvasGroup.alpha = 1f;
                yield return WaitRealtimeSeconds(transitionBlackScreenSeconds);
            }

            if (transitionFadeDarkToBrightDuration > 0f)
                yield return FadeCanvasGroup(
                    transitionCanvasGroup,
                    transitionCanvasGroup.alpha,
                    transitionAfterBrightenAlpha,
                    transitionFadeDarkToBrightDuration);
            else
                transitionCanvasGroup.alpha = transitionAfterBrightenAlpha;

            OnTransitionDarkToBrightComplete();

            yield return WaitRealtimeSeconds(transitionHoldSeconds);

            OnTransitionHoldComplete();

            if (transitionFadeBrightToDarkDuration > 0f)
                yield return FadeCanvasGroup(
                    transitionCanvasGroup,
                    transitionCanvasGroup.alpha,
                    transitionEndDarkAlpha,
                    transitionFadeBrightToDarkDuration);
            else
                transitionCanvasGroup.alpha = transitionEndDarkAlpha;

            OnTransitionBrightToDarkComplete();

            if (transitionFadeOutClearDuration > 0f)
                yield return FadeCanvasGroup(
                    transitionCanvasGroup,
                    transitionCanvasGroup.alpha,
                    0f,
                    transitionFadeOutClearDuration);
            else
                transitionCanvasGroup.alpha = 0f;
        }
        else
        {
            if (transitionBlackScreenEnabled && transitionBlackScreenSeconds > 0f)
                yield return WaitRealtimeSeconds(transitionBlackScreenSeconds);
            OnTransitionDarkToBrightComplete();
            yield return WaitRealtimeSeconds(transitionHoldSeconds);
            OnTransitionHoldComplete();
            OnTransitionBrightToDarkComplete();
        }

        OnTransitionPhase3Exit();

        if (transitionPanel != null)
            transitionPanel.SetActive(false);
    }

    static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }

    void ShowMenuImmediate()
    {
        StopPromptBlink();
        if (narrativePanel != null) narrativePanel.SetActive(false);
        if (promptPanel != null) promptPanel.SetActive(false);
        if (transitionPanel != null) transitionPanel.SetActive(false);
        if (phase2DimOverlay != null) phase2DimOverlay.alpha = 0f;
        StartCoroutine(ShowMenuImmediateRoutine());
    }

    IEnumerator ShowMenuImmediateRoutine()
    {
        yield return ShowMainMenuEntrance();
        if (playBgmAtTransitionStart)
            PlayBgmIfNeeded();
    }

    void PlayMenuButtonClip(AudioClip clip)
    {
        if (clip == null) return;
        EnsureSfxSource();
        if (sfxSource != null)
            sfxSource.PlayOneShot(clip, menuButtonClickVolume);
    }

    void OnStartClicked()
    {
        StopStartButtonBlink();
        GameplayBgmGate.EnteredFromStartButton = true;
        if (stopBgmOnStartGame && musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
        PlayMenuButtonClip(startButtonClickClip);

        if (string.IsNullOrEmpty(gameSceneName))
        {
            if (introUiRoot != null) introUiRoot.SetActive(false);
            else if (menuPanel != null) menuPanel.SetActive(false);
            MainGameplayBgmController.PlayAllPending();
            return;
        }

        if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            if (introUiRoot != null) introUiRoot.SetActive(false);
            else if (menuPanel != null) menuPanel.SetActive(false);
            MainGameplayBgmController.PlayAllPending();
            return;
        }

        StartCoroutine(LoadGameSceneAsyncWithProgress());
    }

    IEnumerator LoadGameSceneAsyncWithProgress()
    {
        ResolveLoadingUiReferences();

        if (loadingSceneBackdrop != null)
        {
            loadingSceneBackdrop.gameObject.SetActive(true);
            PlaceLoadingBackdropBehindProgress();
        }

        if (loadingSceneProgressSlider != null)
        {
            loadingSceneProgressSlider.gameObject.SetActive(true);
            loadingSceneProgressSlider.minValue = 0f;
            loadingSceneProgressSlider.maxValue = 1f;
            loadingSceneProgressSlider.value = 0f;
            loadingSceneProgressSlider.interactable = false;
        }

        if (loadingSceneProgressText != null)
        {
            loadingSceneProgressText.gameObject.SetActive(true);
            loadingSceneProgressText.text = "加载中 0%";
        }

        Canvas.ForceUpdateCanvases();

        // Slider/Text 在 introUiRoot 下时不能关掉整棵 IntroCanvas，否则加载条一并被隐藏
        if (narrativePanel != null) narrativePanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
        if (promptPanel != null) promptPanel.SetActive(false);
        if (transitionPanel != null) transitionPanel.SetActive(false);

        var op = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
        if (op == null)
        {
            if (gameSceneEntranceFadeSeconds > 0f)
            {
                GameplaySceneEntranceFader.DurationSeconds = gameSceneEntranceFadeSeconds;
                GameplayBgmGate.PendingGameplayFadeIn = true;
            }

            SceneManager.LoadScene(gameSceneName);
            yield break;
        }

        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            var t = Mathf.Clamp01(op.progress / 0.9f);
            if (loadingSceneProgressSlider != null)
                loadingSceneProgressSlider.value = t;
            if (loadingSceneProgressText != null)
                loadingSceneProgressText.text = $"加载中 {Mathf.RoundToInt(t * 100f)}%";
            yield return null;
        }

        if (loadingSceneProgressSlider != null)
            loadingSceneProgressSlider.value = 1f;
        if (loadingSceneProgressText != null)
            loadingSceneProgressText.text = "加载中 100%";

        yield return null;

        if (gameSceneEntranceFadeSeconds > 0f)
        {
            GameplaySceneEntranceFader.DurationSeconds = gameSceneEntranceFadeSeconds;
            GameplayBgmGate.PendingGameplayFadeIn = true;
        }

        op.allowSceneActivation = true;
        yield return op;
    }

    void PlaceLoadingBackdropBehindProgress()
    {
        if (loadingSceneBackdrop == null || introUiRoot == null) return;
        var bdTr = loadingSceneBackdrop.transform;
        if (bdTr.parent != introUiRoot.transform) return;

        var parent = introUiRoot.transform;
        var target = int.MaxValue;
        if (loadingSceneProgressSlider != null && loadingSceneProgressSlider.transform.parent == parent)
            target = Mathf.Min(target, loadingSceneProgressSlider.transform.GetSiblingIndex());
        if (loadingSceneProgressText != null && loadingSceneProgressText.transform.parent == parent)
            target = Mathf.Min(target, loadingSceneProgressText.transform.GetSiblingIndex());

        if (target == int.MaxValue) return;
        var iBd = bdTr.GetSiblingIndex();
        if (iBd > target)
            bdTr.SetSiblingIndex(target);
    }

    void OnQuitClicked()
    {
        PlayMenuButtonClip(quitButtonClickClip);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
