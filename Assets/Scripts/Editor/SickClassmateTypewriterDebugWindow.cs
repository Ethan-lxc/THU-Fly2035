#if UNITY_EDITOR
using Gameplay.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 集中调节 <see cref="SickClassmateNpcController"/> 里 NPC / 无人机打字速度（与 Inspector 同一套序列化字段）。
/// </summary>
public class SickClassmateTypewriterDebugWindow : EditorWindow
{
    const string MenuPath = "Window/调试/同学对白打字机";

    [MenuItem(MenuPath)]
    public static void Open()
    {
        GetWindow<SickClassmateTypewriterDebugWindow>(false, "同学对白打字机", true);
    }

    Vector2 _scroll;
    SickClassmateNpcController[] _targets = System.Array.Empty<SickClassmateNpcController>();
    int _pick;
    SerializedObject _so;

    void OnEnable() => RefreshTargets();

    void OnFocus() => RefreshTargets();

    void RefreshTargets()
    {
        _targets = FindObjectsOfType<SickClassmateNpcController>(true);
        if (_targets == null || _targets.Length == 0)
        {
            _targets = System.Array.Empty<SickClassmateNpcController>();
            _so = null;
            _pick = 0;
            return;
        }

        _pick = Mathf.Clamp(_pick, 0, _targets.Length - 1);
        RebuildSo();
    }

    void RebuildSo()
    {
        _so = _targets.Length > 0 && _pick < _targets.Length
            ? new SerializedObject(_targets[_pick])
            : null;
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("同学求助 · 打字机", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "NPC 与无人机使用不同「字/秒」与打字音间隔。编辑态改的是场景默认值；Play 时改动会立刻影响尚未打完的句子。",
            MessageType.Info);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("刷新场景中的 SickClassmateNpcController"))
            RefreshTargets();

        if (_targets.Length == 0)
        {
            EditorGUILayout.HelpBox("当前无可选对象（场景里没有 SickClassmateNpcController）。", MessageType.Warning);
            return;
        }

        if (_targets.Length > 1)
        {
            var labels = new string[_targets.Length];
            for (var i = 0; i < _targets.Length; i++)
                labels[i] = $"{i}: {_targets[i].gameObject.name}";
            var next = EditorGUILayout.Popup("目标实例", _pick, labels);
            if (next != _pick)
            {
                _pick = next;
                RebuildSo();
            }
        }
        else
            EditorGUILayout.LabelField("目标", _targets[0].gameObject.name);

        if (_so == null || _so.targetObject != _targets[_pick])
            RebuildSo();
        if (_so == null)
            return;

        _so.Update();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawProp("npcCharsPerSecond");
        DrawProp("droneCharsPerSecond");
        DrawProp("npcMinTypewriterTickInterval");
        DrawProp("droneMinTypewriterTickInterval");
        DrawProp("typewriterSfxVolume");
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("音效引用（在 Inspector 或下方只读预览）", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            DrawProp("typewriterTickClip");
            DrawProp("typewriterTickClipDrone");
        }
        EditorGUILayout.EndScrollView();

        if (_so.ApplyModifiedProperties())
        {
            foreach (var t in _targets)
            {
                if (t != null)
                    EditorUtility.SetDirty(t);
            }
        }
    }

    static void DrawProp(SerializedObject so, string name)
    {
        var p = so.FindProperty(name);
        if (p != null)
            EditorGUILayout.PropertyField(p);
    }

    void DrawProp(string name) => DrawProp(_so, name);
}
#endif
