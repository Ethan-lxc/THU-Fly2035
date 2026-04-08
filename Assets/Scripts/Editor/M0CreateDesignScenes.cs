using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M0：生成 Boot / MainMenu / Game 空场景并写入 Editor 构建列表（批处理：M0CreateDesignScenes.CreateAll）。
/// </summary>
public static class M0CreateDesignScenes
{
    public static void CreateAll()
    {
        const string dir = "Assets/Scenes/";
        CreateAndSave($"{dir}Boot.unity");
        CreateAndSave($"{dir}MainMenu.unity");
        CreateAndSave($"{dir}Game.unity");

        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene($"{dir}Boot.unity", true),
            new EditorBuildSettingsScene($"{dir}MainMenu.unity", true),
            new EditorBuildSettingsScene($"{dir}Game.unity", true),
            new EditorBuildSettingsScene($"{dir}TitleScene.unity", true),
            new EditorBuildSettingsScene($"{dir}GameScene.unity", true),
        };
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }

    static void CreateAndSave(string path)
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
    }
}
