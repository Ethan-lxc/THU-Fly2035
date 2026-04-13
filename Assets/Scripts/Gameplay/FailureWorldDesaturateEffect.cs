using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 失败时通过 URP 全局 Volume 将<b>摄像机画面</b>去饱和并略压暗（Screen Space Overlay UI 不受影响）。
/// 运行时在主摄像机旁挂一个全局 Volume，退出失败时关闭。
/// </summary>
[DisallowMultipleComponent]
public sealed class FailureWorldDesaturateEffect : MonoBehaviour
{
    public Camera targetCamera;

    [Tooltip("饱和度，-100 为完全黑白")]
    [Range(-100f, 0f)]
    public float saturation = -100f;

    [Tooltip("曝光补偿，略负可配合失败氛围压暗场景")]
    [Range(-2f, 0f)]
    public float postExposure = -0.45f;

    Volume _volume;
    VolumeProfile _profile;
    ColorAdjustments _colorAdjustments;
    bool _built;
    bool _savedRenderPostProcessing;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (_volume != null)
            Destroy(_volume.gameObject);
        if (_profile != null)
            Destroy(_profile);
    }

    void EnsureBuilt()
    {
        if (_built || targetCamera == null)
            return;

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        if (!_profile.TryGet(out _colorAdjustments))
            _colorAdjustments = _profile.Add<ColorAdjustments>(true);

        _colorAdjustments.active = true;
        _colorAdjustments.saturation.overrideState = true;
        _colorAdjustments.saturation.value = saturation;
        _colorAdjustments.postExposure.overrideState = true;
        _colorAdjustments.postExposure.value = postExposure;

        var go = new GameObject("FailureDesaturateVolume");
        go.transform.SetParent(targetCamera.transform, false);
        go.layer = targetCamera.gameObject.layer;

        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 2000f;
        _volume.weight = 0f;
        _volume.profile = _profile;
        _volume.enabled = true;

        _built = true;
    }

    /// <summary>与 Inspector 不同的数值时可在失败流程前调用。</summary>
    public void ApplyTuning(float saturationValue, float postExposureValue)
    {
        saturation = saturationValue;
        postExposure = postExposureValue;
        if (_colorAdjustments != null)
        {
            _colorAdjustments.saturation.value = saturationValue;
            _colorAdjustments.postExposure.value = postExposureValue;
        }
    }

    public void SetWorldFailureActive(bool active)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        EnsureBuilt();

        var data = targetCamera.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            if (active)
            {
                _savedRenderPostProcessing = data.renderPostProcessing;
                data.renderPostProcessing = true;
            }
            else
            {
                data.renderPostProcessing = _savedRenderPostProcessing;
            }
        }

        if (_volume != null)
            _volume.weight = active ? 1f : 0f;
    }
}
