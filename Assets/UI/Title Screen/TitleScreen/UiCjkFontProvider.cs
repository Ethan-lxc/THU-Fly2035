using TMPro;
using UnityEngine;

/// <summary>
/// 运行时中文 TMP 字体与回退（Resources/CjkUiFont 可选）。IntroManager 依赖此类。
/// </summary>
public static class UiCjkFontProvider
{
    public static TMP_FontAsset GetOrCreateRuntimeCjkFont()
    {
        return Resources.Load<TMP_FontAsset>("CjkUiFont");
    }

    public static void EnsureCjkFallback(TMP_FontAsset primary, TMP_FontAsset cjk)
    {
        if (primary == null || cjk == null)
            return;
        if (primary.fallbackFontAssetTable == null)
            return;
        if (primary.fallbackFontAssetTable.Contains(cjk))
            return;
        primary.fallbackFontAssetTable.Add(cjk);
    }

    public static void WarmAtlas(TMP_FontAsset cjk, string text)
    {
        if (cjk == null || string.IsNullOrEmpty(text))
            return;
    }
}
