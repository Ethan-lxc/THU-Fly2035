using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 HUD 总入口：仅聚合引用，便于在 Inspector 中一眼看到各子模块（可选）。
/// </summary>
public class GameplayHudLayout : MonoBehaviour
{
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
        if (stretchHudRootToFullScreen)
            StretchHudRootToFullScreen();
        if (fixNestedCanvasForRaycasts)
            EnsureNestedCanvasRaycasts();
    }

    void StretchHudRootToFullScreen()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        var n = transform.childCount;
        var savedWorld = new Vector3[n];
        for (var i = 0; i < n; i++)
            savedWorld[i] = transform.GetChild(i).position;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Canvas.ForceUpdateCanvases();
        for (var i = 0; i < n; i++)
            transform.GetChild(i).position = savedWorld[i];
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
