using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 右中：成就按钮切换成就面板；可生成占位卡片。
/// </summary>
public class AchievementPanelController : MonoBehaviour
{
    [Header("引用")]
    public Button toggleButton;

    [Tooltip("可选；打开成就前关闭暂停/背包。空则打开时 FindObjectOfType")]
    public GameplayHudLayout gameplayHudLayout;

    [Tooltip("成就面板根（建议带 CanvasGroup）")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("无 CanvasGroup 时可用普通 GameObject，仅用 SetActive 显隐")]
    public GameObject panelFallbackRoot;

    [Tooltip("可选：列表滚动")]
    public ScrollRect scrollRect;

    [Tooltip("卡片父节点；空则使用 ScrollRect.content")]
    public RectTransform contentRoot;

    [Header("占位卡片")]
    public GameObject achievementCardPrefab;

    [Min(0)]
    public int placeholderCardCount = 6;

    [Header("行为")]
    public bool startClosed = true;

    bool _open;

    public bool IsOpen => _open;

    void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
        ResolveContentRoot();
        BuildPlaceholders();
        SetOpen(!startClosed);
    }

    void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(Toggle);
    }

    void ResolveContentRoot()
    {
        if (contentRoot == null && scrollRect != null)
            contentRoot = scrollRect.content;
    }

    void BuildPlaceholders()
    {
        ResolveContentRoot();
        if (contentRoot == null || achievementCardPrefab == null || placeholderCardCount <= 0)
            return;

        for (var i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        for (var i = 0; i < placeholderCardCount; i++)
            Instantiate(achievementCardPrefab, contentRoot);
    }

    public void Toggle()
    {
        SetOpen(!_open);
    }

    public void SetOpen(bool open)
    {
        if (open)
        {
            var hud = gameplayHudLayout != null ? gameplayHudLayout : FindObjectOfType<GameplayHudLayout>();
            hud?.CloseOtherPanelsBeforeAchievement();
        }

        _open = open;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = open ? 1f : 0f;
            panelCanvasGroup.interactable = open;
            panelCanvasGroup.blocksRaycasts = open;
        }
        else if (panelFallbackRoot != null)
            panelFallbackRoot.SetActive(open);
        else if (scrollRect != null)
            scrollRect.gameObject.SetActive(open);
    }
}
