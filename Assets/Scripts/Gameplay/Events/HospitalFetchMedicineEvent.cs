using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>医院取药：对话 → 接受 → 取药点 → 送回 NPC → 奖励。</summary>
    public class HospitalFetchMedicineEvent : MonoBehaviour
    {
        public HospitalFetchMedicineEventConfig config;

        [Header("引用")]
        public GameplayDialoguePanel dialoguePanel;

        public QuestPanelHud questPanelHud;

        public AchievementPanelController achievementPanel;

        public InventoryRuntime inventoryRuntime;

        public InventoryPanelController inventoryPanel;

        [Tooltip("取药点世界提示根（含 BobbingWorldSprite）；接受任务后显示")]
        public GameObject pickupPromptRoot;

        [Tooltip("取药点 Collider 所在物体，用于隐藏/显示")]
        public GameObject pickupColliderRoot;

        [Tooltip("绑定的同学 NPC；开局不舒服动画，送药完成后切愉快动画")]
        public NpcInteractable questNpc;

        public enum QuestPhase
        {
            Inactive,
            AwaitingDialogueAccept,
            GoingToPickup,
            HasMedicine,
            Completed
        }

        [SerializeField] QuestPhase _phase = QuestPhase.Inactive;

        public QuestPhase Phase => _phase;

        void Awake()
        {
            if (inventoryRuntime == null)
                inventoryRuntime = FindObjectOfType<InventoryRuntime>();
            if (pickupPromptRoot != null)
                pickupPromptRoot.SetActive(false);
            if (pickupColliderRoot != null)
                pickupColliderRoot.SetActive(false);

            if (config != null && !string.IsNullOrEmpty(config.playerPrefsCompletedKey) &&
                PlayerPrefs.GetInt(config.playerPrefsCompletedKey, 0) == 1)
                _phase = QuestPhase.Completed;

            SyncNpcMoodToQuest();
        }

        public bool CanInteractNpc(NpcInteractable npc)
        {
            if (config == null || dialoguePanel == null)
                return false;
            if (_phase == QuestPhase.Completed)
                return false;
            if (_phase == QuestPhase.AwaitingDialogueAccept)
                return false;
            if (_phase == QuestPhase.GoingToPickup)
                return false;
            if (_phase == QuestPhase.HasMedicine)
                return true;
            return _phase == QuestPhase.Inactive;
        }

        public bool CanPickupMedicine()
        {
            return _phase == QuestPhase.GoingToPickup;
        }

        public void OnNpcInteract(NpcInteractable npc)
        {
            if (_phase == QuestPhase.HasMedicine)
            {
                CompleteQuest();
                return;
            }

            if (_phase != QuestPhase.Inactive)
                return;

            _phase = QuestPhase.AwaitingDialogueAccept;
            dialoguePanel.BeginSequence(config, OnQuestAccepted, OnQuestDeclined);
        }

        void OnQuestAccepted()
        {
            if (_phase != QuestPhase.AwaitingDialogueAccept)
                return;
            _phase = QuestPhase.GoingToPickup;
            if (questPanelHud != null && config != null)
                questPanelHud.SetQuestText(config.activeQuestHudText);
            if (pickupPromptRoot != null)
                pickupPromptRoot.SetActive(true);
            if (pickupColliderRoot != null)
                pickupColliderRoot.SetActive(true);
        }

        void OnQuestDeclined()
        {
            _phase = QuestPhase.Inactive;
        }

        public void OnMedicinePickedUp()
        {
            if (_phase != QuestPhase.GoingToPickup)
                return;
            _phase = QuestPhase.HasMedicine;
            if (pickupPromptRoot != null)
                pickupPromptRoot.SetActive(false);
            if (pickupColliderRoot != null)
                pickupColliderRoot.SetActive(false);
            if (questPanelHud != null && config != null)
                questPanelHud.SetQuestText(config.returnQuestHudText);
        }

        void SyncNpcMoodToQuest()
        {
            if (questNpc == null)
                return;
            if (_phase == QuestPhase.Completed)
                questNpc.SetMoodHappy();
            else
                questNpc.ApplyUnwellMood();
        }

        void CompleteQuest()
        {
            if (_phase != QuestPhase.HasMedicine)
                return;
            _phase = QuestPhase.Completed;

            questNpc?.SetMoodHappy();

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.playerPrefsCompletedKey))
                {
                    PlayerPrefs.SetInt(config.playerPrefsCompletedKey, 1);
                    PlayerPrefs.Save();
                }

                if (achievementPanel != null && config.rewardAchievementCardSprite != null)
                    achievementPanel.AddAchievementCard(config.rewardAchievementCardSprite, config.rewardAchievementTitle);

                if (inventoryRuntime == null)
                    inventoryRuntime = FindObjectOfType<InventoryRuntime>();
                if (inventoryRuntime != null)
                {
                    inventoryRuntime.AddClue(config.rewardClueId, config.rewardClueTitle, config.rewardClueIcon);
                    if (inventoryPanel != null)
                        inventoryPanel.RefreshFromRuntime();
                }
                else
                    Debug.LogWarning($"{nameof(HospitalFetchMedicineEvent)}: 未找到 InventoryRuntime，线索未入库。");
            }
        }
    }
}
