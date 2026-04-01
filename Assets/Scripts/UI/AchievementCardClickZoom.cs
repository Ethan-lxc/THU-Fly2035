using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 成就槽内单张卡：左键打开全屏欣赏层（半透明遮罩 + 大图），由 <see cref="AchievementPanelController"/> 提供。
/// </summary>
[DisallowMultipleComponent]
public class AchievementCardClickZoom : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("入库时写入，与槽位显示同源；点击欣赏层优先用此图，避免再从子 Image 里猜")]
    [SerializeField] Sprite _boundPreviewSprite;

    [SerializeField] string _boundPreviewTitle;

    AchievementPanelController _panel;

    /// <summary>应由 <see cref="AchievementPanelController.AddAchievementCard"/> 调用，传入与入库相同的 icon/title。</summary>
    public void Configure(AchievementPanelController panel, Sprite previewSprite, string previewTitle)
    {
        _panel = panel;
        _boundPreviewSprite = previewSprite;
        _boundPreviewTitle = previewTitle ?? string.Empty;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        var panel = _panel;
        if (panel == null)
            panel = GetComponentInParent<AchievementPanelController>();
        if (panel == null)
            panel = FindObjectOfType<AchievementPanelController>(true);
        if (panel == null)
            return;

        var sprite = _boundPreviewSprite != null ? _boundPreviewSprite : FindPrimaryCardSprite();
        if (sprite == null)
            return;

        var title = _boundPreviewTitle;
        if (string.IsNullOrEmpty(title))
        {
            var tmp = GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
                title = tmp.text;
        }

        panel.ShowAchievementCardPreview(sprite, title ?? string.Empty);
    }

    /// <summary>
    /// 取主卡图：优先本物体上的 Image（点击目标）；否则取纹理面积最大的、排除编辑器白块占位。
    /// 避免「白底/边框」排在子级遍历前面时误把白图当主图。
    /// </summary>
    Sprite FindPrimaryCardSprite()
    {
        var own = GetComponent<Image>();
        if (own != null && own.sprite != null && own.color.a > 0.05f && !IsIgnorablePreviewPlaceholder(own.sprite))
            return own.sprite;

        Image bestImg = null;
        var bestArea = 0;
        foreach (var img in GetComponentsInChildren<Image>(true))
        {
            if (img == null || img.sprite == null || img.color.a <= 0.05f)
                continue;
            if (IsIgnorablePreviewPlaceholder(img.sprite))
                continue;

            var area = GetSpritePixelArea(img.sprite);
            if (area > bestArea)
            {
                bestArea = area;
                bestImg = img;
            }
        }

        return bestImg != null ? bestImg.sprite : null;
    }

    static bool IsIgnorablePreviewPlaceholder(Sprite s)
    {
        if (s == null)
            return true;
        if (s.name == "AchievementViewer_EditorWhite")
            return true;
        return false;
    }

    /// <summary>图集子图在运行时 texture 可能为 null，用 textureRect 计算面积更可靠。</summary>
    static int GetSpritePixelArea(Sprite s)
    {
        if (s == null)
            return 0;
        var r = s.textureRect;
        var a = Mathf.RoundToInt(r.width * r.height);
        if (a > 0)
            return a;
        var t = s.texture;
        return t != null ? t.width * t.height : 1;
    }
}
