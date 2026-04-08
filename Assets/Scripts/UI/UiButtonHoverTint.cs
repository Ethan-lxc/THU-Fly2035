using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一为 <see cref="Button"/> 配置 ColorTint：悬停/按下时相对变暗（与图标 <see cref="Image.color"/> 相乘）。
/// </summary>
public static class UiButtonHoverTint
{
    public static void Apply(
        Button button,
        float hoverBrightness = 0.78f,
        float pressedBrightness = 0.65f,
        float fadeDuration = 0.08f)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.ColorTint;
        var c = button.colors;
        c.normalColor = Color.white;
        var h = Mathf.Clamp(hoverBrightness, 0.4f, 1f);
        var p = Mathf.Clamp(pressedBrightness, 0.35f, 1f);
        c.highlightedColor = new Color(h, h, h, 1f);
        c.pressedColor = new Color(p, p, p, 1f);
        c.selectedColor = c.highlightedColor;
        c.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        c.colorMultiplier = 1f;
        c.fadeDuration = Mathf.Max(0.01f, fadeDuration);
        button.colors = c;
    }
}
