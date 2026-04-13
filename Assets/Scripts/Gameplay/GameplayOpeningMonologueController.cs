using System.Collections;
using Gameplay.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 进入游戏场景后延迟播放开场内心独白（<see cref="GameplayDialoguePanel.BeginNarrativeSequence"/>），使用现有 GameplayDialoguePanel_Root，不新建 UI。
/// 纯剧情介绍：不经过 <see cref="RewardCardOfferService"/>，不发放奖励卡。头像可在配置里用无人机立绘（见 <see cref="HospitalFetchMedicineEventConfig.defaultPlayerPortrait"/>）。
/// </summary>
[DefaultExecutionOrder(200)]
public class GameplayOpeningMonologueController : MonoBehaviour
{
    [Tooltip("空则在运行时查找（含未激活物体上的组件）")]
    public GameplayDialoguePanel dialoguePanel;

    [Tooltip("独白文案；每条 Speaker 选 Player 时仅左侧显示头像。在资源 Inspector 将 Default Player Portrait 设为无人机图即可「只有无人机头像」介绍剧情；本组件不读 Config 里的奖励/成就字段。")]
    public HospitalFetchMedicineEventConfig monologueConfig;

    [Tooltip("应略大于 IntroManager.gameSceneEntranceFadeSeconds（默认约 0.85），否则开场全屏淡入会盖在对白之上；即便已提升 Canvas 排序，也建议 ≥1 秒以错开淡入动画。")]
    [Min(0f)]
    public float delaySecondsAfterStart = 1.15f;

    [Tooltip("勾选且 PlayerPrefs 已标记则跳过（新游戏/清档后可再播）")]
    public bool showOnlyOnce = true;

    [Tooltip("showOnlyOnce 为 true 时写入 1 表示已播过")]
    public string playerPrefsShownKey = "OpeningInnerMonologue_Shown";

    [Tooltip("仅编辑器：不读、不写 PlayerPrefs，便于反复测。发布包仍正常存档。")]
    public bool editorSkipPlayerPrefs = true;

    [Tooltip("若对白面板正忙，每隔多久再尝试一次 BeginNarrativeSequence")]
    [Min(0.05f)]
    public float retryIntervalSeconds = 0.35f;

    [Min(1)]
    public int maxStartAttempts = 12;

    [Tooltip("留空则不限制；填 GameScene 时仅在进入该场景名时尝试播放（避免同脚本被误挂到其它场景）")]
    public string onlyPlayInSceneName = "GameScene";

    IEnumerator Start()
    {
        if (!string.IsNullOrEmpty(onlyPlayInSceneName) &&
            SceneManager.GetActiveScene().name != onlyPlayInSceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"GameplayOpeningMonologue: 当前场景为「{SceneManager.GetActiveScene().name}」，与 onlyPlayInSceneName「{onlyPlayInSceneName}」不一致，已跳过。请把 IntroManager 的加载场景改为含本组件与 GameplayDialoguePanel 的场景，或清空 onlyPlayInSceneName。",
                this);
#endif
            yield break;
        }

        if (ShouldUsePlayerPrefs() &&
            PlayerPrefs.GetInt(playerPrefsShownKey, 0) != 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"GameplayOpeningMonologue: 已跳过（PlayerPrefs「{playerPrefsShownKey}」=1）。取消勾选 showOnlyOnce 或删该键可再播。",
                this);
#endif
            yield break;
        }

        yield return null;
        yield return null;

        ResolveDialoguePanel();
        if (dialoguePanel == null || monologueConfig == null ||
            monologueConfig.dialogueLines == null || monologueConfig.dialogueLines.Length == 0)
        {
            Debug.LogWarning(
                "GameplayOpeningMonologue: 未找到 GameplayDialoguePanel 或 monologueConfig 无效，无法播放开场独白。",
                this);
            yield break;
        }

        if (delaySecondsAfterStart > 0f)
            yield return new WaitForSecondsRealtime(delaySecondsAfterStart);

        for (var attempt = 0; attempt < maxStartAttempts; attempt++)
        {
            if (dialoguePanel == null)
                ResolveDialoguePanel();
            if (dialoguePanel == null)
                break;

            if (!dialoguePanel.IsOpen &&
                dialoguePanel.BeginNarrativeSequence(monologueConfig, OnMonologueComplete))
                yield break;

            yield return new WaitForSecondsRealtime(retryIntervalSeconds);
        }

        Debug.LogWarning(
            "GameplayOpeningMonologue: 多次尝试后仍无法开始独白（对白面板可能一直处于占用状态）。请检查是否有其它系统在同时打开 GameplayDialoguePanel。",
            this);
    }

    void ResolveDialoguePanel()
    {
        if (dialoguePanel != null)
            return;

        dialoguePanel = FindObjectOfType<GameplayDialoguePanel>(true);
    }

    bool ShouldUsePlayerPrefs()
    {
        if (!showOnlyOnce || string.IsNullOrEmpty(playerPrefsShownKey))
            return false;
#if UNITY_EDITOR
        if (editorSkipPlayerPrefs)
            return false;
#endif
        return true;
    }

    void OnMonologueComplete()
    {
        if (!ShouldUsePlayerPrefs())
            return;
        PlayerPrefs.SetInt(playerPrefsShownKey, 1);
        PlayerPrefs.Save();
    }
}
