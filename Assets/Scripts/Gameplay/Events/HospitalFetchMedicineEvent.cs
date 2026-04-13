using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.Events
{
    /// <summary>
    /// 医院取药：对话（可 Space/E 接受）→ 取药点按 E 取药 → 持药回 NPC → 可选感谢对白 → 奖励。
    /// 同时实现 <see cref="IWorldInteractable"/>：取药点只需本脚本 + Collider2D（可在本物体或子物体上），勿再挂单独的取药互动物体脚本。
    /// </summary>
    [AddComponentMenu("Gameplay/Events/Hospital Fetch Medicine (Quest + Pickup)")]
    public class HospitalFetchMedicineEvent : MonoBehaviour, IQuestWorldEvent, IWorldInteractable,
        IWorldInteractableResolvePriority
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

        [Tooltip("可选：选分支「去医院取药」后额外点亮的医院区域（门头灯、指引箭头等）；与取药点提示可分开绑")]
        public GameObject hospitalAreaHighlightRoot;

        [Tooltip(
            "取药点 Collider 所在物体，用于隐藏/显示。留空则自动使用本物体或子物体上第一个 Collider2D 所在物体。多任务时请指定，或保证层级与 OwnsPickup 规则一致。")]
        public GameObject pickupColliderRoot;

        [Tooltip("取药点处静态图片/Sprite 根（与 proximity 提示可分开）；取药成功后隐藏")]
        public GameObject pickupPointVisualRoot;

        [Tooltip("绑定的同学 NPC；开局不舒服动画，送药完成后切愉快动画（须挂 QuestNpcInteractable 或兼容子类）")]
        public MonoBehaviour questNpc;

        [Header("取药点世界交互（与 IsoDrone E）")]
        [Tooltip("靠近取药点时显示（可与浮动提示共用父物体）；仅 GoingToPickup 且无人机高亮时显示")]
        public GameObject proximityPromptRoot;

        [Header("碰撞")]
        [Min(0.1f)]
        [Tooltip("若为 CircleCollider2D，运行时至少使用该半径，避免过小导致无人机进不了范围")]
        [SerializeField] float minColliderRadius = 1.2f;

        [Header("输入回退")]
        [Tooltip("勾选后：即使无人机侧 Trigger 未登记重叠，只要药点 Collider 包含无人机位置且按 E，也会取药")]
        [SerializeField] bool allowDirectKeyPickupWhenOverlapping = true;

        [Header("音效")]
        [Tooltip("在医院取药点按 E 成功取药时播放（2D 一次性）")]
        public AudioClip pickupInteractSfx;

        [Range(0f, 1f)]
        public float pickupInteractSfxVolume = 1f;

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

        Collider2D _col2d;

        public int WorldInteractResolvePriority =>
            CanPickupMedicine() ? 200 : 0;

        /// <summary>
        /// 判断取药互动物体是否属于本任务的取药区域（与 <see cref="pickupColliderRoot"/> 同物体或为其子物体）。
        /// </summary>
        public bool OwnsPickupTransform(Transform pickupTransform)
        {
            if (pickupTransform == null)
                return false;
            if (pickupColliderRoot != null)
            {
                var root = pickupColliderRoot.transform;
                return pickupTransform == root || pickupTransform.IsChildOf(root);
            }

            return pickupTransform == transform || pickupTransform.IsChildOf(transform);
        }

        /// <summary>
        /// 为取药点解析应绑定的任务：优先匹配 <see cref="pickupColliderRoot"/> 层级；多份时优先 <see cref="QuestPhase.GoingToPickup"/>；
        /// 仅当全场景唯一一份事件时才回退到该实例。Inspector 已指定时不调用本方法。
        /// </summary>
        public static HospitalFetchMedicineEvent FindBestForPickupTransform(Transform pickupTransform)
        {
            if (pickupTransform == null)
                return null;

            var all = FindObjectsOfType<HospitalFetchMedicineEvent>(true);
            if (all == null || all.Length == 0)
                return null;

            HospitalFetchMedicineEvent match = null;
            foreach (var e in all)
            {
                if (e == null)
                    continue;
                if (!e.OwnsPickupTransform(pickupTransform))
                    continue;
                if (match == null)
                {
                    match = e;
                    continue;
                }

                if (e.Phase == QuestPhase.GoingToPickup && match.Phase != QuestPhase.GoingToPickup)
                    match = e;
            }

            if (match != null)
                return match;

            return all.Length == 1 ? all[0] : null;
        }

        void Awake()
        {
            ResolveHudReferencesIfNeeded();
            if (inventoryRuntime == null)
                inventoryRuntime = FindObjectOfType<InventoryRuntime>();

            EnsureColliderRef();
            if (pickupColliderRoot == null && _col2d != null)
                pickupColliderRoot = _col2d.gameObject;

            ApplyColliderDefaults();

            if (pickupPromptRoot != null)
                pickupPromptRoot.SetActive(false);
            if (proximityPromptRoot != null)
                proximityPromptRoot.SetActive(false);
            if (hospitalAreaHighlightRoot != null)
                hospitalAreaHighlightRoot.SetActive(false);
            if (pickupColliderRoot != null)
                pickupColliderRoot.SetActive(false);
            if (pickupPointVisualRoot != null)
                pickupPointVisualRoot.SetActive(false);

            if (config != null && !string.IsNullOrEmpty(config.playerPrefsCompletedKey) &&
                PlayerPrefs.GetInt(config.playerPrefsCompletedKey, 0) == 1)
                _phase = QuestPhase.Completed;

            SyncNpcMoodToQuest();
        }

        void EnsureColliderRef()
        {
            if (_col2d != null)
                return;
            _col2d = GetComponent<Collider2D>();
            if (_col2d == null)
                _col2d = GetComponentInChildren<Collider2D>(true);
        }

        void ApplyColliderDefaults()
        {
            if (_col2d == null)
                return;
            _col2d.isTrigger = true;
            if (_col2d is CircleCollider2D circle && circle.radius < minColliderRadius)
                circle.radius = minColliderRadius;
        }

        void OnEnable()
        {
            Physics2D.SyncTransforms();
        }

        void Update()
        {
            if (!allowDirectKeyPickupWhenOverlapping)
                return;
            if (GameplayModalBlocker.IsBlockingInput)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            if (!Input.GetKeyDown(KeyCode.E))
                return;
            if (!CanPickupMedicine())
                return;

            EnsureColliderRef();
            if (_col2d == null)
                return;

            var drone = FindObjectOfType<IsoDroneController>();
            if (drone == null)
                return;
            var p = (Vector2)drone.transform.position;
            if (!_col2d.OverlapPoint(p))
                return;

            BeginInteract(drone.transform);
        }

        void ResolveHudReferencesIfNeeded()
        {
            if (dialoguePanel == null)
                dialoguePanel = FindObjectOfType<GameplayDialoguePanel>(true);

            var hud = FindObjectOfType<GameplayHudLayout>();
            if (hud != null)
            {
                if (questPanelHud == null)
                    questPanelHud = hud.questPanelHud;
                if (achievementPanel == null)
                    achievementPanel = hud.achievementPanelController;
                if (inventoryPanel == null)
                    inventoryPanel = hud.inventoryPanelController;
            }

            if (inventoryRuntime == null)
                inventoryRuntime = FindObjectOfType<InventoryRuntime>();
        }

        /// <summary>供 <see cref="IQuestWorldEvent"/> 与任务 NPC（传 <see cref="MonoBehaviour"/>）调用。</summary>
        public bool CanInteractNpc(MonoBehaviour npc) =>
            npc != null && npc == questNpc && CanInteractNpcCore();

        /// <summary>供 <see cref="IQuestWorldEvent"/> 与任务 NPC（传 <see cref="MonoBehaviour"/>）调用。</summary>
        public void OnNpcInteract(MonoBehaviour npc)
        {
            if (npc != questNpc)
                return;
            OnNpcInteractCore();
        }

        bool CanInteractNpcCore()
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
            {
                if (dialoguePanel.IsOpen)
                    return false;
                return true;
            }
            return _phase == QuestPhase.Inactive;
        }

        public bool CanPickupMedicine()
        {
            return _phase == QuestPhase.GoingToPickup;
        }

        void OnNpcInteractCore()
        {
            if (_phase == QuestPhase.HasMedicine)
            {
                TryBeginReturnDialogueThenComplete();
                return;
            }

            if (_phase != QuestPhase.Inactive)
                return;

            _phase = QuestPhase.AwaitingDialogueAccept;
            dialoguePanel.BeginSequence(config, OnQuestAccepted, OnQuestDeclined);
        }

        /// <summary>
        /// 持药返回：若有配置 <see cref="HospitalFetchMedicineEventConfig.returnDialogueLines"/> 则先播感谢对白，结束后再结算；否则与旧版一致直接完成。
        /// </summary>
        void TryBeginReturnDialogueThenComplete()
        {
            if (config == null || dialoguePanel == null)
            {
                CompleteQuest();
                return;
            }

            var lines = config.returnDialogueLines;
            if (lines == null || lines.Length == 0)
            {
                CompleteQuest();
                return;
            }

            var tempCfg = ScriptableObject.CreateInstance<HospitalFetchMedicineEventConfig>();
            tempCfg.dialogueLines = lines;
            tempCfg.defaultNpcPortrait = config.defaultNpcPortrait;
            tempCfg.defaultPlayerPortrait = config.defaultPlayerPortrait;
            tempCfg.charsPerSecond = config.charsPerSecond;
            tempCfg.minTypewriterTickInterval = config.minTypewriterTickInterval;
            tempCfg.typewriterTickClip = config.typewriterTickClip;
            tempCfg.typewriterSfxVolume = config.typewriterSfxVolume;

            if (!dialoguePanel.BeginNarrativeSequence(tempCfg, () =>
                {
                    Destroy(tempCfg);
                    CompleteQuest();
                }))
            {
                Destroy(tempCfg);
                CompleteQuest();
            }
        }

        void OnQuestAccepted()
        {
            if (_phase != QuestPhase.AwaitingDialogueAccept)
                return;
            _phase = QuestPhase.GoingToPickup;
            ApplyGoingToPickupPresentation();
        }

        void ApplyGoingToPickupPresentation()
        {
            if (questPanelHud != null && config != null)
            {
                var hud = !string.IsNullOrEmpty(config.searchingHospitalHudText)
                    ? config.searchingHospitalHudText
                    : config.activeQuestHudText;
                questPanelHud.SetQuestText(hud);
            }
            if (pickupPromptRoot != null)
                pickupPromptRoot.SetActive(true);
            if (hospitalAreaHighlightRoot != null)
                hospitalAreaHighlightRoot.SetActive(true);
            if (pickupColliderRoot != null)
                pickupColliderRoot.SetActive(true);
            if (pickupPointVisualRoot != null)
                pickupPointVisualRoot.SetActive(true);
        }

        /// <summary>
        /// 同伴 NPC（如 SickClassmate）整条线未完成，但本事件阶段已是 <see cref="QuestPhase.Completed"/>（常见于仅全局存档键被写满）时，
        /// 将阶段拉回 <see cref="QuestPhase.Inactive"/>，以便「按 E 去医院」分支对白与 <see cref="EnterGoingToPickupFromBranchingDialogue"/> 能执行。
        /// 不修改 PlayerPrefs；若任务确已完整结算，请勿调用。
        /// </summary>
        public void ResetPhaseForCompanionBranchIfStale(bool companionMedicineLineComplete)
        {
            if (companionMedicineLineComplete)
                return;
            if (_phase != QuestPhase.Completed)
                return;
            _phase = QuestPhase.Inactive;
        }

        /// <summary>
        /// 由分支对白（如 <see cref="SickClassmateNpcController"/> 选「去医院取药」）触发：直接进入取药阶段，不经过任务接受序列。
        /// 若已在 <see cref="QuestPhase.GoingToPickup"/> 则仅刷新取药点显示（可重复从对白确认）。
        /// </summary>
        public void EnterGoingToPickupFromBranchingDialogue()
        {
            if (_phase == QuestPhase.Completed || _phase == QuestPhase.AwaitingDialogueAccept ||
                _phase == QuestPhase.HasMedicine)
                return;

            _phase = QuestPhase.GoingToPickup;
            ApplyGoingToPickupPresentation();
        }

        /// <summary>
        /// 手持药物送回 NPC 时由同学脚本调用，写入任务完成、成就与线索（与 <see cref="OnNpcInteractCore"/> 中完成逻辑一致）。
        /// </summary>
        public void CompleteQuestIfHasMedicine()
        {
            if (_phase != QuestPhase.HasMedicine)
                return;
            CompleteQuest();
        }

        /// <summary>
        /// 回退/重置时：若取药流程由分支 NPC 发起且未真正完成，将阶段收回 Inactive 并隐藏取药引导。
        /// </summary>
        public void CancelCompanionBranchFlowToInactive()
        {
            if (_phase == QuestPhase.Completed)
                return;

            _phase = QuestPhase.Inactive;
            if (pickupPromptRoot != null)
                pickupPromptRoot.SetActive(false);
            if (hospitalAreaHighlightRoot != null)
                hospitalAreaHighlightRoot.SetActive(false);
            if (pickupColliderRoot != null)
                pickupColliderRoot.SetActive(false);
            if (pickupPointVisualRoot != null)
                pickupPointVisualRoot.SetActive(false);
            if (questPanelHud != null)
                questPanelHud.SetQuestText(null);
            SyncNpcMoodToQuest();
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
            if (hospitalAreaHighlightRoot != null)
                hospitalAreaHighlightRoot.SetActive(false);
            if (pickupColliderRoot != null)
                pickupColliderRoot.SetActive(false);
            if (pickupPointVisualRoot != null)
                pickupPointVisualRoot.SetActive(false);
            if (proximityPromptRoot != null)
                proximityPromptRoot.SetActive(false);
            if (questPanelHud != null && config != null)
            {
                var ok = config.pickupSuccessHudText ?? string.Empty;
                var ret = config.returnQuestHudText ?? string.Empty;
                var combined = string.IsNullOrEmpty(ret) ? ok : $"{ok}\n{ret}";
                questPanelHud.SetQuestText(combined);
            }
        }

        void SyncNpcMoodToQuest()
        {
            if (questNpc is not IQuestNpcMood mood)
                return;
            if (_phase == QuestPhase.Completed)
                mood.SetMoodHappy();
            else
                mood.ApplyUnwellMood();
        }

        void CompleteQuest()
        {
            if (_phase != QuestPhase.HasMedicine)
                return;
            _phase = QuestPhase.Completed;

            if (questNpc is IQuestNpcMood mood)
                mood.SetMoodHappy();

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

        public bool CanInteract(Transform interactor)
        {
            return CanPickupMedicine();
        }

        public void BeginInteract(Transform interactor)
        {
            if (!CanPickupMedicine())
                return;
            if (pickupInteractSfx != null)
                NpcEventOutcomeAudio.PlayClip2D(pickupInteractSfx, pickupInteractSfxVolume);
            OnMedicinePickedUp();
        }

        public Transform GetPromptAnchor()
        {
            EnsureColliderRef();
            return _col2d != null ? _col2d.transform : transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
            if (proximityPromptRoot == null)
                return;
            proximityPromptRoot.SetActive(highlighted && CanPickupMedicine());
        }
    }
}
