using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 为主相机守卫提供「进入 Play 时」的基准值（正交大小、Target Texture）。
/// </summary>
public static class MainCameraPropertyGuardRegistry
{
    public struct Baseline
    {
        public float OrthographicSize;
        public RenderTexture TargetTexture;
    }

    static readonly Dictionary<int, Baseline> Baselines = new Dictionary<int, Baseline>();

    public static void Register(Camera c)
    {
        if (c == null) return;
        Baselines[c.GetInstanceID()] = new Baseline
        {
            OrthographicSize = c.orthographicSize,
            TargetTexture = c.targetTexture
        };
    }

    public static void Unregister(Camera c)
    {
        if (c == null) return;
        Baselines.Remove(c.GetInstanceID());
    }

    public static void Restore(Camera c, bool lockOrthographicSize, bool lockTargetTexture)
    {
        if (c == null || !Baselines.TryGetValue(c.GetInstanceID(), out var b))
            return;

        if (lockOrthographicSize && c.orthographic)
            c.orthographicSize = b.OrthographicSize;

        if (lockTargetTexture && c.targetTexture != b.TargetTexture)
            c.targetTexture = b.TargetTexture;
    }
}

/// <summary>
/// 与 <see cref="MainCameraPropertyGuard"/> 同挂在主相机上；在极早 Awake 采样并登记基准。
/// </summary>
[DefaultExecutionOrder(-5000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class MainCameraBaselineCapture : MonoBehaviour
{
    void OnEnable()
    {
        MainCameraPropertyGuardRegistry.Register(GetComponent<Camera>());
    }

    void OnDisable()
    {
        MainCameraPropertyGuardRegistry.Unregister(GetComponent<Camera>());
    }
}
