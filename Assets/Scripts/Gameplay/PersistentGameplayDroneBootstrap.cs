using UnityEngine;

/// <summary>
/// 挂在带 <see cref="IsoDroneController"/> 的玩家根物体上：首次进入可玩场景时 DontDestroyOnLoad，
/// 以便进入超市等子场景后无人机仍存在且移动逻辑不变；再次加载 GameScene 时销毁场景内新生成的重复玩家实例。
/// </summary>
[DefaultExecutionOrder(-32100)]
public class PersistentGameplayDroneBootstrap : MonoBehaviour
{
    public static PersistentGameplayDroneBootstrap Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 不用 DestroyImmediate（独立版在场景加载时易出问题）；先禁用再延后销毁，减少与主相机/物理同帧交错。
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
