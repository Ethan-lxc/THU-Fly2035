using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 HUD 总入口：<b>Title 开场单独场景 + 本场景单 Canvas 栈</b>。
/// Hierarchy 约定在 <c>GameplayHUD</c> 下为 <c>HUD</c> / <c>Panels</c> / <c>Popups</c> 三个全屏拉伸空节点（同级顺序决定绘制先后，<c>Popups</c> 在最上）。
/// 常驻 elements、入口按钮放在 <c>HUD</c>；整块面板放 <c>Panels</c>；确认框、Toast 等放 <c>Popups</c>（必要时子节点可再挂高 <c>sortingOrder</c> 的 Canvas）。
/// 各功能保持独立 Prefab + 单一职责控制器；模块间用事件 / 接口 / SO，避免 UI 直接耦合玩法内部。
/// </summary>
public class GameplayHudLayout : MonoBehaviour
{
    [Header("分层节点（可选；留空则在 Awake 按名称 HUD / Panels / Popups 查找）")]
    [SerializeField] RectTransform hudLayerRoot;
    [SerializeField] RectTransform panelsLayerRoot;
    [SerializeField] RectTransform popupsLayerRoot;

    /// <summary>运行时 Instantiate 面板时可挂到 <c>Panels</c> 下。</summary>
    public RectTransform HudLayerRoot => hudLayerRoot;
    /// <summary>整块面板层。</summary>
    public RectTransform PanelsLayerRoot => panelsLayerRoot;
    /// <summary>弹层（Toast / 确认框）；需更高层级时在此下再加子 Canvas 调高 sortingOrder。</summary>
    public RectTransform PopupsLayerRoot => popupsLayerRoot;

    [Header("分区引用（可选，仅作总览）")]
    public MiniMapHud miniMapHud;
    public QuestPanelHud questPanelHud;
    public PlayerStatsHud playerStatsHud;
    public SettingsEntryButton settingsEntryButton;
    public PauseMenuController pauseMenuController;
    public AchievementPanelController achievementPanelController;
    public InventoryPanelController inventoryPanelController;

    [Header("点击 / 射线（解决设置等按钮点不到）")]
    [Tooltip("若 GameplayHUD 根 Rect 只有一小块，角落按钮在射线范围外；勾选时 Awake 将根 Rect 铺满父级，并尽量保持子物体在屏幕上的原位置（避免左侧 HUD 被摆出屏外）")]
    [SerializeField] bool stretchHudRootToFullScreen = true;

    [Tooltip("嵌套 Canvas 为 World Space 时绑定 Event Camera，并补 GraphicRaycaster；勿强制改为 Overlay，以免与原有布局冲突")]
    [SerializeField] bool fixNestedCanvasForRaycasts = true;

    void Awake()
    {
        BindLayerRootsByNameIfMissing();
        if (stretchHudRootToFullScreen)
            StretchHudRootToFullScreen();
        if (fixNestedCanvasForRaycasts)
            EnsureNestedCanvasRaycasts();
    }

    void BindLayerRootsByNameIfMissing()
    {
        if (hudLayerRoot == null)
            hudLayerRoot = transform.Find("HUD") as RectTransform;
        if (panelsLayerRoot == null)
            panelsLayerRoot = transform.Find("Panels") as RectTransform;
        if (popupsLayerRoot == null)
            popupsLayerRoot = transform.Find("Popups") as RectTransform;
    }

    /// <summary>打开设置/暂停菜单前：收起成就与背包，避免与暂停菜单叠在同一 <c>Panels</c> 层下互相遮挡。</summary>
    public void ClosePanelsBeforePauseMenu()
    {
        if (achievementPanelController != null)
            achievementPanelController.SetOpen(false);
        if (inventoryPanelController != null)
            inventoryPanelController.SetOpen(false);
    }

    /// <summary>打开成就前：关闭暂停菜单（恢复时间缩放）并收起背包。</summary>
    public void CloseOtherPanelsBeforeAchievement()
    {
        if (pauseMenuController != null && pauseMenuController.IsOpen)
            pauseMenuController.Hide();
        if (inventoryPanelController != null)
            inventoryPanelController.SetOpen(false);
    }

    /// <summary>打开背包前：关闭暂停菜单并收起成就。</summary>
    public void CloseOtherPanelsBeforeInventory()
    {
        if (pauseMenuController != null && pauseMenuController.IsOpen)
            pauseMenuController.Hide();
        if (achievementPanelController != null)
            achievementPanelController.SetOpen(false);
    }

    void StretchHudRootToFullScreen()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // 仅恢复「非 HUD/Panels/Popups 占位层」的直接子节点世界坐标（全屏拉伸层勿强行 SetPosition，以免和 anchor 冲突）
        var n = transform.childCount;
        var saved = new List<Vector3>(n);
        var indices = new List<int>(n);
        for (var i = 0; i < n; i++)
        {
            var c = transform.GetChild(i);
            if (c.name == "HUD" || c.name == "Panels" || c.name == "Popups")
                continue;
            indices.Add(i);
            saved.Add(c.position);
        }

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Canvas.ForceUpdateCanvases();
        for (var j = 0; j < indices.Count; j++)
            transform.GetChild(indices[j]).position = saved[j];
    }

    void EnsureNestedCanvasRaycasts()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
        {
            var cam = Camera.main;
            if (cam != null)
                canvas.worldCamera = cam;
        }

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }
}
