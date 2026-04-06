#if UNITY_EDITOR
using Gameplay.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 一键为当前打开场景补组件，便于「医院取药」事件接线。
/// </summary>
public static class HospitalFetchMedicineSceneTools
{
    const string MenuRoot = "Tools/Gameplay/Hospital Fetch Medicine/";

    [MenuItem(MenuRoot + "Enable World Interact (E) On IsoDrone", false, 1)]
    public static void EnableWorldInteractOnDrone()
    {
        foreach (var drone in Object.FindObjectsOfType<IsoDroneController>())
        {
            Undo.RecordObject(drone, "Enable World Interact");
            drone.enableWorldInteract = true;
            drone.worldInteractDetection = IsoDroneController.WorldInteractDetectionMode.ThisRigidbodyTriggers;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem(MenuRoot + "Create Or Find InventoryRuntime", false, 2)]
    public static void EnsureInventoryRuntime()
    {
        if (Object.FindObjectOfType<InventoryRuntime>() != null)
        {
            Debug.Log("InventoryRuntime 已存在。");
            return;
        }

        var go = new GameObject("InventoryRuntime");
        Undo.RegisterCreatedObjectUndo(go, "Create InventoryRuntime");
        go.AddComponent<InventoryRuntime>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem(MenuRoot + "Create Hospital Event Root (Empty)", false, 10)]
    public static void CreateEventRoot()
    {
        var go = new GameObject("HospitalFetchMedicine_Event");
        Undo.RegisterCreatedObjectUndo(go, "Create HospitalFetchMedicine_Event");
        go.AddComponent<HospitalFetchMedicineEvent>();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    /// <summary>第二套任务（如找自行车）：仍用 HospitalFetchMedicineEventConfig + HospitalFetchMedicineEvent，仅存档键与文案不同。</summary>
    [MenuItem(MenuRoot + "Create Find Bicycle Config (same script, new asset)", false, 20)]
    public static void CreateFindBicycleMedicineTemplateConfig()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "保存找自行车任务配置（HospitalFetchMedicineEventConfig）",
            "FindBicycleQuest_MedicineTemplate",
            "asset",
            "选择保存路径");
        if (string.IsNullOrEmpty(path))
            return;

        var asset = ScriptableObject.CreateInstance<HospitalFetchMedicineEventConfig>();
        asset.dialogueLines = new[]
        {
            new DialogueLine
            {
                speaker = DialogueSpeaker.Npc,
                text = "打扰一下，我的自行车不见了，你能帮我找回来吗？",
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.Player,
                text = "我来看看。",
            },
        };
        asset.activeQuestHudText = "前往指定地点找回自行车。";
        asset.returnQuestHudText = "把自行车交还给失主。";
        asset.rewardClueId = "find_bicycle_clue";
        asset.rewardClueTitle = "自行车线索";
        asset.playerPrefsCompletedKey = "Quest_FindBicycle_Completed";
        asset.rewardAchievementTitle = "寻回自行车";

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }
}
#endif
