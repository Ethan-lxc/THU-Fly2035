using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左侧任务框：当前任务正文 + 下方竖向滚动的 NPC 事件进度列表（PlayerPrefs 完成键）。
/// </summary>
public class QuestPanelHud : MonoBehaviour
{
    const string BuiltScrollChildName = "QuestJournal_ScrollView";

    [Serializable]
    public class QuestJournalEntry
    {
        public string displayName;
        [Tooltip("与各 NPC 上 progressPlayerPrefsKey / 医院 Config 的 playerPrefsCompletedKey 一致")]
        public string progressPlayerPrefsKey;
        public int sortOrder;
        [TextArea(1, 3)]
        public string description;
    }

    [Header("引用")]
    [Tooltip("任务面板背景框（Image）")]
    public Image questFrameImage;

    [Tooltip("任务描述正文")]
    public TextMeshProUGUI questBodyText;

    [Header("外观")]
    public Color frameColor = Color.white;

    [Tooltip("正文默认颜色")]
    public Color bodyTextColor = Color.white;

    [Tooltip("无任务时显示的占位文案")]
    [TextArea(1, 4)]
    public string defaultQuestText = "当前任务将显示在此处。";

    [Header("事件进度列表")]
    [Tooltip("关闭时不生成滚动区，仅保留单行任务提示")]
    public bool enableEventJournal = true;

    public List<QuestJournalEntry> journalEntries = new List<QuestJournalEntry>();

    [Tooltip("journalEntries 为空时填入工程内已知事件的默认键名")]
    public bool populateKnownDefaultsWhenJournalEmpty = true;

    [Tooltip("未指定 ScrollRect 时自动生成（挂在任务框下）")]
    public bool createJournalScrollIfMissing = true;

    public ScrollRect journalScrollRect;

    [Tooltip("一般留空，使用 scrollRect.content")]
    public RectTransform journalContentRoot;

    [Min(8)] public float journalRowFontSize = 16f;

    [Min(20f)] public float journalRowMinHeight = 24f;

    [Tooltip("启用列表时任务框尺寸（x/y 均大于 0 时应用）")]
    public Vector2 questPanelSizeWithJournal = new Vector2(280f, 320f);

    [Tooltip("顶部留给「当前任务」正文的纵向比例，其余为滚动列表")]
    [Range(0.15f, 0.5f)]
    public float narrativeHeightRatio = 0.28f;

    RectTransform _panelRt;
    RectTransform _journalContent;

    void Awake()
    {
        _panelRt = GetComponent<RectTransform>();
        ApplyColors();
        if (questBodyText != null && string.IsNullOrEmpty(questBodyText.text))
            questBodyText.text = defaultQuestText;

        if (!enableEventJournal)
            return;

        if (populateKnownDefaultsWhenJournalEmpty && (journalEntries == null || journalEntries.Count == 0))
            PopulateKnownNpcEventEntries();

        EnsureJournalScrollAndContent();
        ApplyJournalAnchors();
        RefreshJournal();
    }

    void OnEnable()
    {
        if (enableEventJournal)
            RefreshJournal();
    }

    void OnValidate()
    {
        ApplyColors();
    }

    void ApplyColors()
    {
        if (questFrameImage != null)
        {
            var c = questFrameImage.color;
            c.r = frameColor.r;
            c.g = frameColor.g;
            c.b = frameColor.b;
            c.a = frameColor.a;
            questFrameImage.color = c;
        }

        if (questBodyText != null)
            questBodyText.color = bodyTextColor;
    }

    void PopulateKnownNpcEventEntries()
    {
        journalEntries = new List<QuestJournalEntry>
        {
            new QuestJournalEntry { displayName = "医院取药", progressPlayerPrefsKey = "Quest_HospitalFetchMedicine_Completed", sortOrder = 0 },
            new QuestJournalEntry { displayName = "同学生病支线", progressPlayerPrefsKey = "SickClassmate_MedicineComplete", sortOrder = 1 },
            new QuestJournalEntry { displayName = "指路事件", progressPlayerPrefsKey = "DirectionGuide_BranchComplete", sortOrder = 2 },
            new QuestJournalEntry { displayName = "找车任务", progressPlayerPrefsKey = "Quest_FindBike_NPCComplete", sortOrder = 3 },
            new QuestJournalEntry { displayName = "失眠咨询", progressPlayerPrefsKey = "InsomniaAdvice_EventComplete", sortOrder = 4 },
            new QuestJournalEntry { displayName = "送伞事件", progressPlayerPrefsKey = "UmbrellaDelivery_EventComplete", sortOrder = 5 },
            new QuestJournalEntry { displayName = "等待事件", progressPlayerPrefsKey = "WaitingEvent_EventComplete", sortOrder = 6 },
            new QuestJournalEntry { displayName = "表白咨询", progressPlayerPrefsKey = "ConfessionAdvice_EventComplete", sortOrder = 7 },
            new QuestJournalEntry { displayName = "论文咨询", progressPlayerPrefsKey = "ThesisAdvice_EventComplete", sortOrder = 8 }
        };
    }

