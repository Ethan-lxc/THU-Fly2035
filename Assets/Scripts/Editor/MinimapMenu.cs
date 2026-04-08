#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MinimapMenu
{
    const string GameObjectUiRoot = "GameObject/UI/";

    /// <summary>与你已有小地图框配合：只挂 MinimapSystem，RawImage 自行拖到 minimapRawImage。</summary>
    [MenuItem(GameObjectUiRoot + "Minimap 仅缩略图源 (摄像机+RT，RawImage 自行绑定)", false, 10)]
    static void CreateThumbnailSourceOnly()
    {
        var root = new GameObject("MinimapThumbnailSource");
        Undo.RegisterCreatedObjectUndo(root, "Create Minimap Thumbnail Source");
        root.AddComponent<MinimapSystem>();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    [MenuItem(GameObjectUiRoot + "Minimap 完整搭建 (Canvas+RawImage 右上角)", false, 11)]
    static void CreateFullMinimapWithCanvas()
    {
        var root = new GameObject("MinimapSystem");
        Undo.RegisterCreatedObjectUndo(root, "Create Minimap");

        if (Selection.activeTransform != null)
        {
            Undo.SetTransformParent(root.transform, Selection.activeTransform, "Parent Minimap");
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        var sys = root.AddComponent<MinimapSystem>();

        var canvasGo = new GameObject("MinimapCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Minimap Canvas");
        canvasGo.transform.SetParent(root.transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("MinimapPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create Minimap Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(220f, 220f);

        var raw = panel.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;

        sys.minimapRawImage = raw;

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    const string PrefabPath = "Assets/Prefabs/UI/MinimapThumbnailSource.prefab";

    [MenuItem("Assets/Create/UI/Minimap 缩略图源 Prefab", false, 210)]
    static void CreateThumbnailPrefab()
    {
        EnsurePrefabFolderExists();

        var root = new GameObject("MinimapThumbnailSource");
        root.AddComponent<MinimapSystem>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    static void EnsurePrefabFolderExists()
    {
        if (AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            var e1 = AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!string.IsNullOrEmpty(e1))
                Debug.LogWarning("MinimapMenu: " + e1);
        }

        var e2 = AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        if (!string.IsNullOrEmpty(e2))
            Debug.LogWarning("MinimapMenu: " + e2);
    }
}
#endif
