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
}
#endif
