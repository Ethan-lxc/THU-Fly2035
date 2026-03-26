using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 暂停菜单：继续、BGM 音量滑块、保存并退出；可绑定 AudioMixer 暴露参数或 AudioSource。
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public enum BgmVolumeBindMode
    {
        AudioMixerExposedParameter,
        AudioSourceVolume
    }

    [Header("根节点")]
    public CanvasGroup rootCanvasGroup;

    [Header("按钮")]
    public Button continueButton;
    public Button saveAndExitButton;
    
    [Header("按钮图片（可选）")]
    [Tooltip("继续按钮主图；留空则不覆盖现有 Image")]
    public Sprite continueButtonSprite;
    [Tooltip("保存并退出按钮主图；留空则不覆盖现有 Image")]
    public Sprite saveAndExitButtonSprite;
    [Tooltip("自动隐藏按钮下的 Text/TMP 子节点，适配纯图片按钮")]
    public bool hideButtonTextForImageUi = true;

    [Header("背景音乐音量")]
    public Slider bgmVolumeSlider;

    [Tooltip("滑块旁可选装饰图")]
    public Image bgmIconImage;

    public BgmVolumeBindMode bgmBindMode = BgmVolumeBindMode.AudioSourceVolume;

    public AudioMixer audioMixer;

    [Tooltip("须在 AudioMixer 中勾选 Exposed，并在此填写同名参数")]
    public string mixerVolumeParameterName = "BGMVolume";

    public AudioSource bgmAudioSource;

    [Tooltip("Mixer 模式下滑块记忆键（Mixer 读回数值不可靠，用本地记忆）")]
    public string playerPrefsVolumeKey = "GameplayBGMVolume";

    [Header("暂停")]
    [Tooltip("打开菜单时是否 Time.timeScale = 0")]
    public bool pauseTimeWhenOpen = true;

    [Header("事件（可选）")]
    public UnityEvent onContinueClicked;
    public UnityEvent onSaveAndExitClicked;

    [Header("调试")]
    [SerializeField] bool _isOpen;

    float _savedTimeScale = 1f;

    public bool IsOpen => _isOpen;

    void Awake()
    {
        ApplyButtonImageUi();

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);
        if (saveAndExitButton != null)
            saveAndExitButton.onClick.AddListener(OnSaveAndExit);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmSliderChanged);

        _isOpen = false;
    }

    void OnValidate()
    {
        ApplyButtonImageUi();
    }

    void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinue);
        if (saveAndExitButton != null)
            saveAndExitButton.onClick.RemoveListener(OnSaveAndExit);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
    }

    public void Show()
    {
        if (pauseTimeWhenOpen)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }

        SyncSliderFromBgmSource();
        _isOpen = true;
    }

    public void Hide()
    {
        if (pauseTimeWhenOpen)
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        _isOpen = false;
    }

    void OnContinue()
    {
        onContinueClicked?.Invoke();
        Hide();
    }

    void OnSaveAndExit()
    {
        onSaveAndExitClicked?.Invoke();
    }

    void ApplyButtonImageUi()
    {
        ApplySingleButtonImage(continueButton, continueButtonSprite);
        ApplySingleButtonImage(saveAndExitButton, saveAndExitButtonSprite);
    }

    void ApplySingleButtonImage(Button button, Sprite sprite)
    {
        if (button == null) return;
        if (sprite != null && button.image != null)
            button.image.sprite = sprite;
        if (!hideButtonTextForImageUi) return;

        for (var i = 0; i < button.transform.childCount; i++)
        {
            var child = button.transform.GetChild(i);
            if (child.GetComponent<Text>() != null)
            {
                child.gameObject.SetActive(false);
                continue;
            }

#if TMP_PRESENT
            if (child.GetComponent<TMPro.TMP_Text>() != null)
                child.gameObject.SetActive(false);
#endif
        }
    }

    void SyncSliderFromBgmSource()
    {
        if (bgmVolumeSlider == null) return;
        float v = 1f;
        switch (bgmBindMode)
        {
            case BgmVolumeBindMode.AudioSourceVolume:
                if (bgmAudioSource != null)
                    v = bgmAudioSource.volume;
                break;
            case BgmVolumeBindMode.AudioMixerExposedParameter:
                v = PlayerPrefs.GetFloat(playerPrefsVolumeKey, 1f);
                break;
        }

        bgmVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(v));
    }

    void OnBgmSliderChanged(float value)
    {
        var v = Mathf.Clamp01(value);
        switch (bgmBindMode)
        {
            case BgmVolumeBindMode.AudioSourceVolume:
                if (bgmAudioSource != null)
                    bgmAudioSource.volume = v;
                break;
            case BgmVolumeBindMode.AudioMixerExposedParameter:
                if (audioMixer != null && !string.IsNullOrEmpty(mixerVolumeParameterName))
                    audioMixer.SetFloat(mixerVolumeParameterName, v);
                break;
        }

        PlayerPrefs.SetFloat(playerPrefsVolumeKey, v);
    }
}
