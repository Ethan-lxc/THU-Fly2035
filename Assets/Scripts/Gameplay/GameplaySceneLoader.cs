using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 主世界与超市场景的单一加载入口（Single 模式）。
/// 从主世界进超市时：加载完成后将无人机放到名为 EntrySpawnMarker 的物体处。
/// 从超市回主世界时：加载完成后将无人机放到名为 SupermarketReturnSpawn 的物体处（若无则回退到 SupermarketPortal）。
/// </summary>
public static class GameplaySceneLoader
{
    static bool _subscribed;

    static bool _pendingSpawnAtSupermarketEntry;
    static bool _pendingSpawnAtMainWorldDoor;

    static void EnsureSubscribed()
    {
        if (_subscribed)
            return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _subscribed = true;
    }

    public static void LoadMainWorld()
    {
        EnsureSubscribed();
        if (SceneManager.GetActiveScene().name == GameplaySceneNames.SupermarketSceneName)
            _pendingSpawnAtMainWorldDoor = true;

        GameplayBgmGate.EnteredFromStartButton = true;
        SceneManager.LoadScene(GameplaySceneNames.MainWorldSceneName, LoadSceneMode.Single);
    }

    public static void LoadSupermarket()
    {
        EnsureSubscribed();
        if (SceneManager.GetActiveScene().name == GameplaySceneNames.MainWorldSceneName)
            _pendingSpawnAtSupermarketEntry = true;

        SceneManager.LoadScene(GameplaySceneNames.SupermarketSceneName, LoadSceneMode.Single);
    }

    /// <summary>与常量表一致时用封装；否则按名称直载（不自动设置传送点）。</summary>
    public static void LoadSceneByGameplayName(string sceneName)
    {
        if (sceneName == GameplaySceneNames.MainWorldSceneName)
        {
            LoadMainWorld();
            return;
        }

        if (sceneName == GameplaySceneNames.SupermarketSceneName)
        {
            LoadSupermarket();
            return;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        if (scene.name == GameplaySceneNames.SupermarketSceneName && _pendingSpawnAtSupermarketEntry)
        {
            _pendingSpawnAtSupermarketEntry = false;
            ApplyDroneToNamedMarker("EntrySpawnMarker");
        }
        else if (scene.name == GameplaySceneNames.MainWorldSceneName && _pendingSpawnAtMainWorldDoor)
        {
            _pendingSpawnAtMainWorldDoor = false;
            if (!ApplyDroneToNamedMarker("SupermarketReturnSpawn"))
                ApplyDroneToNamedMarker("SupermarketPortal");
        }
    }

    /// <summary>将无人机移动到指定名称物体的世界坐标（保留原 z）。</summary>
    public static bool ApplyDroneToNamedMarker(string goName)
    {
        var root = PersistentGameplayDroneBootstrap.Instance;
        if (root == null)
            return false;

        var marker = GameObject.Find(goName);
        if (marker == null)
            return false;

        var t = root.transform;
        var p = marker.transform.position;
        t.position = new Vector3(p.x, p.y, t.position.z);
        var drone = t.GetComponent<IsoDroneController>();
        if (drone != null)
            drone.SyncMovementStateAfterTeleport();
        return true;
    }
}
