#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 不依赖合并场景；仅提供「Build 列表：TitleScene → GameScene」的一键同步（与 File > Build Settings 一致）。
/// </summary>
public static class TitleGameSceneTools
{
    const string TitlePath = "Assets/Scenes/TitleScene.unity";
    const string GamePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Scene/Apply Build Settings (Title + Game)", false, 0)]
    public static void ApplyBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene(TitlePath, true),
            new EditorBuildSettingsScene(GamePath, true),
        };
        EditorBuildSettings.scenes = scenes;
        AssetDatabase.SaveAssets();
        Debug.Log("Build Settings: 0 = TitleScene, 1 = GameScene");
    }

    /// <summary>batchmode: -executeMethod TitleGameSceneTools.ApplyBuildSettingsFromCommandLine</summary>
    public static void ApplyBuildSettingsFromCommandLine()
    {
        ApplyBuildSettings();
        EditorApplication.Exit(0);
    }
}
#endif
