using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 从 Title 异步加载进游戏场景后：全屏黑幕由暗变亮。
/// 由 <see cref="GameplaySceneEntranceFaderHooks"/> 订阅 <see cref="SceneManager.sceneLoaded"/> 创建；无需在场景里摆放。
/// （<see cref="RuntimeInitializeLoadType.AfterSceneLoad"/> 只在<strong>第一次</strong>进游戏时触发，换场景不会再次调用，故不能用它。）
/// </summary>
public class GameplaySceneEntranceFader : MonoBehaviour
{
    public static float DurationSeconds { get; set; } = 0.85f;

    [Tooltip("遮罩 Canvas 排序，需高于 HUD")]
    [SerializeField] int canvasSortOrder = 32000;

    [Tooltip("淡出过程中拦截射线")]
    [SerializeField] bool blockRaycastsWhileFading = true;

    static Sprite s_white;

    void Awake()
    {
        if (!GameplayBgmGate.PendingGameplayFadeIn)
        {
            Destroy(gameObject);
            return;
        }

        GameplayBgmGate.PendingGameplayFadeIn = false;
        StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        var d = Mathf.Max(0.01f, DurationSeconds);

        var root = gameObject;
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null)
            canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();

        StretchFull((RectTransform)root.transform);

        var panel = new GameObject("FadePanel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        StretchFull(prt);

        var image = panel.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = Color.black;
        image.raycastTarget = blockRaycastsWhileFading;

        var cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = blockRaycastsWhileFading;
        cg.blocksRaycasts = blockRaycastsWhileFading;

        var t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / d);
            yield return null;
        }

        cg.alpha = 0f;
        Destroy(root);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    static Sprite GetWhiteSprite()
    {
        if (s_white != null) return s_white;
        var tex = Texture2D.whiteTexture;
        s_white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return s_white;
    }
}

/// <summary>每次有场景 <see cref="SceneManager.LoadScene"/> / <c>LoadSceneAsync</c> 完成并激活后检查是否需要淡入。</summary>
static class GameplaySceneEntranceFaderHooks
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GameplayBgmGate.PendingGameplayFadeIn)
            return;

        var go = new GameObject(nameof(GameplaySceneEntranceFader));
        go.AddComponent<GameplaySceneEntranceFader>();
    }
}
