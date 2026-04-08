using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左上角小地图：背景图预留；可另挂子物体作边框。
/// </summary>
public class MiniMapHud : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("小地图底图（Image）")]
    public Image minimapBackground;

    [Tooltip("可选：小地图框插槽。可在其子级放 RawImage 显示实时缩略图，并把该 RawImage 拖到场景里 MinimapSystem 的 Minimap Raw Image 上。")]
    public RectTransform frameSlot;

    [Header("外观")]
    [Tooltip("未单独指定 Sprite 时的占位色")]
    public Color backgroundColor = new Color(0.15f, 0.2f, 0.25f, 0.85f);

    [Tooltip("区域缩放系数（1=保持 RectTransform 尺寸）")]
    [Min(0.1f)]
    public float areaScale = 1f;

    void Awake()
    {
        ApplyVisuals();
    }

    void OnValidate()
    {
        ApplyVisuals();
    }

    void ApplyVisuals()
    {
        if (minimapBackground == null) return;
        var c = minimapBackground.color;
        c.r = backgroundColor.r;
        c.g = backgroundColor.g;
        c.b = backgroundColor.b;
        c.a = backgroundColor.a;
        minimapBackground.color = c;
        if (minimapBackground.sprite == null && minimapBackground.canvasRenderer != null)
            minimapBackground.enabled = true;

        if (minimapBackground.rectTransform != null && !Mathf.Approximately(areaScale, 1f))
            minimapBackground.rectTransform.localScale = Vector3.one * areaScale;
    }

    /// <summary>运行时替换底图（例如任务系统切换区域）。</summary>
    public void SetBackgroundSprite(Sprite sprite)
    {
        if (minimapBackground == null) return;
        minimapBackground.sprite = sprite;
        minimapBackground.enabled = sprite != null;
    }
}
