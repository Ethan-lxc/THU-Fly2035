using UnityEngine;

/// <summary>
/// 挂在场景 EventSystem 上：随主流程进入 DDOL，子场景可不挂 EventSystem；返回主场景时销毁重复实例。
/// </summary>
[DefaultExecutionOrder(-31999)]
public class PersistentEventSystemBootstrap : MonoBehaviour
{
    static PersistentEventSystemBootstrap _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
