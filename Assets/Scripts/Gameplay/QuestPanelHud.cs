using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左侧任务框：当前任务正文与背景框。环境 NPC 事件列表请使用 <see cref="NpcEventTaskBarHud"/>。
/// </summary>
public class QuestPanelHud : MonoBehaviour
{
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

    [Header("布局")]
    [Tooltip("Awake 时将任务正文 Rect 铺满面板（内边距）")]
    public bool stretchQuestBodyToFillPanel = true;

    [Min(0f)] public float bodyPadding = 8f;

    RectTransform _panelRt;

    void Awake()
    {
        _panelRt = GetComponent<RectTransform>();
        ApplyColors();
        if (questBodyText != null && string.IsNullOrEmpty(questBodyText.text))
            questBodyText.text = defaultQuestText;

        if (stretchQuestBodyToFillPanel)
            ApplyQuestBodyFillLayout();
    }

    void OnValidate()
    {
        ApplyColors();
        if (!Application.isPlaying && stretchQuestBodyToFillPanel && questBodyText != null)
            ApplyQuestBodyFillLayout();
    }

    void ApplyQuestBodyFillLayout()
    {
        if (questBodyText == null)
            return;
        var textRt = questBodyText.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        var p = bodyPadding;
        textRt.offsetMin = new Vector2(p, p);
        textRt.offsetMax = new Vector2(-p, -p);
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
}
