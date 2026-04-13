using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Events
{
    /// <summary>
    /// 超市场景 · 校园卡丢失：是否帮助 → Q 直接搜索 / E 扫描（专业度门槛由 <see cref="SupermarketScanModeController"/> 判定）；
    /// 与 <see cref="HospitalFetchMedicineEvent"/> + <see cref="SupermarketCampusCardFlowBinder"/> 配合。
    /// </summary>
    [AddComponentMenu("Gameplay/Events/Supermarket Campus Card NPC Controller")]
    public class SupermarketCampusCardNpcController : MonoBehaviour, IWorldInteractable, IWorldInteractableResolvePriority,
        IInteractionRewindTarget, IQuestNpcMood
    {
        [Header("对话 UI")]
        public GameplayDialoguePanel dialoguePanel;

        [Tooltip("空则 FindObjectOfType")]
        public PlayerStatsHud statsHud;

        [Header("超市任务")]
        [Tooltip("超市校园卡任务根；空则运行时查找")]
        public HospitalFetchMedicineEvent hospitalMedicineQuest;

        [Tooltip("Q/E 拾取路径分流；空则运行时查找")]
        public SupermarketCampusCardFlowBinder campusCardFlowBinder;

        [Tooltip("空则运行时查找")]
        public SupermarketScanModeController scanModeController;

        [Header("肖像")]
        public Sprite npcPortrait;
        public Sprite dronePortrait;

        [Header("调试 / 测试")]
        [Tooltip("勾选后：不写进度存档、回退时不删 PlayerPrefs；奖励卡不写去重键。")]
        public bool skipPersistentProgress;

        [Header("存档")]
        [Tooltip("完成整条线后的进度键；留空则 Quest_SupermarketCampusCard_NPCComplete")]
        public string progressPlayerPrefsKey = "Quest_SupermarketCampusCard_NPCComplete";

        [Header("奖励")]
        [Tooltip("空则从 GameplayHudLayout 或场景中查找")]
        public RewardCardOfferService rewardCardOfferService;

        [Tooltip("与入库去重一致")]
        public string campusCardRewardStorageKey = "SupermarketCampusCard_RewardCardStored";

        public Sprite campusCardRewardCardSprite;
        public string campusCardRewardCardTitle = "校园卡归还";

        [Header("台词")]
        [TextArea(1, 4)]
        public string lineNpcAsksHelp = "我在超市里把校园卡弄丢了，你能帮我找回来吗？";

        [TextArea(1, 4)]
        public string lineDroneRefuses = "对不起，我现在不方便帮你。";

        [TextArea(1, 4)]
        public string lineDroneAcceptsHelp = "好吧，我来试试看。";

        [TextArea(1, 3)]
        public string promptAcceptOrDecline = "按 空格 接受帮助，按 Esc 拒绝";

        [TextArea(1, 4)]
        public string promptBranchChoices = "按 Q 直接搜索，按 E 开启扫描模式";

        [TextArea(1, 4)]
        public string lineNpcThanksForCard = "太谢谢你了，卡找到了！";

        [TextArea(1, 4)]
        public string lineDroneScanProTooLow = "我的专业度还不够，暂时无法开启扫描模式。";

        [TextArea(1, 2)]
        public string promptAfterScanProFail = "按 空格 返回";

        [Header("专业度不足（选 E 时）")]
        public AudioClip scanProFailedSfx;

        [Range(0f, 1f)]
        public float scanProFailedSfxVolume = 0.85f;

        [Header("打字机")]
        public AudioClip typewriterTickClip;
        public AudioClip typewriterTickClipDrone;

        public float npcCharsPerSecond = 24f;
        public float droneCharsPerSecond = 34f;
        public float npcMinTypewriterTickInterval = 0.03f;
        public float droneMinTypewriterTickInterval = 0.028f;

        [Range(0f, 1f)]
        public float typewriterSfxVolume = 0.35f;

        [Header("对话结算音乐")]
        public AudioClip rewardVictoryMusicClip;
        public AudioClip dialogueEndedWithoutRewardMusicClip;

        [Range(0f, 1f)]
        public float dialogueOutcomeMusicVolume = 0.85f;

        [Header("动画 / 提示")]
        public Animator animator;
        public bool useFeelingBoolParameter = true;
        public string feelingGoodBoolParameter = "IsFeelingGood";
        public bool useAnimatorPlayStates;
        public string unwellStateName = "Unwell";
        public string happyStateName = "Happy";
        public int animatorLayer;

        [Header("世界互动 E")]
        [Min(0.1f)]
        [Tooltip("无 Collider2D 时自动添加圆形触发器使用的半径；过小易导致无人机进不了互动范围")]
        [SerializeField] float npcInteractTriggerRadius = 1.6f;

        [Header("头顶提示")]
        public GameObject promptRoot;
        public Transform promptBobbingTarget;
        public bool enablePromptBobbing = true;
        public BranchingNpcPromptBobAxis promptBobAxis = BranchingNpcPromptBobAxis.LocalY;

        [Min(0f)]
        public float promptBobAmplitude = 0.35f;

        [Min(0.01f)]
        public float promptBobFrequency = 2f;

        public Transform promptAnchor;
        public bool proximityPromptOnlyWhenNear;

        [Header("回退")]
        public KeyCode resetToPreInteractKey = KeyCode.None;

        enum Flow
        {
            Idle,
            NpcAskTyping,
            FirstChoice,
            AcceptDroneTyping,
            RejectDroneTyping,
            RejectWaitConfirm,
            BranchWaitKey,
            BranchAfterScanProFailTyping,
            BranchAfterENpcTyping,
            BranchAfterEWaitSpace
        }

        Flow _flow = Flow.Idle;
        int _visibleChars;
        float _typewriterCharsCarry;
        float _lastTick;
        bool _lineDone;
        bool _campusCardComplete;
        bool _rewardCardOfferedThisSession;

        bool _proximityHighlight;
        Vector3 _promptBobBaseLocal;
        bool _promptBobBaseReady;

        bool _awaitingMedicineFromHospital;
        bool _pendingHospitalQuestCompleteAfterEDialogue;

        bool _warnedMissingDialoguePanel;

        AudioClip DroneTickClip => typewriterTickClipDrone != null ? typewriterTickClipDrone : typewriterTickClip;

        public int WorldInteractResolvePriority => 100;

        string EffectiveProgressPrefsKey =>
            string.IsNullOrWhiteSpace(progressPlayerPrefsKey)
                ? "Quest_SupermarketCampusCard_NPCComplete"
                : progressPlayerPrefsKey.Trim();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                return;
            EnsureCollider2DTriggerEditor();
        }

        void EnsureCollider2DTriggerEditor()
        {
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                var circle = Undo.AddComponent<CircleCollider2D>(gameObject);
                Undo.RecordObject(circle, "SupermarketCampusCard Collider");
                circle.isTrigger = true;
                circle.radius = npcInteractTriggerRadius;
                col = circle;
            }
            else if (col != null && !col.isTrigger)
            {
                Undo.RecordObject(col, "SupermarketCampusCard Collider isTrigger");
                col.isTrigger = true;
            }
        }
