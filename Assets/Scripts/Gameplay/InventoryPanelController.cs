using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包按钮：切换背包面板显隐；可生成占位格子。
/// </summary>
public class InventoryPanelController : MonoBehaviour
{
    [Header("引用")]
    public Button toggleButton;

    [Tooltip("可选；打开背包前关闭暂停/成就。空则打开时 FindObjectOfType")]
    public GameplayHudLayout gameplayHudLayout;

    [Tooltip("背包面板根（建议带 CanvasGroup）")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("无 CanvasGroup 时可用普通 GameObject 控制显隐")]
    public GameObject panelFallbackRoot;

    [Tooltip("格子父节点")]
    public RectTransform contentRoot;

    [Header("占位格子")]
    public GameObject slotPlaceholderPrefab;

    [Min(0)]
    public int placeholderSlotCount = 12;

    [Header("行为")]
    public bool startClosed = true;

    bool _open;

    public bool IsOpen => _open;

    void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
        BuildPlaceholders();
        SetOpen(!startClosed);
    }

    void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(Toggle);
    }

    void BuildPlaceholders()
    {
        if (contentRoot == null || slotPlaceholderPrefab == null || placeholderSlotCount <= 0)
            return;

        for (var i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        for (var i = 0; i < placeholderSlotCount; i++)
            Instantiate(slotPlaceholderPrefab, contentRoot);
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
            hud?.CloseOtherPanelsBeforeInventory();
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
    }
}
