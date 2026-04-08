#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MinimapSystem))]
public class MinimapSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "缩略画面只由本组件输出到 RenderTexture。\n" +
            "若你已有小地图框：在框内用 RawImage，把该 RawImage 拖到下方「Minimap Raw Image」。\n" +
            "建议把本物体放在场景根级（不要塞进 Canvas）；子物体里的世界相机会正常渲染场景。",
            MessageType.Info);

        DrawDefaultInspector();
    }
}
#endif
