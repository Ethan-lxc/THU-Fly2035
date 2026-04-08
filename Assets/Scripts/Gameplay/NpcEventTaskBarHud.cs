using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 环境事件任务栏 HUD 入口：<b>仅图标</b>（点击音效）。实际列表与成就台一样放在
/// <c>GameplayHUD → Panels</c> 下的 <see cref="NpcEventJournalPanelController"/>。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class NpcEventTaskBarHud : MonoBehaviour
{
    const string BuiltIconName = "NpcEventTaskBar_Icon";

    [Header("目标面板（Panels 层）")]
    [Tooltip("空则运行时查找 NpcEventJournalPanelController")]
    public NpcEventJournalPanelController journalPanel;

    [Header("图标区域")]
    [Min(24f)] public float iconWidth = 56f;

    [Min(24f)] public float iconHeight = 56f;

    public Sprite iconSprite;

    public Color iconColor = Color.white;

    [Header("悬停 / 按下（ColorTint 与图标色相乘）")]
    [Tooltip("鼠标悬停时亮度系数，越小越暗")]
    [Range(0.4f, 1f)] public float hoverBrightness = 0.78f;

    [Tooltip("按下时亮度系数")]
    [Range(0.35f, 1f)] public float pressedBrightness = 0.65f;

    [Min(0.01f)] public float colorTransitionDuration = 0.08f;

    [Header("点击音效")]
    public AudioClip iconClickClip;

    [Range(0f, 1f)] public float iconClickVolume = 1f;

    RectTransform _panelRt;
    AudioSource _uiAudio;

    void OnEnable()
    {
        if (!Application.isPlaying)
            RefreshEditorOrStandaloneVisuals();
    }

    void Awake()
    {
        _panelRt = transform as RectTransform;
        if (journalPanel == null)
            journalPanel = FindObjectOfType<NpcEventJournalPanelController>(true);

        if (journalPanel == null)
        {
            Debug.LogWarning(
                $"{nameof(NpcEventTaskBarHud)}: 未找到 {nameof(NpcEventJournalPanelController)}，请放在场景中并指定引用（建议在 Panels 下，与成就台同级）。",
                this);
        }

        DisableRootGraphicForIconOnly();
        ApplyRootSize();
        EnsureIconButton(true);
    }

    void RefreshEditorOrStandaloneVisuals()
    {
        _panelRt = transform as RectTransform;
        DisableRootGraphicForIconOnly();
        ApplyRootSize();
        EnsureIconButton(false);
    }

    void DisableRootGraphicForIconOnly()
    {
        var rootImg = GetComponent<Image>();
        if (rootImg != null)
            rootImg.enabled = false;
    }

    void ApplyRootSize()
    {
        if (_panelRt != null)
            _panelRt.sizeDelta = new Vector2(iconWidth, iconHeight);
    }

    void EnsureIconButton(bool registerClick)
    {
        var rt = transform as RectTransform;
        if (rt == null)
            return;
        if (_panelRt == null)
            _panelRt = rt;

        Transform iconTf = transform.Find(BuiltIconName);
        GameObject iconGo;
        if (iconTf == null)
        {
            iconGo = new GameObject(BuiltIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            iconGo.transform.SetParent(_panelRt, false);
        }
        else
            iconGo = iconTf.gameObject;

        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;

        var iconImage = iconGo.GetComponent<Image>();
        iconImage.sprite = iconSprite;
        iconImage.color = iconColor;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = true;

        var iconButton = iconGo.GetComponent<Button>();
        iconButton.targetGraphic = iconImage;
        iconButton.transition = Selectable.Transition.ColorTint;
        UiButtonHoverTint.Apply(iconButton, hoverBrightness, pressedBrightness, colorTransitionDuration);
        iconButton.onClick.RemoveAllListeners();
        if (registerClick)
            iconButton.onClick.AddListener(OnIconClicked);
    }

    void OnIconClicked()
    {
        PlayIconClickSound();
        if (journalPanel != null)
            journalPanel.Toggle();
    }

    void PlayIconClickSound()
    {
        if (iconClickClip == null)
            return;
        if (_uiAudio == null)
        {
            _uiAudio = GetComponent<AudioSource>();
            if (_uiAudio == null)
                _uiAudio = gameObject.AddComponent<AudioSource>();
            _uiAudio.playOnAwake = false;
        }

        _uiAudio.PlayOneShot(iconClickClip, iconClickVolume);
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            RefreshEditorOrStandaloneVisuals();
    }
}
