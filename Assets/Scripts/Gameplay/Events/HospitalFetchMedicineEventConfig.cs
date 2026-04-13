using UnityEngine;

namespace Gameplay.Events
{
    [CreateAssetMenu(menuName = "Gameplay/Events/Hospital Fetch Medicine", fileName = "HospitalFetchMedicineEventConfig")]
    public class HospitalFetchMedicineEventConfig : ScriptableObject
    {
        [Header("对白")]
        public DialogueLine[] dialogueLines;

        [Tooltip("可选，名字条等扩展用")]
        public string npcDisplayName;

        public Sprite defaultNpcPortrait;
        public Sprite defaultPlayerPortrait;

        [TextArea(1, 3)]
        public string acceptPromptText = "按 空格 或 E 接受任务，按 Esc 拒绝。";

        [Tooltip("取药成功后回到 NPC 时播放的感谢对白（空格推进）；为空则仍直接完成任务（与旧版一致）")]
        public DialogueLine[] returnDialogueLines;

        [Header("任务 HUD（QuestPanelHud）")]
        [TextArea(1, 4)]
        [Tooltip("前往取药途中（GoingToPickup）；若为空则回退用 activeQuestHudText")]
        public string searchingHospitalHudText = "正在寻找医院";

        [TextArea(1, 4)]
        [Tooltip("兼容旧配置：未填 searchingHospitalHudText 时使用")]
        public string activeQuestHudText = "前往医院取药点取药。";

        [TextArea(1, 4)]
        [Tooltip("在医院取药点按 E 取药成功后，任务栏第一行提示")]
        public string pickupSuccessHudText = "取药成功";

        [TextArea(1, 4)]
        [Tooltip("取药成功后与 pickupSuccessHudText 一起显示（第二行），提示送回 NPC")]
        public string returnQuestHudText = "把药送回给同学。";

        [Header("打字机")]
        [Min(1f)]
        public float charsPerSecond = 24f;

        [Min(0.01f)]
        public float minTypewriterTickInterval = 0.04f;

        public AudioClip typewriterTickClip;

        [Range(0f, 1f)]
        public float typewriterSfxVolume = 0.85f;

        [Header("奖励")]
        public Sprite rewardAchievementCardSprite;

        [Tooltip("成就卡标题（可选）")]
        public string rewardAchievementTitle;

        public string rewardClueId = "hospital_medicine_clue";
        public string rewardClueTitle = "医院线索";
        public Sprite rewardClueIcon;

        [Header("存档键")]
        public string playerPrefsCompletedKey = "Quest_HospitalFetchMedicine_Completed";
    }
}
