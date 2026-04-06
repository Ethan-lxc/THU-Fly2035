#if UNITY_EDITOR
using Gameplay.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 集中调节分支对白 NPC（<see cref="SickClassmateNpcController"/> / <see cref="DirectionGuideNpcController"/>）的打字速度（字段名一致）。
/// </summary>
public class SickClassmateTypewriterDebugWindow : EditorWindow
{
    const string MenuPath = "Window/调试/分支对白打字机";

    [MenuItem(MenuPath)]
    public static void Open()
    {
        GetWindow<SickClassmateTypewriterDebugWindow>(false, "分支对白打字机", true);
    }

    Vector2 _scroll;
    MonoBehaviour[] _targets = System.Array.Empty<MonoBehaviour>();
    string[] _targetLabels = System.Array.Empty<string>();
    int _pick;
    SerializedObject _so;

    void OnEnable() => RefreshTargets();

    void OnFocus() => RefreshTargets();

    void RefreshTargets()
    {
        var a = FindObjectsOfType<SickClassmateNpcController>(true);
        var b = FindObjectsOfType<DirectionGuideNpcController>(true);
        var n = (a != null ? a.Length : 0) + (b != null ? b.Length : 0);
        if (n == 0)
        {
            _targets = System.Array.Empty<MonoBehaviour>();
            _targetLabels = System.Array.Empty<string>();
            _so = null;
            _pick = 0;
            return;
        }

        _targets = new MonoBehaviour[n];
        _targetLabels = new string[n];
        var i = 0;
        if (a != null)
        {
            foreach (var c in a)
            {
                _targets[i] = c;
                _targetLabels[i] = $"{c.gameObject.name} · SickClassmate";
                i++;
            }
        }
        if (b != null)
        {
            foreach (var c in b)
            {
                _targets[i] = c;
                _targetLabels[i] = $"{c.gameObject.name} · DirectionGuide";
                i++;
            }
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
        EditorGUILayout.LabelField("分支对白 · 打字机", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "NPC 与无人机使用不同「字/秒」与打字音间隔。编辑态改的是场景默认值；Play 时改动会立刻影响尚未打完的句子。",
            MessageType.Info);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("刷新场景中的分支对白 NPC"))
            RefreshTargets();

        if (_targets.Length == 0)
        {
            EditorGUILayout.HelpBox("当前场景没有 SickClassmateNpcController 或 DirectionGuideNpcController。", MessageType.Warning);
            return;
        }

        if (_targets.Length > 1)
        {
            var next = EditorGUILayout.Popup("目标实例", _pick, _targetLabels);
            if (next != _pick)
            {
                _pick = next;
                RebuildSo();
            }
        }
        else
            EditorGUILayout.LabelField("目标", _targetLabels[0]);

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
