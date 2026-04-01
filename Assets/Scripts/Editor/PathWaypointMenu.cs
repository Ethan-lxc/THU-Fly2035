#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 在 Hierarchy 里选中父物体（例如 WaypointRoot）后，菜单创建带 <see cref="PathWaypointGizmo"/> 的子路点。
/// </summary>
public static class PathWaypointMenu
{
    const string MenuPath = "GameObject/Gameplay/创建可见路点";

    [MenuItem(MenuPath, false, 10)]
    static void CreateVisibleWaypoint()
    {
        var parent = Selection.activeTransform;
        var go = new GameObject("Waypoint");
        Undo.RegisterCreatedObjectUndo(go, "Create Visible Waypoint");
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        go.AddComponent<PathWaypointGizmo>();
        Selection.activeGameObject = go;
    }

    const string SpriteMenuPath = "GameObject/Gameplay/创建图片路点（Sprite）";

    [MenuItem(SpriteMenuPath, false, 11)]
    static void CreateSpriteWaypoint()
    {
        var parent = Selection.activeTransform;
        var go = new GameObject("Waypoint_Image");
        Undo.RegisterCreatedObjectUndo(go, "Create Sprite Waypoint");
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        go.AddComponent<PathWaypointSprite2D>();
        Selection.activeGameObject = go;
    }
}
#endif
