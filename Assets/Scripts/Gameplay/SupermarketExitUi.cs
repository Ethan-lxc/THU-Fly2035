using UnityEngine;

/// <summary>
/// 超市场景最小出口：无玩家预制体时用屏幕按钮返回主世界（依赖 DDOL 的 EventSystem 时可换为 uGUI）。
/// </summary>
public class SupermarketExitUi : MonoBehaviour
{
    void OnGUI()
    {
        const float w = 220f;
        const float h = 48f;
        if (GUI.Button(new Rect(20f, 20f, w, h), "\u8FD4\u56DE\u4E3B\u4E16\u754C"))
            GameplaySceneLoader.LoadMainWorld();
    }
}
