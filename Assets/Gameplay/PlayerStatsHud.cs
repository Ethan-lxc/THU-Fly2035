using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左下角：专业度 / 情感度 数值与图标。
/// </summary>
public class PlayerStatsHud : MonoBehaviour
{
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
        ApplyLabels();
        RefreshDisplay();
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

    void RefreshDisplay()
    {
        var fmt = decimalPlaces <= 0 ? "F0" : "F" + decimalPlaces;
        if (professionalismValueText != null)
            professionalismValueText.text = _pro.ToString(fmt);
        if (emotionValueText != null)
            emotionValueText.text = _emo.ToString(fmt);
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
