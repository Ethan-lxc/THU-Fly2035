using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 <see cref="ScrollRect"/> 设为静态网格用途：关闭拖拽与滚动条，viewport 铺满。
/// </summary>
public static class ScrollRectStaticGridConfigurator
{
    public static void ConfigureForStaticGrid(ScrollRect scrollRect)
    {
        if (scrollRect == null)
            return;

        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.elasticity = 0f;
        scrollRect.inertia = false;

        if (scrollRect.horizontalScrollbar != null)
            scrollRect.horizontalScrollbar.gameObject.SetActive(false);
        if (scrollRect.verticalScrollbar != null)
            scrollRect.verticalScrollbar.gameObject.SetActive(false);

        if (scrollRect.viewport != null)
        {
            var vp = scrollRect.viewport;
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.pivot = new Vector2(0f, 1f);
            vp.offsetMin = Vector2.zero;
            vp.offsetMax = Vector2.zero;
        }

        var scrollRt = scrollRect.transform as RectTransform;
        if (scrollRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRt);
    }
}
