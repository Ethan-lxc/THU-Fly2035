using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 环境事件任务列表面板：挂在 <c>GameplayHUD → Panels</c> 下，思路与 <see cref="AchievementPanelController"/> 相同
///（CanvasGroup 显隐、整块面板背景、与 HUD 入口按钮分离）。
/// 由 <see cref="NpcEventTaskBarHud"/> 图标或本面板上的关闭按钮切换显示。
/// </summary>
[DisallowMultipleComponent]
public class NpcEventJournalPanelController : MonoBehaviour
{
    const string BuiltScrollChildName = "NpcEventJournal_ScrollView";

    [Serializable]
    public class EventRowEntry
    {
        public string displayName;
        [Tooltip("RewardCardRevealPopup 入库键，或医院任务的 playerPrefsCompletedKey")]
        public string achievementPlayerPrefsKey;
        public int sortOrder;
    }

    [Header("引用")]
    [Tooltip("空则 FindObjectOfType")]
    public GameplayHudLayout gameplayHudLayout;

    [Tooltip("建议与本物体同一节点；alpha 控制整板显隐")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("无 CanvasGroup 时用于 SetActive")]
    public GameObject panelFallbackRoot;

    [Tooltip("面板底图；空则仅用纯色")]
    public Image panelBackgroundImage;

    public Sprite panelBackgroundSprite;

    [Tooltip("与底图相乘")]
    public Color panelBackgroundTint = new Color(0.12f, 0.12f, 0.14f, 0.94f);

    [Header("条目")]
    public List<EventRowEntry> entries = new List<EventRowEntry>();

    [Tooltip("entries 为空时填入工程内已知事件")]
    public bool populateKnownDefaultsWhenEmpty = true;

    [Header("外观与列表")]
    [Min(8)] public float rowFontSize = 15f;

    [Min(22f)] public float rowMinHeight = 26f;

    [Min(6f)] public float indicatorSize = 12f;

    public Color indicatorIncomplete = new Color(0.95f, 0.85f, 0.2f, 1f);

    public Color indicatorComplete = new Color(0.35f, 0.78f, 0.35f, 1f);

    public string titleText = "环境事件";

    [Min(10)] public float titleFontSize = 16f;

    [Min(10)] public float closeButtonFontSize = 20f;

    [Header("滚动")]
    public bool createScrollIfMissing = true;

    public ScrollRect scrollRect;

    public RectTransform contentRoot;

    [Tooltip("0 则仅在 Awake / 打开面板时刷新")]
    [Min(0f)] public float realtimeRefreshInterval = 1.25f;

    [Header("行为")]
    public bool startClosed = true;

    [Header("关闭按钮音效（可选）")]
    public AudioClip closeClickClip;

    [Range(0f, 1f)] public float closeClickVolume = 1f;

    bool _open;
    Coroutine _refreshRoutine;
    AudioSource _sfx;
    TextMeshProUGUI _titleTmp;
    Button _closeButton;

    public bool IsOpen => _open;

    void Awake()
    {
        AutoResolveReferences();
        ApplyPanelBackground();
        if (populateKnownDefaultsWhenEmpty && (entries == null || entries.Count == 0))
            PopulateKnownDefaults();

        EnsurePanelChrome();
        var panelRt = transform as RectTransform;
        EnsureScroll(panelRt);
        if (scrollRect != null)
            scrollRect.transform.SetAsFirstSibling();
        EnsureTitleAndClose();
        LayoutScrollBelowTitle();
        SetOpen(!startClosed);
    }

