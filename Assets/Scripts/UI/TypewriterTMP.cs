using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP 打字机：按字符显示，支持按字音效与 unscaled 时间。
/// </summary>
public static class TypewriterTMP
{
    public static void RevealFullText(TextMeshProUGUI tmp, string fullText)
    {
        if (tmp == null) return;
        tmp.text = fullText ?? string.Empty;
        tmp.ForceMeshUpdate();
    }

    /// <summary>推进一帧；返回 true 表示已显示完整文本。</summary>
    /// <param name="charsCarry">小数累加，避免「每帧至少 1 字」导致低速设置仍像 ~60 字/秒。新的一句开始时请置 0。</param>
    public static bool Step(
        TextMeshProUGUI tmp,
        string fullText,
        ref int visibleCharCount,
        float charsPerSecond,
        float minTickInterval,
        ref float lastTickTime,
        ref float charsCarry,
        AudioClip tickClip,
        AudioSource audioSource,
        float volume,
        float unscaledDeltaTime)
    {
        // 首帧、切到 timeScale=0 后等情况下 unscaledDeltaTime 可能很大，否则一整句会在第一帧出完。
        unscaledDeltaTime = Mathf.Clamp(unscaledDeltaTime, 0f, 1f / 60f);

        if (tmp == null || string.IsNullOrEmpty(fullText))
        {
            visibleCharCount = fullText?.Length ?? 0;
            charsCarry = 0f;
            if (tmp != null)
            {
                tmp.text = fullText ?? string.Empty;
                tmp.ForceMeshUpdate();
            }
            return true;
        }

        if (visibleCharCount >= fullText.Length)
        {
            tmp.text = fullText;
            tmp.ForceMeshUpdate();
            return true;
        }

        var prev = visibleCharCount;
        var cps = Mathf.Max(0f, charsPerSecond);
        charsCarry += cps * unscaledDeltaTime;
        var add = Mathf.FloorToInt(charsCarry);
        if (add > 0)
            charsCarry -= add;
        visibleCharCount = Mathf.Min(fullText.Length, visibleCharCount + add);

        var sb = new StringBuilder(visibleCharCount);
        for (var i = 0; i < visibleCharCount; i++)
            sb.Append(fullText[i]);
        tmp.text = sb.ToString();
        tmp.ForceMeshUpdate();

        if (tickClip != null && audioSource != null && visibleCharCount > prev)
        {
            if (Time.unscaledTime - lastTickTime >= minTickInterval)
            {
                audioSource.PlayOneShot(tickClip, volume);
                lastTickTime = Time.unscaledTime;
            }
        }

        return visibleCharCount >= fullText.Length;
    }
}
