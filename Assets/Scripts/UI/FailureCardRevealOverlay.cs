using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 失败全屏卡：应挂在场景 <b>GameplayHUD / FailureHud</b> 上（<see cref="GameplayHudLayout.FailureHudRoot"/>）；
/// 实现方式对齐 <see cref="RewardCardRevealPopup"/>，Awake 里 <see cref="EnsureBuilt"/> 拼 Dim + 卡图 + 标题 + 提示，并用嵌套 <see cref="Canvas"/> 抬高 sortingOrder。
/// 若场景未放置，<see cref="GetOrCreate"/> 会在 FailureHud 或 Popups 下动态生成。
/// </summary>
[DisallowMultipleComponent]
public sealed class FailureCardRevealOverlay : MonoBehaviour
{
    static FailureCardRevealOverlay _cachedInstance;

    [Header("自动生成后可在 Inspector 看到引用；一般无需手摆")]
    public CanvasGroup rootCanvasGroup;
    public Image dimImage;
    public Image cardImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI hintText;

    bool _built;

    public static FailureCardRevealOverlay GetOrCreate()
    {
        if (_cachedInstance != null)
            return _cachedInstance;

        var hud = FindObjectOfType<GameplayHudLayout>();
        if (hud != null && hud.FailureHudRoot != null)
        {
            var onFailureHud = hud.FailureHudRoot.GetComponent<FailureCardRevealOverlay>();
            if (onFailureHud != null)
            {
                _cachedInstance = onFailureHud;
                return onFailureHud;
            }
        }

        var existing = FindObjectOfType<FailureCardRevealOverlay>(true);
        if (existing != null)
        {
            _cachedInstance = existing;
            return existing;
        }

        RectTransform parent = null;
        if (hud != null && hud.FailureHudRoot != null)
            parent = hud.FailureHudRoot;
        if (parent == null && hud != null && hud.PopupsLayerRoot != null)
            parent = hud.PopupsLayerRoot;
        if (parent == null)
        {
            var pop = GameObject.Find("Popups");
            if (pop != null)
                parent = pop.transform as RectTransform;
        }

        if (parent == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                parent = canvas.transform as RectTransform;
        }

        if (parent == null)
        {
            var pf = FindObjectOfType<PlayerFailureController>();
            if (pf != null)
                parent = pf.transform as RectTransform;
        }

        if (parent == null)
            return null;

        var go = new GameObject("FailureCardRevealOverlay", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        StretchFull(rt);
        go.transform.SetAsLastSibling();
        go.layer = parent.gameObject.layer;

        var comp = go.AddComponent<FailureCardRevealOverlay>();
        _cachedInstance = comp;
        return comp;
    }

    void Awake()
    {
        EnsureBuilt();
        HideImmediate();
    }

    void OnDestroy()
    {
        if (_cachedInstance == this)
            _cachedInstance = null;
    }

    /// <summary>展示卡面 → 延时 → 提示重生操作 → 等到 Space / 左键，再收起。</summary>
    public IEnumerator RunPresentation(
        Sprite sprite,
        string title,
        Color dimColor,
        float delayBeforeHintSeconds,
        string respawnHint)
    {
        EnsureBuilt();
        if (rootCanvasGroup == null)
            yield break;

        if (dimImage != null)
            dimImage.color = dimColor;

        if (cardImage != null)
        {
            cardImage.sprite = sprite;
            cardImage.enabled = sprite != null;
        }

        if (titleText != null)
        {
            var t = title ?? string.Empty;
            titleText.text = t;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(t));
        }

        if (hintText != null)
        {
            hintText.text = string.Empty;
            hintText.gameObject.SetActive(false);
        }

        transform.SetAsLastSibling();
        rootCanvasGroup.alpha = 1f;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;
        Canvas.ForceUpdateCanvases();

        if (delayBeforeHintSeconds > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeHintSeconds);

        if (hintText != null)
        {
            var h = respawnHint ?? string.Empty;
            hintText.text = h;
            hintText.gameObject.SetActive(!string.IsNullOrEmpty(h));
        }

        Canvas.ForceUpdateCanvases();

        while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetMouseButtonDown(0))
            yield return null;

        HideImmediate();
    }

    public void HideImmediate()
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        // 与 RewardCardRevealPopup 一致：勿 SetActive(false) 整层，否则 FailureHud 被关后 Find 不到、序列化未拖引用时会丢绑定。
    }

    void EnsureBuilt()
    {
        if (_built)
            return;

        var rt = transform as RectTransform;
        if (rt != null)
            StretchFull(rt);

        if (GetComponent<Canvas>() == null)
        {
            var cv = gameObject.AddComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = 32760;
        }

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        rootCanvasGroup = GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null)
            rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (dimImage == null)
        {
            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dimGo.transform.SetParent(transform, false);
            var dimRt = dimGo.GetComponent<RectTransform>();
            StretchFull(dimRt);
            dimImage = dimGo.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.78f);
            dimImage.raycastTarget = true;
        }

        BuildCardStack();
        _built = true;
    }

    void BuildCardStack()
    {
        var stack = transform.Find("FailureStack");
        if (stack == null)
        {
            var stackGo = new GameObject("FailureStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
            stack = stackGo.transform;
            stack.SetParent(transform, false);
            var stackRt = stack.GetComponent<RectTransform>();
            stackRt.anchorMin = stackRt.anchorMax = new Vector2(0.5f, 0.5f);
            stackRt.sizeDelta = new Vector2(440f, 580f);
            stackRt.anchoredPosition = Vector2.zero;
            var v = stack.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleCenter;
            v.spacing = 18f;
            v.childControlHeight = false;
            v.childControlWidth = false;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = false;
        }

        if (cardImage == null)
        {
            var cardGo = new GameObject("CardIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardGo.transform.SetParent(stack, false);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(300f, 380f);
            cardImage = cardGo.GetComponent<Image>();
            cardImage.preserveAspect = true;
            cardImage.color = Color.white;
            cardImage.raycastTarget = false;
        }

        if (titleText == null)
        {
            var tGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(stack, false);
            var tRt = tGo.GetComponent<RectTransform>();
            tRt.sizeDelta = new Vector2(420f, 52f);
            titleText = tGo.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 28f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                titleText.font = TMP_Settings.defaultFontAsset;
        }

        if (hintText == null)
        {
            var hGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hGo.transform.SetParent(stack, false);
            var hRt = hGo.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(560f, 80f);
            hintText = hGo.GetComponent<TextMeshProUGUI>();
            hintText.fontSize = 22f;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(1f, 0.92f, 0.65f);
            if (TMP_Settings.defaultFontAsset != null)
                hintText.font = TMP_Settings.defaultFontAsset;
        }
    }

    static void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
    }
}