    void ApplyJournalAnchors()
    {
        if (!enableEventJournal || journalScrollRect == null || _panelRt == null)
            return;

        if (questPanelSizeWithJournal.x > 1f && questPanelSizeWithJournal.y > 1f)
            _panelRt.sizeDelta = questPanelSizeWithJournal;

        var h = Mathf.Clamp(narrativeHeightRatio, 0.15f, 0.5f);
        var scrollRt = journalScrollRect.transform as RectTransform;
        if (scrollRt != null)
        {
            scrollRt.SetAsFirstSibling();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = new Vector2(1f, 1f - h);
            scrollRt.offsetMin = new Vector2(6f, 6f);
            scrollRt.offsetMax = new Vector2(-6f, -4f);
        }

        if (questBodyText != null)
        {
            var textRt = questBodyText.rectTransform;
            textRt.SetAsLastSibling();
            textRt.anchorMin = new Vector2(0f, 1f - h);
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 0f);
            textRt.offsetMax = new Vector2(-8f, -6f);
        }
    }

    public void SetQuestText(string text)
    {
        if (questBodyText == null) return;
        questBodyText.text = string.IsNullOrEmpty(text) ? defaultQuestText : text;
    }

    public void SetFrameSprite(Sprite sprite)
    {
        if (questFrameImage == null) return;
        questFrameImage.sprite = sprite;
    }

    /// <summary>根据 PlayerPrefs 刷新底部事件列表（外部在完成任务后可调用）。</summary>
    public void RefreshJournal()
    {
        if (!enableEventJournal || journalScrollRect == null)
            return;

        var content = journalContentRoot != null ? journalContentRoot : journalScrollRect.content;
        if (content == null)
            return;
        _journalContent = content;

        for (var i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        var ordered = journalEntries != null && journalEntries.Count > 0
            ? journalEntries
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.progressPlayerPrefsKey))
                .OrderBy(e => e.sortOrder)
                .ThenBy(e => e.displayName ?? "")
                .ToList()
            : new List<QuestJournalEntry>();

        if (ordered.Count == 0)
        {
            AddJournalRow("(暂无条目：在 QuestPanelHud.journalEntries 中配置)", false);
            FinishRefresh();
            return;
        }

        foreach (var e in ordered)
        {
            var key = e.progressPlayerPrefsKey.Trim();
            var done = PlayerPrefs.GetInt(key, 0) != 0;
            var title = string.IsNullOrWhiteSpace(e.displayName) ? key : e.displayName.Trim();
            var status = done ? "[已完成]" : "[未完成]";
            var line = string.IsNullOrWhiteSpace(e.description)
                ? $"{title}　{status}"
                : $"{title}　{status}\n<color=#888888>{e.description.Trim()}</color>";
            AddJournalRow(line, true);
        }

        FinishRefresh();
    }

    void FinishRefresh()
    {
        Canvas.ForceUpdateCanvases();
        if (journalScrollRect != null)
            journalScrollRect.verticalNormalizedPosition = 1f;
    }

    void AddJournalRow(string text, bool richText)
    {
        var rowGo = new GameObject("Row", typeof(RectTransform));
        var content = _journalContent != null ? _journalContent : journalScrollRect.content;
        rowGo.transform.SetParent(content, false);
        var rt = rowGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, journalRowMinHeight);

        var le = rowGo.AddComponent<LayoutElement>();
        le.minHeight = journalRowMinHeight;
        le.preferredHeight = -1f;

        var tmp = rowGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = journalRowFontSize;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.richText = richText;
        tmp.margin = new Vector4(4f, 2f, 4f, 2f);
    }

    void EnsureJournalScrollAndContent()
    {
        if (journalScrollRect != null)
        {
            if (journalContentRoot == null)
                journalContentRoot = journalScrollRect.content;
            EnsureContentLayout(journalContentRoot);
            return;
        }

        if (!createJournalScrollIfMissing || _panelRt == null)
            return;

        var existing = _panelRt.Find(BuiltScrollChildName);
        if (existing != null)
        {
            journalScrollRect = existing.GetComponent<ScrollRect>();
            if (journalScrollRect != null)
            {
                if (journalContentRoot == null)
                    journalContentRoot = journalScrollRect.content;
                EnsureContentLayout(journalContentRoot);
            }
            return;
        }

        BuildVerticalScrollUnder(_panelRt);
    }

    void BuildVerticalScrollUnder(RectTransform panelRt)
    {
        var scrollGo = new GameObject(BuiltScrollChildName, typeof(RectTransform));
        scrollGo.transform.SetParent(panelRt, false);
        scrollGo.transform.SetAsFirstSibling();

        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(6f, 6f);
        scrollRt.offsetMax = new Vector2(-6f, -6f);

        var scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(0.12f, 0.12f, 0.14f, 0.55f);
        scrollBg.raycastTarget = true;

        journalScrollRect = scrollGo.AddComponent<ScrollRect>();
        journalScrollRect.horizontal = false;
        journalScrollRect.vertical = true;
        journalScrollRect.movementType = ScrollRect.MovementType.Clamped;
        journalScrollRect.scrollSensitivity = 24f;

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
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 4, 6);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        journalScrollRect.viewport = viewportRt;
        journalScrollRect.content = crt;
        journalContentRoot = crt;
        _journalContent = crt;
    }

    static void EnsureContentLayout(RectTransform content)
    {
        if (content == null)
            return;
        if (content.GetComponent<VerticalLayoutGroup>() == null)
        {
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(6, 6, 4, 6);
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
}
