using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左下角：专业度 / 情感度 数值与图标。
/// </summary>
public class PlayerStatsHud : MonoBehaviour
{
    /// <summary>运行中任意数值写入后触发（含 Set / Add / Awake 首刷）。参数为当前专业、情感。</summary>
    public event Action<float, float> StatsChanged;

    [Header("引用")]
    public Image professionalismIcon;
    public Image emotionIcon;

    public TextMeshProUGUI professionalismLabel;
    public TextMeshProUGUI emotionLabel;

    public TextMeshProUGUI professionalismValueText;
    public TextMeshProUGUI emotionValueText;

    [Header("标签文案")]
    public string professionalismLabelText = "专业度";
    public string emotionLabelText = "情感度";

    [Header("数值")]
    [Tooltip("初始专业度")]
    public float initialProfessionalism;

    [Tooltip("初始情感度")]
    public float initialEmotion;

    [Tooltip("显示到小数位；0 表示整数")]
    [Min(0)]
    public int decimalPlaces = 0;

    float _pro;
    float _emo;

    void Awake()
    {
        _pro = initialProfessionalism;
        _emo = initialEmotion;
        EnsureCjkFallbackForTmpChildren();
        ApplyLabels();
        RefreshDisplay();
        WarmCjkForCurrentHudText();
    }

    /// <summary>
    /// 标签可能使用 Anton SDF 等不含 CJK 的字体；为 HUD 内 TMP 主字体挂上 <see cref="UiCjkFontProvider"/> 的 fallback。
    /// </summary>
    void EnsureCjkFallbackForTmpChildren()
    {
        var cjk = UiCjkFontProvider.GetOrCreateRuntimeCjkFontAsset();
        if (cjk == null) return;

        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font != null)
                UiCjkFontProvider.EnsureCjkFallback(tmp.font, cjk);
        }
    }

    void WarmCjkForCurrentHudText()
    {
        var cjk = UiCjkFontProvider.GetOrCreateRuntimeCjkFontAsset();
        if (cjk == null) return;

        var s = professionalismLabelText + emotionLabelText;
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!string.IsNullOrEmpty(tmp.text))
                s += tmp.text;
        }

        UiCjkFontProvider.WarmAtlas(cjk, s);
    }

    void ApplyLabels()
    {
        if (professionalismLabel != null)
            professionalismLabel.text = professionalismLabelText;
        if (emotionLabel != null)
            emotionLabel.text = emotionLabelText;
    }

    void OnValidate()
    {
        ApplyLabels();
        _pro = initialProfessionalism;
        _emo = initialEmotion;
        RefreshDisplay();
    }

    public void SetProfessionalism(float value)
    {
        _pro = value;
        RefreshDisplay();
    }

    public void SetEmotion(float value)
    {
        _emo = value;
        RefreshDisplay();
    }

    public void SetStats(float professionalism, float emotion)
    {
        _pro = professionalism;
        _emo = emotion;
        RefreshDisplay();
    }

    public void AddProfessionalism(float delta)
    {
        _pro += delta;
        RefreshDisplay();
    }

    public void AddEmotion(float delta)
    {
        _emo += delta;
        RefreshDisplay();
    }

    public float Professionalism => _pro;
    public float Emotion => _emo;

    void RefreshDisplay()
    {
        var fmt = decimalPlaces <= 0 ? "F0" : "F" + decimalPlaces;
        if (professionalismValueText != null)
            professionalismValueText.text = _pro.ToString(fmt);
        if (emotionValueText != null)
            emotionValueText.text = _emo.ToString(fmt);

        if (Application.isPlaying)
            StatsChanged?.Invoke(_pro, _emo);
    }

    public void SetProfessionalismIcon(Sprite sprite)
    {
        if (professionalismIcon != null)
            professionalismIcon.sprite = sprite;
    }

    public void SetEmotionIcon(Sprite sprite)
    {
        if (emotionIcon != null)
            emotionIcon.sprite = sprite;
    }
}