#endif

        void EnsureCollider2DTriggerRuntime()
        {
            var c = GetComponent<Collider2D>();
            if (c == null)
            {
                var circle = gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = npcInteractTriggerRadius;
                return;
            }
            c.isTrigger = true;
        }

        void Awake()
        {
            ResolveAnimator();
            ResolveStatsHud();
            ResolveHospitalMedicineQuestIfNeeded();
            ResolveCampusCardBinderIfNeeded();
            ResolveScanControllerIfNeeded();

            EnsureCollider2DTriggerRuntime();

            _campusCardComplete = !skipPersistentProgress && PlayerPrefs.GetInt(EffectiveProgressPrefsKey, 0) != 0;

            if (!_campusCardComplete)
                ApplyUnwellMood();
            else
                SetMoodHappy();

            BranchingQuestNpcPromptUtil.DisableBobbingWorldSpritesUnder(promptRoot);
            ApplyPromptRootVisibility();
        }

        void Start()
        {
            ResolveDialoguePanelIfNeeded();
        }

        void ResolveDialoguePanelIfNeeded()
        {
            if (dialoguePanel != null)
                return;
            dialoguePanel = FindObjectOfType<GameplayDialoguePanel>(true);
            if (dialoguePanel == null && !_warnedMissingDialoguePanel)
            {
                _warnedMissingDialoguePanel = true;
                Debug.LogWarning(
                    $"{nameof(SupermarketCampusCardNpcController)} 「{name}」: 未找到 GameplayDialoguePanel，靠近按 E 无法开始对话。请在 Inspector 指定或确保场景中存在对白面板。",
                    this);
            }
        }

        void ResolveHospitalMedicineQuestIfNeeded()
        {
            if (hospitalMedicineQuest != null)
                return;

            var all = FindObjectsOfType<HospitalFetchMedicineEvent>(true);
            if (all == null || all.Length == 0)
                return;

            if (all.Length == 1)
            {
                hospitalMedicineQuest = all[0];
                return;
            }

            var self = (MonoBehaviour)this;
            foreach (var h in all)
            {
                if (h != null && h.questNpc == self)
                {
                    hospitalMedicineQuest = h;
                    return;
                }
            }

            hospitalMedicineQuest = all[0];
        }

        void ResolveCampusCardBinderIfNeeded()
        {
            if (campusCardFlowBinder != null)
                return;
            campusCardFlowBinder = FindObjectOfType<SupermarketCampusCardFlowBinder>(true);
        }

        void ResolveScanControllerIfNeeded()
        {
            if (scanModeController != null)
                return;
            scanModeController = FindObjectOfType<SupermarketScanModeController>(true);
        }

        void ResolveRewardOfferService()
        {
            if (rewardCardOfferService != null)
                return;

            var layout = FindObjectOfType<GameplayHudLayout>();
            if (layout != null && layout.rewardCardOfferService != null)
                rewardCardOfferService = layout.rewardCardOfferService;
            else
                rewardCardOfferService = FindObjectOfType<RewardCardOfferService>(true);
        }

        void ResolveStatsHud()
        {
            if (statsHud != null)
                return;

            var layout = FindObjectOfType<GameplayHudLayout>();
            if (layout != null && layout.playerStatsHud != null)
                statsHud = layout.playerStatsHud;

            if (statsHud == null)
                statsHud = FindObjectOfType<PlayerStatsHud>();
        }

        public bool CanInteract(Transform interactor)
        {
            if (dialoguePanel == null)
                ResolveDialoguePanelIfNeeded();
            if (dialoguePanel == null)
                return false;

            if (_campusCardComplete)
                return false;
            if (dialoguePanel.IsOpen)
                return false;

            if (_awaitingMedicineFromHospital)
            {
                if (hospitalMedicineQuest == null)
                {
                    _awaitingMedicineFromHospital = false;
                    return false;
                }

                if (hospitalMedicineQuest.Phase != HospitalFetchMedicineEvent.QuestPhase.HasMedicine)
                    return false;
                if (_flow != Flow.Idle)
                    return false;
                return true;
            }

            if (_flow != Flow.Idle)
                return false;
            return true;
        }

        public void BeginInteract(Transform interactor)
        {
            ResolveDialoguePanelIfNeeded();
            if (dialoguePanel == null)
                return;

            if (_awaitingMedicineFromHospital && hospitalMedicineQuest != null &&
                hospitalMedicineQuest.Phase == HospitalFetchMedicineEvent.QuestPhase.HasMedicine)
            {
                BeginMedicineReturnThanksFlow();
                return;
            }

            if (!CanInteract(interactor))
                return;

            ResolveHospitalMedicineQuestIfNeeded();
            ResolveCampusCardBinderIfNeeded();
            ResolveScanControllerIfNeeded();

            dialoguePanel.ExternalSessionBegin(this);
            _flow = Flow.NpcAskTyping;
            _visibleChars = 0;
            _typewriterCharsCarry = 0f;
            _lastTick = 0f;
            _lineDone = false;
            _rewardCardOfferedThisSession = false;

            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            dialoguePanel.ExternalClearBody();
            dialoguePanel.ExternalEnsureBodyVisible();
            dialoguePanel.ExternalSetChoicePrompt(false, null);
            ApplyPromptRootVisibility();
        }

        void Update()
        {
            if (resetToPreInteractKey != KeyCode.None && Input.GetKeyDown(resetToPreInteractKey))
            {
                ResetToPreInteractState();
                return;
            }

            if (_flow == Flow.Idle || dialoguePanel == null)
                return;

            switch (_flow)
            {
                case Flow.NpcAskTyping:
                    UpdateNpcAskTyping();
                    break;
                case Flow.FirstChoice:
                    UpdateFirstChoice();
                    break;
                case Flow.AcceptDroneTyping:
                    UpdateAcceptDroneTyping();
                    break;
                case Flow.RejectDroneTyping:
                    UpdateRejectDroneTyping();
                    break;
                case Flow.RejectWaitConfirm:
                    UpdateRejectWaitConfirm();
                    break;
                case Flow.BranchWaitKey:
                    UpdateBranchWaitKey();
                    break;
                case Flow.BranchAfterScanProFailTyping:
                    UpdateBranchAfterScanProFailTyping();
                    break;
                case Flow.BranchAfterENpcTyping:
                    UpdateBranchAfterENpcTyping();
                    break;
                case Flow.BranchAfterEWaitSpace:
                    UpdateBranchAfterEWaitSpace();
                    break;
            }
        }

        void UpdateNpcAskTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcAsksHelp ?? string.Empty;

            if (Input.GetKeyDown(KeyCode.Space) && !_lineDone)
            {
                TypewriterTMP.RevealFullText(dialoguePanel.bodyText, full);
                _visibleChars = full.Length;
                _typewriterCharsCarry = 0f;
                _lineDone = true;
            }
            else if (!_lineDone)
            {
                _lineDone = dialoguePanel.ExternalTypewriterStep(
                    full,
                    ref _visibleChars,
                    ref _typewriterCharsCarry,
                    ref _lastTick,
                    npcCharsPerSecond,
                    npcMinTypewriterTickInterval,
                    typewriterTickClip,
                    typewriterSfxVolume);
            }

            if (_lineDone)
            {
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                dialoguePanel.ExternalSetChoicePrompt(true, promptAcceptOrDecline);
                _flow = Flow.FirstChoice;
            }
        }

        void UpdateFirstChoice()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.RejectDroneTyping;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.AcceptDroneTyping;
            }
        }

        void UpdateAcceptDroneTyping()
        {
            var full = lineDroneAcceptsHelp ?? string.Empty;

            if (Input.GetKeyDown(KeyCode.Space) && !_lineDone)
            {
                TypewriterTMP.RevealFullText(dialoguePanel.bodyText, full);
                _visibleChars = full.Length;
                _typewriterCharsCarry = 0f;
                _lineDone = true;
            }
            else if (!_lineDone)
            {
                _lineDone = dialoguePanel.ExternalTypewriterStep(
                    full,
                    ref _visibleChars,
                    ref _typewriterCharsCarry,
                    ref _lastTick,
                    droneCharsPerSecond,
                    droneMinTypewriterTickInterval,
                    DroneTickClip,
                    typewriterSfxVolume);
            }

            if (_lineDone)
            {
                dialoguePanel.ExternalSetChoicePrompt(true, promptBranchChoices);
                _flow = Flow.BranchWaitKey;
            }
        }

        void UpdateRejectDroneTyping()
        {
            var full = lineDroneRefuses ?? string.Empty;

            if (Input.GetKeyDown(KeyCode.Space) && !_lineDone)
            {
                TypewriterTMP.RevealFullText(dialoguePanel.bodyText, full);
                _visibleChars = full.Length;
                _typewriterCharsCarry = 0f;
                _lineDone = true;
            }
            else if (!_lineDone)
            {
                _lineDone = dialoguePanel.ExternalTypewriterStep(
                    full,
                    ref _visibleChars,
                    ref _typewriterCharsCarry,
                    ref _lastTick,
                    droneCharsPerSecond,
                    droneMinTypewriterTickInterval,
                    DroneTickClip,
                    typewriterSfxVolume);
            }

            if (_lineDone)
                _flow = Flow.RejectWaitConfirm;
        }

        void UpdateRejectWaitConfirm()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ResolveStatsHud();
                if (statsHud != null)
                    statsHud.AddEmotion(-1f);

                EndDialogue();
            }
        }

        void UpdateBranchWaitKey()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                StartDirectSearchPath();
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                StartScanPathOrFail();
            }
        }

        void StartDirectSearchPath()
        {
            ResolveHospitalMedicineQuestIfNeeded();
            ResolveCampusCardBinderIfNeeded();

            if (hospitalMedicineQuest == null || campusCardFlowBinder == null)
            {
                Debug.LogWarning(
                    $"{nameof(SupermarketCampusCardNpcController)} 「{name}」: 缺少 HospitalFetchMedicineEvent 或 SupermarketCampusCardFlowBinder，无法开始 Q 分支。",
                    this);
                EndDialogue();
                return;
            }

            hospitalMedicineQuest.ResetPhaseForCompanionBranchIfStale(_campusCardComplete);
            hospitalMedicineQuest.EnterGoingToPickupFromBranchingDialogue();
            if (hospitalMedicineQuest.Phase != HospitalFetchMedicineEvent.QuestPhase.GoingToPickup)
            {
                EndDialogue();
                return;
            }

            if (statsHud != null)
                statsHud.AddProfessionalism(1f);

            campusCardFlowBinder.SetPickupPath(SupermarketCampusCardFlowBinder.CampusCardPickupPath.DirectSearch);
            _awaitingMedicineFromHospital = true;
            ApplyUnwellMood();
            ApplyPromptRootVisibility();
            EndDialogue();
        }

        void StartScanPathOrFail()
        {
            ResolveHospitalMedicineQuestIfNeeded();
            ResolveCampusCardBinderIfNeeded();
            ResolveScanControllerIfNeeded();

            if (hospitalMedicineQuest == null || campusCardFlowBinder == null)
            {
                Debug.LogWarning(
                    $"{nameof(SupermarketCampusCardNpcController)} 「{name}」: 缺少 HospitalFetchMedicineEvent 或 SupermarketCampusCardFlowBinder，无法开始 E 分支。",
                    this);
                EndDialogue();
                return;
            }

            hospitalMedicineQuest.ResetPhaseForCompanionBranchIfStale(_campusCardComplete);

            if (scanModeController == null || !scanModeController.TryEnableScanModeFromQuest(out _))
            {
                if (scanProFailedSfx != null)
                    NpcEventOutcomeAudio.PlayClip2D(scanProFailedSfx, scanProFailedSfxVolume);

                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.BranchAfterScanProFailTyping;
                return;
            }

            hospitalMedicineQuest.EnterGoingToPickupFromBranchingDialogue();
            if (hospitalMedicineQuest.Phase != HospitalFetchMedicineEvent.QuestPhase.GoingToPickup)
            {
                EndDialogue();
                return;
            }

            if (statsHud != null)
                statsHud.AddProfessionalism(1f);

            campusCardFlowBinder.SetPickupPath(SupermarketCampusCardFlowBinder.CampusCardPickupPath.ScanMode);
            _awaitingMedicineFromHospital = true;
            ApplyUnwellMood();
            ApplyPromptRootVisibility();
            EndDialogue();
        }

        void UpdateBranchAfterScanProFailTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            var full = lineDroneScanProTooLow ?? string.Empty;

            if (!_lineDone)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    TypewriterTMP.RevealFullText(dialoguePanel.bodyText, full);
                    _visibleChars = full.Length;
                    _typewriterCharsCarry = 0f;
                    _lineDone = true;
                }
                else
                {
                    _lineDone = dialoguePanel.ExternalTypewriterStep(
                        full,
                        ref _visibleChars,
                        ref _typewriterCharsCarry,
                        ref _lastTick,
                        droneCharsPerSecond,
                        droneMinTypewriterTickInterval,
                        DroneTickClip,
                        typewriterSfxVolume);
                }

                if (_lineDone)
                    dialoguePanel.ExternalSetChoicePrompt(true, promptAfterScanProFail);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalSetChoicePrompt(true, promptBranchChoices);
                _flow = Flow.BranchWaitKey;
            }
        }

        void BeginMedicineReturnThanksFlow()
        {
            _awaitingMedicineFromHospital = false;
            _pendingHospitalQuestCompleteAfterEDialogue = true;

            _rewardCardOfferedThisSession = false;
            dialoguePanel.ExternalSessionBegin(this);
            _flow = Flow.BranchAfterENpcTyping;
            _visibleChars = 0;
            _typewriterCharsCarry = 0f;
            _lastTick = 0f;
            _lineDone = false;

            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            dialoguePanel.ExternalClearBody();
            dialoguePanel.ExternalEnsureBodyVisible();
            dialoguePanel.ExternalSetChoicePrompt(false, null);
            ApplyPromptRootVisibility();
        }

        void UpdateBranchAfterENpcTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcThanksForCard ?? string.Empty;

            if (Input.GetKeyDown(KeyCode.Space) && !_lineDone)
            {
                TypewriterTMP.RevealFullText(dialoguePanel.bodyText, full);
                _visibleChars = full.Length;
                _typewriterCharsCarry = 0f;
                _lineDone = true;
            }
            else if (!_lineDone)
            {
                _lineDone = dialoguePanel.ExternalTypewriterStep(
                    full,
                    ref _visibleChars,
                    ref _typewriterCharsCarry,
                    ref _lastTick,
                    npcCharsPerSecond,
                    npcMinTypewriterTickInterval,
                    typewriterTickClip,
                    typewriterSfxVolume);
            }

            if (_lineDone)
                _flow = Flow.BranchAfterEWaitSpace;
        }

        void UpdateBranchAfterEWaitSpace()
        {
            if (!Input.GetKeyDown(KeyCode.Space))
                return;

            if (_pendingHospitalQuestCompleteAfterEDialogue)
            {
                _pendingHospitalQuestCompleteAfterEDialogue = false;
                if (hospitalMedicineQuest != null)
                    hospitalMedicineQuest.CompleteQuestIfHasMedicine();
                _campusCardComplete = true;
                if (!skipPersistentProgress)
                {
                    PlayerPrefs.SetInt(EffectiveProgressPrefsKey, 1);
                    PlayerPrefs.Save();
                }
                SetMoodHappy();
                ApplyPromptRootVisibility();
            }

            ResolveRewardOfferService();
            if (rewardCardOfferService != null && campusCardRewardCardSprite != null &&
                !string.IsNullOrEmpty(campusCardRewardStorageKey))
            {
                _rewardCardOfferedThisSession = true;
                NpcEventOutcomeAudio.PlayClip2D(rewardVictoryMusicClip, dialogueOutcomeMusicVolume);
                EndDialogue();
                rewardCardOfferService.Offer(
                    campusCardRewardCardSprite,
                    campusCardRewardCardTitle,
                    campusCardRewardStorageKey,
                    persistPlayerPrefs: !skipPersistentProgress);
            }
            else
                EndDialogue();
        }

        void EndDialogue()
        {
            if (!_rewardCardOfferedThisSession)
                NpcEventOutcomeAudio.PlayClip2D(dialogueEndedWithoutRewardMusicClip, dialogueOutcomeMusicVolume);
            if (dialoguePanel != null)
                dialoguePanel.ExternalSessionEnd();
            _flow = Flow.Idle;
            ApplyPromptRootVisibility();
        }

        [ContextMenu("回退到可互动状态")]
        public void ResetToPreInteractState()
        {
            if (dialoguePanel != null && dialoguePanel.IsOpen)
                dialoguePanel.ForceCloseWithoutCallback();

            _flow = Flow.Idle;
            _visibleChars = 0;
            _typewriterCharsCarry = 0f;
            _lastTick = 0f;
            _lineDone = false;

            if (!skipPersistentProgress)
            {
                PlayerPrefs.DeleteKey(EffectiveProgressPrefsKey);
                if (!string.IsNullOrEmpty(campusCardRewardStorageKey))
                    PlayerPrefs.DeleteKey(campusCardRewardStorageKey);
                PlayerPrefs.Save();
            }
            _campusCardComplete = false;
            _awaitingMedicineFromHospital = false;
            _pendingHospitalQuestCompleteAfterEDialogue = false;

            if (campusCardFlowBinder != null)
                campusCardFlowBinder.ResetToInactive();

            if (hospitalMedicineQuest != null &&
                hospitalMedicineQuest.Phase != HospitalFetchMedicineEvent.QuestPhase.Completed)
                hospitalMedicineQuest.CancelCompanionBranchFlowToInactive();

            ApplyUnwellMood();
            ApplyPromptRootVisibility();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !enablePromptBobbing || promptBobAmplitude <= 0f)
                return;
            var t = BranchingQuestNpcPromptUtil.ResolveBobbingTransform(promptRoot, promptBobbingTarget);
            BranchingQuestNpcPromptUtil.TickBobbing(
                t,
                ref _promptBobBaseLocal,
                ref _promptBobBaseReady,
                promptBobAmplitude,
                promptBobFrequency,
                true,
                promptBobAxis);
        }

        void ApplyPromptRootVisibility()
        {
            if (promptRoot == null)
                return;
            var wasActive = promptRoot.activeSelf;
            var show = BranchingQuestNpcPromptUtil.ShouldShowPromptRoot(
                _campusCardComplete,
                proximityPromptOnlyWhenNear,
                _proximityHighlight,
                () => CanInteract(null));
            promptRoot.SetActive(show);
            if (show && !wasActive)
                _promptBobBaseReady = false;
        }

        public Transform GetPromptAnchor()
        {
            return promptAnchor != null ? promptAnchor : transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
            _proximityHighlight = highlighted;
            ApplyPromptRootVisibility();
        }

        public void ApplyUnwellMood()
        {
            ResolveAnimator();
            if (animator == null)
                return;

            if (useAnimatorPlayStates && !string.IsNullOrEmpty(unwellStateName))
                animator.Play(unwellStateName, animatorLayer, 0f);
            if (useFeelingBoolParameter && !string.IsNullOrEmpty(feelingGoodBoolParameter))
                animator.SetBool(feelingGoodBoolParameter, false);
        }

        public void SetMoodHappy()
        {
            ResolveAnimator();
            if (animator == null)
                return;

            if (useAnimatorPlayStates && !string.IsNullOrEmpty(happyStateName))
                animator.Play(happyStateName, animatorLayer, 0f);
            if (useFeelingBoolParameter && !string.IsNullOrEmpty(feelingGoodBoolParameter))
                animator.SetBool(feelingGoodBoolParameter, true);
        }

        void ResolveAnimator()
        {
            if (animator != null)
                return;
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
    }
}
