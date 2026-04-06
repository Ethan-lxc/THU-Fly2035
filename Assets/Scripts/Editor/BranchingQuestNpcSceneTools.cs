#if UNITY_EDITOR
using Gameplay.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 在场景中生成分支对白类 NPC（<see cref="DirectionGuideNpcController"/>）占位，已填默认进度/奖励键，公共引用尽量自动接线。
/// </summary>
public static class BranchingQuestNpcSceneTools
{
    const string MenuRoot = "Tools/Gameplay/Branching Quest NPC/";

    [MenuItem(MenuRoot + "Create Direction Guide NPC (placeholder)", false, 1)]
    public static void CreateDirectionGuideNpc()
    {
        var parent = new GameObject("DirectionGuide_BranchingNpc");
        Undo.RegisterCreatedObjectUndo(parent, "Create Direction Guide NPC");

        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            var cam = SceneView.lastActiveSceneView.camera.transform;
            parent.transform.position = cam.position + cam.forward * 4f + cam.right * 1f;
        }

        var col = parent.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 3f;

        var npc = parent.AddComponent<DirectionGuideNpcController>();

        npc.dialoguePanel = Object.FindObjectOfType<GameplayDialoguePanel>();
        var hud = Object.FindObjectOfType<GameplayHudLayout>();
        if (hud != null && hud.playerStatsHud != null)
            npc.statsHud = hud.playerStatsHud;
        else
            npc.statsHud = Object.FindObjectOfType<PlayerStatsHud>();

        npc.rewardCardOfferService = hud != null
            ? hud.rewardCardOfferService
            : Object.FindObjectOfType<RewardCardOfferService>(true);

        npc.skipPersistentProgress = false;

        npc.lineNpcAsksHelp = "（指路事件）我找不到路，能帮我指一下吗？请替换为你的正文。";
        npc.promptBranchChoices = "按 Q …，按 E …（请按你的分支改写）";
        npc.lineNpcThanksForMedicine = "（完成 E 线）谢谢，我清楚了。请替换为你的正文。";
        npc.lineNpcThanksForFood = "（Q 线）……请替换为你的正文。";

        var prompt = new GameObject("PromptRoot");
        Undo.RegisterCreatedObjectUndo(prompt, "Create Prompt Root");
        prompt.transform.SetParent(parent.transform, false);
        npc.promptRoot = prompt;
        npc.promptAnchor = parent.transform;

        EditorUtility.SetDirty(npc);
        Selection.activeGameObject = parent;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(
            "已生成 DirectionGuide_BranchingNpc（DirectionGuideNpcController）：Inspector 中补肖像、奖励卡、Animator；默认进度/奖励键已与本组件默认值一致。");
    }
}
#endif
