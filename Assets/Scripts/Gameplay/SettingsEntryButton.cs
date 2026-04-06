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

    [Tooltip("可选；用于打开设置前收起成就/背包。空则在点击时 FindObjectOfType")]
    public GameplayHudLayout gameplayHudLayout;

    [Header("行为")]
    [Tooltip("若暂停菜单已打开时再次点击，是否关闭（toggle）")]
    public bool toggleIfAlreadyOpen = true;

    [Header("音效（Time.timeScale=0 时仍播放）")]
    [Tooltip("留空则静音")]
    public AudioClip settingsClickClip;
    [Range(0f, 1f)]
    public float settingsClickVolume = 1f;

    AudioSource _sfx;

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

    void EnsureSfxSource()
    {
        if (_sfx != null)
            return;
        _sfx = GetComponent<AudioSource>();
        if (_sfx == null)
            _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.loop = false;
        _sfx.spatialBlend = 0f;
        _sfx.ignoreListenerPause = true;
    }

    void PlaySettingsClick()
    {
        if (settingsClickClip == null || !Application.isPlaying)
            return;
        EnsureSfxSource();
        _sfx.PlayOneShot(settingsClickClip, Mathf.Clamp01(settingsClickVolume));
    }

    void OnSettingsClicked()
    {
        PlaySettingsClick();

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
        {
            pauseMenu.Hide();
            return;
        }

        var hud = gameplayHudLayout != null ? gameplayHudLayout : FindObjectOfType<GameplayHudLayout>();
        hud?.ClosePanelsBeforePauseMenu();
        pauseMenu.Show();
    }
}