    void AutoResolveReferences()
    {
        if (gameplayHudLayout == null)
            gameplayHudLayout = FindObjectOfType<GameplayHudLayout>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelBackgroundImage == null)
        {
            panelBackgroundImage = GetComponent<Image>();
            if (panelBackgroundImage == null && transform.childCount > 0)
                panelBackgroundImage = transform.GetChild(0).GetComponent<Image>();
        }
    }

    void ApplyPanelBackground()
    {
        var img = panelBackgroundImage;
        if (img == null)
            return;
        if (panelBackgroundSprite != null)
        {
            img.sprite = panelBackgroundSprite;
            img.type = Image.Type.Simple;
        }

        img.color = panelBackgroundTint;
        img.raycastTarget = true;
    }

    void EnsurePanelChrome()
    {
        if (panelBackgroundImage == null)
        {
            var img = GetComponent<Image>();
            if (img == null)
                img = gameObject.AddComponent<Image>();
            panelBackgroundImage = img;
            ApplyPanelBackground();
        }
    }

    void OnEnable()
    {
        if (_refreshRoutine != null)
            StopCoroutine(_refreshRoutine);
        if (realtimeRefreshInterval > 0.001f && isActiveAndEnabled)
            _refreshRoutine = StartCoroutine(RealtimeRefreshLoop());
    }

    void OnDisable()
    {
        if (_refreshRoutine != null)
        {
            StopCoroutine(_refreshRoutine);
            _refreshRoutine = null;
        }
    }

    IEnumerator RealtimeRefreshLoop()
    {
        var wait = new WaitForSecondsRealtime(realtimeRefreshInterval);
        while (enabled)
        {
            yield return wait;
            if (_open)
                Refresh();
        }
    }

    public void PopulateKnownDefaults()
    {
        entries = new List<EventRowEntry>
        {
            new EventRowEntry { displayName = "医院取药", achievementPlayerPrefsKey = "Quest_HospitalFetchMedicine_Completed", sortOrder = 0 },
            new EventRowEntry { displayName = "同学生病支线", achievementPlayerPrefsKey = "SickClassmate_RewardCardStored", sortOrder = 1 },
            new EventRowEntry { displayName = "指路事件", achievementPlayerPrefsKey = "DirectionGuide_RewardCardStored", sortOrder = 2 },
            new EventRowEntry { displayName = "找车任务", achievementPlayerPrefsKey = "FindBike_RewardCardStored", sortOrder = 3 },
            new EventRowEntry { displayName = "失眠咨询", achievementPlayerPrefsKey = "InsomniaAdvice_RewardCardStored", sortOrder = 4 },
            new EventRowEntry { displayName = "送伞事件", achievementPlayerPrefsKey = "UmbrellaDelivery_RewardCardStored", sortOrder = 5 },
            new EventRowEntry { displayName = "等待事件", achievementPlayerPrefsKey = "WaitingEvent_RewardCardStored", sortOrder = 6 },
            new EventRowEntry { displayName = "表白咨询", achievementPlayerPrefsKey = "ConfessionAdvice_RewardCardStored", sortOrder = 7 },
            new EventRowEntry { displayName = "论文咨询", achievementPlayerPrefsKey = "ThesisAdvice_RewardCardStored", sortOrder = 8 }
        };
    }

    public void Toggle()
    {
        SetOpen(!_open);
    }

    public void SetOpen(bool open)
    {
        if (open)
        {
            var hud = gameplayHudLayout != null ? gameplayHudLayout : FindObjectOfType<GameplayHudLayout>();
            hud?.CloseOtherPanelsBeforeNpcJournal();
        }

        _open = open;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = open ? 1f : 0f;
            panelCanvasGroup.interactable = open;
            panelCanvasGroup.blocksRaycasts = open;
        }
        else if (panelFallbackRoot != null)
            panelFallbackRoot.SetActive(open);
        else
            gameObject.SetActive(open);

        if (open)
        {
            Refresh();
            FinishRefresh();
        }
    }

    void EnsureTitleAndClose()
    {
        const string titleName = "NpcEventJournal_PanelTitle";
        const string closeName = "NpcEventJournal_Close";

        if (!string.IsNullOrWhiteSpace(titleText))
        {
            Transform titleTf = transform.Find(titleName);
            if (titleTf == null)
            {
                var titleGo = new GameObject(titleName, typeof(RectTransform));
                titleGo.transform.SetParent(transform, false);
                titleTf = titleGo.transform;
                var trt = titleGo.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 1f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.sizeDelta = new Vector2(0f, 32f);
                trt.anchoredPosition = new Vector2(0f, -2f);
                _titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
                _titleTmp.alignment = TextAlignmentOptions.Center;
                _titleTmp.fontStyle = FontStyles.Bold;
                _titleTmp.color = Color.white;
                _titleTmp.raycastTarget = false;
            }
            else
                _titleTmp = titleTf.GetComponent<TextMeshProUGUI>() ?? titleTf.gameObject.AddComponent<TextMeshProUGUI>();

            _titleTmp.text = titleText.Trim();
            _titleTmp.fontSize = titleFontSize;
        }

        Transform closeTf = transform.Find(closeName);
        if (closeTf == null)
        {
            var closeGo = new GameObject(closeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(transform, false);
            closeTf = closeGo.transform;
            var crt = closeGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.sizeDelta = new Vector2(36f, 32f);
            crt.anchoredPosition = new Vector2(-4f, -4f);
            var cImg = closeGo.GetComponent<Image>();
            cImg.color = new Color(1f, 1f, 1f, 0.12f);
            cImg.raycastTarget = true;
            _closeButton = closeGo.GetComponent<Button>();
            _closeButton.targetGraphic = cImg;

            var labelGo = new GameObject("X", typeof(RectTransform));
            labelGo.transform.SetParent(closeGo.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var xTmp = labelGo.AddComponent<TextMeshProUGUI>();
            xTmp.text = "×";
            xTmp.alignment = TextAlignmentOptions.Center;
            xTmp.fontSize = closeButtonFontSize;
            xTmp.color = Color.white;
            xTmp.raycastTarget = false;
        }
        else
            _closeButton = closeTf.GetComponent<Button>();

        _closeButton.onClick.RemoveAllListeners();
        _closeButton.onClick.AddListener(OnCloseClicked);
    }

    void OnCloseClicked()
    {
        PlayCloseSound();
        SetOpen(false);
    }

    void PlayCloseSound()
    {
        if (closeClickClip == null || !Application.isPlaying)
            return;
        if (_sfx == null)
        {
            _sfx = GetComponent<AudioSource>();
            if (_sfx == null)
                _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.ignoreListenerPause = true;
        }

        _sfx.PlayOneShot(closeClickClip, Mathf.Clamp01(closeClickVolume));
    }

    void LayoutScrollBelowTitle()
    {
        if (scrollRect == null)
            return;
        var scrollRt = scrollRect.transform as RectTransform;
        if (scrollRt == null)
            return;
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        var top = string.IsNullOrWhiteSpace(titleText) ? 8f : 40f;
        scrollRt.offsetMin = new Vector2(8f, 8f);
        scrollRt.offsetMax = new Vector2(-8f, -top);
    }

    public void Refresh()
    {
        if (!_open)
            return;
        if (scrollRect == null || contentRoot == null)
            return;

        var content = contentRoot;
        for (var i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        var ordered = entries != null && entries.Count > 0
            ? entries
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.achievementPlayerPrefsKey))
                .OrderBy(e => e.sortOrder)
                .ThenBy(e => e.displayName ?? "")
                .ToList()
            : new List<EventRowEntry>();

        if (ordered.Count == 0)
        {
            AddPlaceholderRow("(无条目：请在 NpcEventJournalPanelController.entries 中配置)");
            FinishRefresh();
            return;
        }

        foreach (var e in ordered)
        {
            var key = e.achievementPlayerPrefsKey.Trim();
            var earned = PlayerPrefs.GetInt(key, 0) != 0;
            var tit = string.IsNullOrWhiteSpace(e.displayName) ? key : e.displayName.Trim();
            AddDataRow(tit, earned);
        }

        FinishRefresh();
    }

    void FinishRefresh()
    {
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    void AddPlaceholderRow(string text)
    {
        var row = CreateRowRoot("Placeholder");
        var tmp = row.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = rowFontSize;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = new Color(0.75f, 0.75f, 0.78f, 1f);
        tmp.enableWordWrapping = true;
        tmp.margin = new Vector4(6f, 4f, 6f, 4f);
    }

    void AddDataRow(string tit, bool cardEarned)
    {
        var row = CreateRowRoot("Row");
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(0, 0, 2, 2);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var indicatorGo = new GameObject("Indicator", typeof(RectTransform));
        indicatorGo.transform.SetParent(row, false);
        var indRt = indicatorGo.GetComponent<RectTransform>();
        indRt.sizeDelta = new Vector2(indicatorSize, indicatorSize);
        var indLe = indicatorGo.AddComponent<LayoutElement>();
        indLe.minWidth = indicatorSize;
        indLe.preferredWidth = indicatorSize;
        indLe.minHeight = indicatorSize;
        indLe.preferredHeight = indicatorSize;
        var indImg = indicatorGo.AddComponent<Image>();
        indImg.color = cardEarned ? indicatorComplete : indicatorIncomplete;
        indImg.raycastTarget = false;

        var textGo = new GameObject("Title", typeof(RectTransform));
        textGo.transform.SetParent(row, false);
        var textLe = textGo.AddComponent<LayoutElement>();
        textLe.flexibleWidth = 1f;
        textLe.minHeight = rowMinHeight;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = tit;
        tmp.fontSize = rowFontSize;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = true;
        tmp.margin = new Vector4(0f, 0f, 4f, 0f);
        tmp.color = Color.white;
    }

    RectTransform CreateRowRoot(string name)
    {
        var rowGo = new GameObject(name, typeof(RectTransform));
        rowGo.transform.SetParent(contentRoot, false);
        var rt = rowGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, rowMinHeight);
        var le = rowGo.AddComponent<LayoutElement>();
        le.minHeight = rowMinHeight;
        le.preferredHeight = -1f;
        return rt;
    }

    void EnsureScroll(RectTransform panelRt)
    {
        if (!createScrollIfMissing && scrollRect == null)
            return;
        if (panelRt == null)
            return;

        var existing = panelRt.Find(BuiltScrollChildName);
        if (existing != null)
        {
            scrollRect = existing.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                contentRoot = scrollRect.content;
                EnsureContentLayout(contentRoot);
            }

            LayoutScrollBelowTitle();
            return;
        }

        if (scrollRect != null && scrollRect.transform.parent == panelRt)
        {
            if (contentRoot == null)
                contentRoot = scrollRect.content;
            EnsureContentLayout(contentRoot);
            LayoutScrollBelowTitle();
            return;
        }

        BuildScrollUnder(panelRt);
        LayoutScrollBelowTitle();
    }

    void BuildScrollUnder(RectTransform panelRt)
    {
        var scrollGo = new GameObject(BuiltScrollChildName, typeof(RectTransform));
        scrollGo.transform.SetParent(panelRt, false);
        scrollGo.transform.SetAsFirstSibling();

        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(8f, 8f);
        scrollRt.offsetMax = new Vector2(-8f, -40f);

        var scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(0.08f, 0.08f, 0.1f, 0.45f);
        scrollBg.raycastTarget = true;

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 22f;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        var vpImg = viewportGo.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.02f);
        vpImg.raycastTarget = true;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var crt = contentGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 0f);

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(4, 4, 4, 6);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRt;
        scrollRect.content = crt;
        contentRoot = crt;
        EnsureContentLayout(contentRoot);
    }

    static void EnsureContentLayout(RectTransform content)
    {
        if (content == null)
            return;
        if (content.GetComponent<VerticalLayoutGroup>() == null)
        {
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(4, 4, 4, 6);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        if (content.GetComponent<ContentSizeFitter>() == null)
        {
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    void OnValidate()
    {
        if (_titleTmp != null && !string.IsNullOrWhiteSpace(titleText))
        {
            _titleTmp.text = titleText.Trim();
            _titleTmp.fontSize = titleFontSize;
        }
    }
}
