using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂在主 Canvas 根上：首次进入可玩场景时将整块 UI 迁入 DontDestroyOnLoad；再次加载 GameScene 时销毁场景内重复实例。
/// </summary>
[DefaultExecutionOrder(-32000)]
public class PersistentGameplayUiBootstrap : MonoBehaviour
{
    static PersistentGameplayUiBootstrap _instance;

    /// <summary>持久化 UI 根（与主 Canvas 同物体），供 <see cref="MapAnchorController"/> 等跨子场景解析锚点父 Canvas。</summary>
    public static PersistentGameplayUiBootstrap Instance { get; private set; }

    /// <summary>与 Intro/子场景共用的主 HUD Canvas（Screen Space）。</summary>
    public Canvas RootCanvas => GetComponent<Canvas>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameplaySceneNames.MainWorldSceneName)
            return;
        Canvas.ForceUpdateCanvases();
    }
}
