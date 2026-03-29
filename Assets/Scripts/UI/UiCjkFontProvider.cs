using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// 运行时中文字体解析：Resources → Windows Fonts 目录文件 → 系统已安装字体。
/// 将 CJK TMP_FontAsset 挂到任意主字体（如 LiberationSans / Roboto）的 fallbackFontAssetTable，缺字时由 TMP 自动回退绘制。
/// </summary>
public static class UiCjkFontProvider
{
    const string ResourcesTmpCjk = "CjkUiFont";
    const string ResourcesTmpCjkAlt = "Fonts/CjkUiFont";
    const string ResourcesBundledFont = "Fonts/NotoSansSC-Regular";
    const string CjkProbeText = "测试汉字AI";

    static readonly string[] s_osCjkFontNames =
    {
        "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "DengXian",
        "Source Han Sans SC", "Noto Sans CJK SC", "PingFang SC", "Heiti SC"
    };

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    static readonly string[] s_windowsFontFileNames =
    {
        "msyh.ttc", "msyhbd.ttc", "msyh.ttf", "simhei.ttf", "simsun.ttc", "simsunb.ttf",
        "mingliub.ttc", "msjhl.ttc", "malgun.ttf"
    };
#endif

    static TMP_FontAsset s_runtimeCjkCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BootstrapBeforeSceneLoad()
    {
        RegisterGlobalCjkFallback();
    }

    /// <summary>
    /// 将运行时 CJK 字体加入 TMP 全局 fallback，并挂到 TMP_Settings.defaultFontAsset 上（若存在）。
    /// 不依赖场景中是否挂载 IntroManager。
    /// </summary>
    public static void RegisterGlobalCjkFallback()
    {
        var cjk = GetOrCreateRuntimeCjkFontAsset();
        if (cjk == null) return;

        var globals = TMP_Settings.fallbackFontAssets;
        if (globals != null && !globals.Contains(cjk))
            globals.Add(cjk);

        var def = TMP_Settings.defaultFontAsset;
        if (def != null)
            EnsureCjkFallback(def, cjk);
    }

    /// <summary>
    /// 在 primary 上注册 cjk 作为 fallback；primary 与 cjk 为同一资源时跳过。
    /// </summary>
    public static void EnsureCjkFallback(TMP_FontAsset primary, TMP_FontAsset cjk)
    {
        if (primary == null || cjk == null) return;
        if (primary == cjk) return;
        var table = primary.fallbackFontAssetTable;
        if (table == null)
            primary.fallbackFontAssetTable = new List<TMP_FontAsset> { cjk };
        else if (!table.Contains(cjk))
            table.Add(cjk);
    }

    /// <summary>
    /// 对动态或支持 TryAddCharacters 的字体预热叙事/提示中可能出现的字符，减轻首帧方块。
    /// </summary>
    public static void WarmAtlas(TMP_FontAsset font, string text)
    {
        if (font == null || string.IsNullOrEmpty(text)) return;
        font.TryAddCharacters(text);
    }

    /// <summary>
    /// 单例式获取「仅用于 fallback」的运行时中文字体（非 Inspector 指定时的回退路径）。
    /// </summary>
    public static TMP_FontAsset GetOrCreateRuntimeCjkFontAsset()
    {
        if (s_runtimeCjkCache != null)
            return s_runtimeCjkCache;

        var r = Resources.Load<TMP_FontAsset>(ResourcesTmpCjk);
        if (r != null) return Cache(r);
        r = Resources.Load<TMP_FontAsset>(ResourcesTmpCjkAlt);
        if (r != null) return Cache(r);

        var bundled = Resources.Load<Font>(ResourcesBundledFont);
        if (bundled != null)
        {
            var created = CreateDynamicTmpFromUnityFont(bundled);
            if (created != null && ProbeCjk(created)) return Cache(created);
            if (created != null) UnityEngine.Object.Destroy(created);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (Directory.Exists(fontsDir))
        {
            foreach (var fileName in s_windowsFontFileNames)
            {
                var path = Path.Combine(fontsDir, fileName);
                if (!File.Exists(path)) continue;
                try
                {
                    var uf = new Font(path);
                    var tmp = CreateDynamicTmpFromUnityFont(uf);
                    if (tmp != null && ProbeCjk(tmp)) return Cache(tmp);
                    if (tmp != null) UnityEngine.Object.Destroy(tmp);
                }
                catch { /* ignored */ }
            }
        }
#endif

        try
        {
            var combined = Font.CreateDynamicFontFromOSFont(s_osCjkFontNames, 48);
            if (combined != null)
            {
                var tmp = CreateDynamicTmpFromUnityFont(combined);
                if (tmp != null && ProbeCjk(tmp)) return Cache(tmp);
                if (tmp != null) UnityEngine.Object.Destroy(tmp);
            }
        }
        catch { /* ignored */ }

        foreach (var name in s_osCjkFontNames)
        {
            Font f;
            try { f = Font.CreateDynamicFontFromOSFont(name, 48); }
            catch { continue; }
            if (f == null) continue;
            var tmp = CreateDynamicTmpFromUnityFont(f);
            if (tmp == null) continue;
            if (ProbeCjk(tmp)) return Cache(tmp);
            UnityEngine.Object.Destroy(tmp);
        }

        return null;
    }

    static TMP_FontAsset Cache(TMP_FontAsset a)
    {
        s_runtimeCjkCache = a;
        return a;
    }

    static bool ProbeCjk(TMP_FontAsset tmp)
    {
        return tmp.TryAddCharacters(CjkProbeText, out var miss) && string.IsNullOrEmpty(miss);
    }

    static TMP_FontAsset CreateDynamicTmpFromUnityFont(Font source)
    {
        if (source == null) return null;
        return TMP_FontAsset.CreateFontAsset(source, 48, 4, GlyphRenderMode.SDFAA, 4096, 4096, AtlasPopulationMode.Dynamic);
    }
}
