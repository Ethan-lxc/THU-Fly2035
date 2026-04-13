using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 槽内单张卡：左键打开全屏欣赏层；面板实现 <see cref="ICardPreviewSource"/>（成就台/背包等）。
/// </summary>
[DisallowMultipleComponent]
public class AchievementCardClickZoom : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("入库时写入，与槽位显示同源；点击欣赏层优先用此图，避免再从子 Image 里猜")]
    [SerializeField] Sprite _boundPreviewSprite;

    [SerializeField] string _boundPreviewTitle;

    ICardPreviewSource _preview;

    /// <summary>兼容旧调用：成就台入库。</summary>
    public void Configure(AchievementPanelController panel, Sprite previewSprite, string previewTitle)
    {
        Configure((ICardPreviewSource)panel, previewSprite, previewTitle);
    }

    /// <summary>应由面板在放入卡时调用，传入与显示相同的 icon/title。</summary>
    public void Configure(ICardPreviewSource preview, Sprite previewSprite, string previewTitle)
    {
        _preview = preview;
        _boundPreviewSprite = previewSprite;
        _boundPreviewTitle = previewTitle ?? string.Empty;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        var preview = _preview ?? FindPreviewSourceInHierarchy(transform);
        if (preview == null)
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

        preview.ShowAchievementCardPreview(sprite, title ?? string.Empty);
    }

    static ICardPreviewSource FindPreviewSourceInHierarchy(Transform start)
    {
        var t = start;
        while (t != null)
        {
            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb is ICardPreviewSource ips)
                    return ips;
            }

            t = t.parent;
        }

        var ach = Object.FindObjectOfType<AchievementPanelController>(true);
        if (ach != null)
            return ach;
        return Object.FindObjectOfType<InventoryPanelController>(true);
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
