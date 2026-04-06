using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 暂停菜单：继续、BGM 音量滑块、保存并退出；可绑定 AudioMixer 暴露参数或 AudioSource。
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    static Sprite _cachedWhiteUISprite;

    static Sprite CachedWhiteUISprite()
    {
        if (_cachedWhiteUISprite != null)
            return _cachedWhiteUISprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false);
        _cachedWhiteUISprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        _cachedWhiteUISprite.hideFlags = HideFlags.HideAndDontSave;
        return _cachedWhiteUISprite;
    }

    static void StretchFullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }

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

    [Header("音效（继续 / 退出；Time.timeScale=0 时仍播放）")]
    [Tooltip("留空则静音")]
    public AudioClip menuButtonClickClip;
    [Range(0f, 1f)]
    public float menuButtonClickVolume = 1f;

    [Header("全屏遮罩")]
    [Tooltip("可拖入 Sprite/图片作全屏遮罩底图；留空则为纯黑半透明。运行时挂在 Panels 等父节点下、位于菜单背后")]
    public Sprite pauseDimSprite;
    [Tooltip("未拖贴图时：遮罩为纯黑，Alpha 由此控制。拖贴图后：与「贴图染色」的 Alpha 相乘")]
    [Range(0f, 1f)]
    public float pauseDimAlpha = 0.65f;
    [Tooltip("仅在拖了 pauseDimSprite 时生效：乘在贴图上的颜色（白=原图，可改色调；本颜色的 Alpha 会与 pauseDimAlpha 相乘）")]
    public Color pauseDimSpriteTint = Color.white;

    [Header("调试")]
    [SerializeField] bool _isOpen;

    float _savedTimeScale = 1f;
    GameObject _fullscreenDim;
    AudioSource _menuSfx;

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

        EnsureFullscreenDimBehindMenu();
        if (_fullscreenDim != null)
            _fullscreenDim.SetActive(false);

        _isOpen = false;
    }

    void OnValidate()
    {
        ApplyButtonImageUi();
        if (Application.isPlaying && _fullscreenDim != null)
            ApplyDimAppearance(_fullscreenDim.GetComponent<Image>());
    }

    void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinue);
        if (saveAndExitButton != null)
            saveAndExitButton.onClick.RemoveListener(OnSaveAndExit);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);

        if (_fullscreenDim != null)
            Destroy(_fullscreenDim);
        _fullscreenDim = null;
    }

    void EnsureFullscreenDimBehindMenu()
    {
        if (_fullscreenDim != null)
            return;

        var parentRt = transform.parent as RectTransform;
        if (parentRt == null)
            return;

        _fullscreenDim = new GameObject("PauseFullscreenDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = _fullscreenDim.GetComponent<RectTransform>();
        rt.SetParent(parentRt, false);
        var insertAt = transform.GetSiblingIndex();
        rt.SetSiblingIndex(insertAt);

        StretchFullScreen(rt);

        var img = _fullscreenDim.GetComponent<Image>();
        ApplyDimAppearance(img);
        img.raycastTarget = true;
    }

    void ApplyDimAppearance(Image img)
    {
        if (img == null)
            return;

        img.sprite = pauseDimSprite != null ? pauseDimSprite : CachedWhiteUISprite();
        var a = Mathf.Clamp01(pauseDimAlpha);
        if (pauseDimSprite == null)
            img.color = new Color(0f, 0f, 0f, a);
        else
        {
            var c = pauseDimSpriteTint;
            c.a = Mathf.Clamp01(c.a) * a;
            img.color = c;
        }
    }

    void RefreshDimAppearance()
    {
        if (_fullscreenDim == null)
            return;
        ApplyDimAppearance(_fullscreenDim.GetComponent<Image>());
    }

    void EnsureMenuSfxSource()
    {
        if (_menuSfx != null)
            return;

        _menuSfx = GetComponent<AudioSource>();
        if (_menuSfx == null)
            _menuSfx = gameObject.AddComponent<AudioSource>();
        _menuSfx.playOnAwake = false;
        _menuSfx.loop = false;
        _menuSfx.spatialBlend = 0f;
        _menuSfx.ignoreListenerPause = true;
    }

    void PlayMenuButtonClick()
    {
        if (menuButtonClickClip == null || !Application.isPlaying)
            return;
        EnsureMenuSfxSource();
        _menuSfx.PlayOneShot(menuButtonClickClip, Mathf.Clamp01(menuButtonClickVolume));
    }

    public void Show()
    {
        EnsureFullscreenDimBehindMenu();
        RefreshDimAppearance();
        if (_fullscreenDim != null)
            _fullscreenDim.SetActive(true);

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
        if (_fullscreenDim != null)
            _fullscreenDim.SetActive(false);

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
        PlayMenuButtonClick();
        onContinueClicked?.Invoke();
        Hide();
    }

    void OnSaveAndExit()
    {
        PlayMenuButtonClick();
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
