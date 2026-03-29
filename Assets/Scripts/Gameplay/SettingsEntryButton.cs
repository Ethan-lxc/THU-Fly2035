using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 右上角设置入口：点击打开暂停菜单。
/// </summary>
public class SettingsEntryButton : MonoBehaviour
{
    [Header("引用")]
    public Button settingsButton;

    [Tooltip("场景中的 PauseMenuController")]
    public PauseMenuController pauseMenu;

    [Header("行为")]
    [Tooltip("若暂停菜单已打开时再次点击，是否关闭（toggle）")]
    public bool toggleIfAlreadyOpen = true;

    void Awake()
    {
        if (settingsButton == null)
            settingsButton = GetComponent<Button>();
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    void OnDestroy()
    {
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
    }

    void OnSettingsClicked()
    {
        if (pauseMenu == null)
        {
            pauseMenu = FindObjectOfType<PauseMenuController>();
            if (pauseMenu == null)
            {
                Debug.LogWarning("SettingsEntryButton: 未找到 PauseMenuController。");
                return;
            }
        }

        if (toggleIfAlreadyOpen && pauseMenu.IsOpen)
            pauseMenu.Hide();
        else
            pauseMenu.Show();
    }
}
