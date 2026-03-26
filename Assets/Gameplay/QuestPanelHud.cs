using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左侧中部任务框：背景框 + 任务正文。
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

    void Awake()
    {
        ApplyColors();
        if (questBodyText != null && string.IsNullOrEmpty(questBodyText.text))
            questBodyText.text = defaultQuestText;
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
