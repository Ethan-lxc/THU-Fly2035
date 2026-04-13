using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Events
{
    /// <summary>
    /// 身体不适同学 · 分支对白 NPC：靠近按 E 打开分支对话（与同物体其它 IWorldInteractable 二选一，勿同时挂两个）。
    /// 可选绑定 <see cref="hospitalMedicineQuest"/>：选 E「去医院取药」会先播无人机与 NPC 指路对白，再进入 <see cref="HospitalFetchMedicineEvent"/> 取药流程；持药返回后再靠近互动可接续感谢与奖励。
    /// 若任务配置 <see cref="HospitalFetchMedicineEventConfig.returnDialogueLines"/> 非空，送药回来时优先播该多段感谢；否则仍用本脚本的 <see cref="lineNpcThanksForMedicine"/> 单段感谢。
    /// 指路事件请用 <see cref="DirectionGuideNpcController"/>。多名该类 NPC 须配置不同的 <see cref="progressPlayerPrefsKey"/> 与 <see cref="medicinePathRewardStorageKey"/>。
    /// 未挂 Collider2D 时会自动添加 <see cref="CircleCollider2D"/>（Trigger）。
    /// 与 <see cref="IWorldInteractableResolvePriority"/> 配合：同屏多目标时优先于地面拾取物。
    /// </summary>
    public class SickClassmateNpcController : MonoBehaviour, IWorldInteractable, IWorldInteractableResolvePriority,
        IInteractionRewindTarget, IQuestNpcMood
    {
        [Header("对话 UI")]
        public GameplayDialoguePanel dialoguePanel;

        [Header("医院取药（可选）")]
        [Tooltip("填写后：选 E 先播无人机/NPC 两句再进入取药任务。空则运行时自动查找场景中的 HospitalFetchMedicineEvent（多个时优先 questNpc 指向本 NPC 的实例）。仍为空则走旧版「当场谢谢」短支。")]
        public HospitalFetchMedicineEvent hospitalMedicineQuest;

        [Tooltip("空则 FindObjectOfType")]
        public PlayerStatsHud statsHud;

        [Header("肖像")]
        public Sprite npcPortrait;
        public Sprite dronePortrait;

        [Header("调试 / 测试")]
        [Tooltip("勾选后：不写「取药完成」存档、回退时不删 PlayerPrefs；奖励卡按空格入库也不写去重键。适合反复试流程；发布前务必取消勾选。")]
        public bool skipPersistentProgress;

        [Header("存档")]
        [Tooltip("完成 E 线后的进度键。每名该类 NPC 须填不同键（如指路事件另设），避免与同学事件串档。留空则回退为 SickClassmate_MedicineComplete。")]
        public string progressPlayerPrefsKey = "SickClassmate_MedicineComplete";

        [Header("奖励（选 E 医院取药并完成对话后；流程由 RewardCardOfferService 统一处理）")]
        [Tooltip("空则从 GameplayHudLayout 或场景中查找")]
        public RewardCardOfferService rewardCardOfferService;

        [Tooltip("与入库去重一致，勿与其它事件重复")]
        public string medicinePathRewardStorageKey = "SickClassmate_RewardCardStored";

        [Tooltip("成就卡图案；空则不弹出奖励层")]
        public Sprite medicinePathRewardCardSprite;

        public string medicinePathRewardCardTitle = "暖心援护";

        [Header("台词")]
        [TextArea(1, 4)]
        public string lineNpcAsksHelp = "我现在身体有点不舒服，你可以帮帮我吗？";

        [TextArea(1, 4)]
        public string lineDroneRefuses = "对不起，我现在不方便帮助你。";

        [TextArea(1, 4)]
        public string lineDroneAcceptsHelp = "好吧让我想想办法";

        [TextArea(1, 3)]
        public string promptAcceptOrDecline = "按 空格 或 E 接受帮助，按 Esc 拒绝";

        [TextArea(1, 4)]
        public string promptBranchChoices = "按 Q 去食堂取食物，按 E 去医院取药";

        [TextArea(1, 4)]
        public string lineNpcThanksForMedicine = "谢谢你取的药，我感觉好多了。";

        [TextArea(1, 4)]
        [Tooltip("选 Q（食堂）后由同学说的台词")]
        [FormerlySerializedAs("lineDroneThanksForFood")]
        public string lineNpcThanksForFood = "谢谢你的吃的，不过我觉得我更需要一些胃药。";

        [Header("分支 E · 去医院取药（已绑定 hospitalMedicineQuest 时）")]
        [TextArea(1, 4)]
        [Tooltip("按 E 选医院线后，无人机先说的台词")]
        public string lineDroneOffersHospitalMedicine = "我去医院给你拿一些药吧。";

        [TextArea(1, 4)]
        [Tooltip("无人机说完后，NPC 指路")]
        public string lineNpcHospitalDirections = "好的，医院就在西北角，拜托了。";

        [TextArea(1, 2)]
        [Tooltip("两句播完后，按空格或 E 关闭对白并开始取药流程")]
        public string promptHospitalBranchContinue = "按 空格 或 E 继续";

        [Header("打字机")]
        [Tooltip("NPC 等正文打字音效")]
        public AudioClip typewriterTickClip;

        [Tooltip("无人机台词打字音效；不指定则沿用 NPC 音效")]
        public AudioClip typewriterTickClipDrone;

        [Tooltip("NPC 台词：每秒显示字符数（越大越快）")]
        [FormerlySerializedAs("charsPerSecond")]
        public float npcCharsPerSecond = 24f;

        [Tooltip("无人机台词：每秒显示字符数")]
        public float droneCharsPerSecond = 34f;

        [Tooltip("NPC 打字音最短间隔（秒）")]
        [FormerlySerializedAs("minTypewriterTickInterval")]
        public float npcMinTypewriterTickInterval = 0.03f;

        [Tooltip("无人机打字音最短间隔（秒）")]
        public float droneMinTypewriterTickInterval = 0.028f;

        [Range(0f, 1f)]
        public float typewriterSfxVolume = 0.35f;

        [Header("对话结算音乐")]
        [Tooltip("成功弹出成就卡时播放（与 Offer 同时）；空则静音")]
        public AudioClip rewardVictoryMusicClip;
        [Tooltip("本会话未获得成就卡就结束对话时播放；空则静音")]
        public AudioClip dialogueEndedWithoutRewardMusicClip;
        [Range(0f, 1f)]
        public float dialogueOutcomeMusicVolume = 0.85f;

        [Header("动画 / 提示")]
        [Tooltip("控制身体/序列图切换的 Animator；空则在本物体与子级上查找")]
        public Animator animator;

        public bool useFeelingBoolParameter = true;
        public string feelingGoodBoolParameter = "IsFeelingGood";

        [Tooltip("勾选后会对 unwell/happy 状态名执行 Play；可与 Bool 同时勾选（寻路靠 Bool 过渡时也能生效）。")]
        public bool useAnimatorPlayStates;
        public string unwellStateName = "Unwell";
        public string happyStateName = "Happy";
        public int animatorLayer;

        [Header("头顶提示")]
        [Tooltip("头顶提示根物体（可仅为一张图）；显隐由本脚本与靠近高亮控制。可与 promptBobbingTarget 为同一物体。")]
        public GameObject promptRoot;

        public Transform promptAnchor;

        [Tooltip("参与上下浮动的 Transform，一般为 NPC 上方那张 Sprite/图；空则对 promptRoot 根变换浮动")]
        public Transform promptBobbingTarget;

        [Tooltip("关闭则不再摆动（仍控制显隐）。用 unscaled 时间，对白 timeScale=0 时仍会动。若 prompt 带 Rigidbody2D，不要用物理驱动位移，否则会与摆动抢 Transform。")]
        public bool enablePromptBobbing = true;

        [Tooltip("摆动所在本地轴。灯泡在 2D 里「几乎不动」时可尝试 LocalX 或 LocalZ。")]
        public BranchingNpcPromptBobAxis promptBobAxis = BranchingNpcPromptBobAxis.LocalY;

        [Min(0f)]
        public float promptBobAmplitude = 0.35f;

        [Min(0.01f)]
        public float promptBobFrequency = 2f;

        [Tooltip("勾选：仅当无人机靠近且当前可互动时显示提示（与 NpcInteractable 一致）。不勾选：未完成主线前提示常显。")]
        public bool proximityPromptOnlyWhenNear;

        [Header("分支 Q（临时寻路）")]
        [Tooltip("按 Q 后保持寻路状态时长（秒，真实时间，不受 timeScale 影响）")]
        [Min(0.1f)]
        public float qBranchNavigatingSeconds = 3f;

        [Header("世界互动 E")]
        [Min(0.1f)]
        [Tooltip("无 Collider2D 时自动添加圆形触发器使用的半径；过小易导致无人机进不了互动范围")]
        [SerializeField] float npcInteractTriggerRadius = 1.6f;

        [Header("回退（未完成取药前）")]
        [Tooltip("运行中按下此键执行回退；选 None 则不监听键盘")]
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
            BranchAfterQNpcTyping,
            BranchAfterQWaitSpace,
            BranchAfterEHospitalDroneTyping,
            BranchAfterEHospitalNpcTyping,
            BranchAfterEHospitalWaitSpace,
            BranchAfterENpcTyping,
            BranchAfterEWaitSpace
        }

        Flow _flow = Flow.Idle;
        int _visibleChars;
        float _typewriterCharsCarry;
        float _lastTick;
        bool _lineDone;
        bool _medicineComplete;
        bool _rewardCardOfferedThisSession;

        bool _proximityHighlight;
        Vector3 _promptBobBaseLocal;
        bool _promptBobBaseReady;

        bool _qBranchRecovering;
        Coroutine _qRecoverRoutine;

        /// <summary>已选 E 且已发起医院取药，等待 <see cref="HospitalFetchMedicineEvent.Phase"/> 变为 HasMedicine 后再互动接续对白。</summary>
        bool _awaitingMedicineFromHospital;

        /// <summary>持药回到 NPC 并已打开感谢对白；在玩家按空格结束该段对白后再结算 <see cref="HospitalFetchMedicineEvent"/>（取药成功 + 奖励）。</summary>
        bool _pendingHospitalQuestCompleteAfterEDialogue;

        bool _warnedMissingDialoguePanel;

        AudioClip DroneTickClip => typewriterTickClipDrone != null ? typewriterTickClipDrone : typewriterTickClip;

        public int WorldInteractResolvePriority => 100;

        static bool AdvanceOrConfirmKeyDown()
        {
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);
        }

        string EffectiveProgressPrefsKey =>
            string.IsNullOrWhiteSpace(progressPlayerPrefsKey)
                ? "SickClassmate_MedicineComplete"
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
                Undo.RecordObject(circle, "SickClassmate Collider");
                circle.isTrigger = true;
                circle.radius = npcInteractTriggerRadius;
                col = circle;
            }
            else if (col != null && !col.isTrigger)
            {
                Undo.RecordObject(col, "SickClassmate Collider isTrigger");
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

            EnsureCollider2DTriggerRuntime();

            _medicineComplete = !skipPersistentProgress && PlayerPrefs.GetInt(EffectiveProgressPrefsKey, 0) != 0;

            if (!_medicineComplete)
                ApplyUnwellMood();
            else
                SetMoodHappy();

            BranchingQuestNpcPromptUtil.DisableBobbingWorldSpritesUnder(promptRoot);
            ResolveHospitalMedicineQuestIfNeeded();
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
                    $"{nameof(SickClassmateNpcController)} 「{name}」: 未找到 GameplayDialoguePanel，靠近按 E 无法开始对话。请在 Inspector 指定或确保场景中存在对白面板。",
                    this);
            }
        }

        void OnDisable()
        {
            StopQRecoverRoutine(clearFlags: true);
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
                _medicineComplete,
                proximityPromptOnlyWhenNear,
                _proximityHighlight,
                () => CanInteract(null));
            promptRoot.SetActive(show);
            if (show && !wasActive)
                _promptBobBaseReady = false;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"{nameof(SickClassmateNpcController)} 「{name}」: 场景中存在多个 HospitalFetchMedicineEvent，且未将 questNpc 指向本 NPC；已回退使用第一个实例，取药分支可能连错任务。请在 HospitalFetchMedicineEvent 上把 Quest Npc 设为此同学，或在 SickClassmate 上手动指定 Hospital Medicine Quest。",
                this);
#endif
            hospitalMedicineQuest = all[0];
        }

        public bool CanInteract(Transform interactor)
        {
            if (dialoguePanel == null)
                ResolveDialoguePanelIfNeeded();
            if (dialoguePanel == null)
                return false;

            if (_medicineComplete)
                return false;
            if (_qBranchRecovering)
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
            ResolveHospitalMedicineQuestIfNeeded();
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
                case Flow.BranchAfterQNpcTyping:
                    UpdateBranchAfterQNpcTyping();
                    break;
                case Flow.BranchAfterQWaitSpace:
                    UpdateBranchAfterQWaitSpace();
                    break;
                case Flow.BranchAfterEHospitalDroneTyping:
                    UpdateBranchAfterEHospitalDroneTyping();
                    break;
                case Flow.BranchAfterEHospitalNpcTyping:
                    UpdateBranchAfterEHospitalNpcTyping();
                    break;
                case Flow.BranchAfterEHospitalWaitSpace:
                    UpdateBranchAfterEHospitalWaitSpace();
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

            if (AdvanceOrConfirmKeyDown() && !_lineDone)
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

            if (AdvanceOrConfirmKeyDown())
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

            if (AdvanceOrConfirmKeyDown() && !_lineDone)
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

            if (AdvanceOrConfirmKeyDown() && !_lineDone)
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
            if (AdvanceOrConfirmKeyDown())
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
                ResolveStatsHud();
                if (statsHud != null)
                {
                    statsHud.AddProfessionalism(-1f);
                    statsHud.AddEmotion(1f);
                }

                StopQRecoverRoutine(clearFlags: true);
                _qBranchRecovering = true;

                ApplyUnwellMood();
                ApplyPromptRootVisibility();

                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.BranchAfterQNpcTyping;
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                StopQRecoverRoutine(clearFlags: true);

                dialoguePanel.ExternalSetChoicePrompt(false, null);

                ResolveHospitalMedicineQuestIfNeeded();
                if (hospitalMedicineQuest != null)
                    hospitalMedicineQuest.ResetPhaseForCompanionBranchIfStale(_medicineComplete);

                if (hospitalMedicineQuest != null &&
                    hospitalMedicineQuest.Phase != HospitalFetchMedicineEvent.QuestPhase.Completed)
                {
                    // 医院取药线：不扣情感度，避免对话结束后触发情感失败与失败音效（取药是正向任务）
                    ResolveStatsHud();
                    if (statsHud != null)
                        statsHud.AddProfessionalism(1f);

                    if (hospitalMedicineQuest.Phase == HospitalFetchMedicineEvent.QuestPhase.HasMedicine)
                    {
                        _awaitingMedicineFromHospital = true;
                        ApplyUnwellMood();
                        ApplyPromptRootVisibility();
                        EndDialogue();
                        return;
                    }

                    // 已在取药途中再次打开分支：不重复播无人机/NPC 两句，直接刷新取药点并关对白
                    if (hospitalMedicineQuest.Phase == HospitalFetchMedicineEvent.QuestPhase.GoingToPickup)
                    {
                        hospitalMedicineQuest.EnterGoingToPickupFromBranchingDialogue();
                        _awaitingMedicineFromHospital = true;
                        ApplyUnwellMood();
                        ApplyPromptRootVisibility();
                        EndDialogue();
                        return;
                    }

                    // 首次选 E：先播无人机承诺 + NPC 指路，再按空格进入取药阶段
                    dialoguePanel.ExternalClearBody();
                    dialoguePanel.ExternalEnsureBodyVisible();
                    dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
                    _visibleChars = 0;
                    _typewriterCharsCarry = 0f;
                    _lastTick = 0f;
                    _lineDone = false;
                    _flow = Flow.BranchAfterEHospitalDroneTyping;
                    return;
                }

                // 无医院任务：沿用旧版 E 线（专业度+1、情感度-1）
                ResolveStatsHud();
                if (statsHud != null)
                {
                    statsHud.AddProfessionalism(1f);
                    statsHud.AddEmotion(-1f);
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (hospitalMedicineQuest == null)
                {
                    Debug.LogWarning(
                        $"{nameof(SickClassmateNpcController)} 「{name}」: 按 E 时未找到 HospitalFetchMedicineEvent，将走无医院任务的「谢谢」短支。请在场景放置该组件或在本脚本指定 Hospital Medicine Quest。",
                        this);
                }
                else if (hospitalMedicineQuest.Phase == HospitalFetchMedicineEvent.QuestPhase.Completed)
                {
                    Debug.LogWarning(
                        $"{nameof(SickClassmateNpcController)} 「{name}」: HospitalFetchMedicineEvent 仍为 Completed（且同伴线未完成时本应已尝试拉回 Inactive）。将走「谢谢」短支；请检查任务状态或存档键。",
                        this);
                }
#endif

                _medicineComplete = true;
                if (!skipPersistentProgress)
                {
                    PlayerPrefs.SetInt(EffectiveProgressPrefsKey, 1);
                    PlayerPrefs.Save();
                }
                SetMoodHappy();
                ApplyPromptRootVisibility();

                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.BranchAfterENpcTyping;
            }
        }

        void BeginMedicineReturnThanksFlow()
        {
            _awaitingMedicineFromHospital = false;
            ResolveHospitalMedicineQuestIfNeeded();

            var cfg = hospitalMedicineQuest != null ? hospitalMedicineQuest.config : null;
            if (cfg != null && cfg.returnDialogueLines != null && cfg.returnDialogueLines.Length > 0 &&
                dialoguePanel != null)
            {
                var tempCfg = ScriptableObject.CreateInstance<HospitalFetchMedicineEventConfig>();
                tempCfg.dialogueLines = cfg.returnDialogueLines;
                tempCfg.defaultNpcPortrait = cfg.defaultNpcPortrait;
                tempCfg.defaultPlayerPortrait = cfg.defaultPlayerPortrait;
                tempCfg.charsPerSecond = cfg.charsPerSecond;
                tempCfg.minTypewriterTickInterval = cfg.minTypewriterTickInterval;
                tempCfg.typewriterTickClip = cfg.typewriterTickClip;
                tempCfg.typewriterSfxVolume = cfg.typewriterSfxVolume;

                if (dialoguePanel.BeginNarrativeSequence(tempCfg, () =>
                    {
                        Destroy(tempCfg);
                        FinishMedicineHospitalReturnAfterConfigThankYou();
                    }))
                {
                    _flow = Flow.Idle;
                    ApplyPromptRootVisibility();
                    return;
                }

                Destroy(tempCfg);
            }

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

        /// <summary>
        /// 使用任务配置里 <see cref="HospitalFetchMedicineEventConfig.returnDialogueLines"/> 播完送药感谢后，结算医院任务与同学线奖励。
        /// </summary>
        void FinishMedicineHospitalReturnAfterConfigThankYou()
        {
            if (hospitalMedicineQuest != null)
                hospitalMedicineQuest.CompleteQuestIfHasMedicine();
            _medicineComplete = true;
            if (!skipPersistentProgress)
            {
                PlayerPrefs.SetInt(EffectiveProgressPrefsKey, 1);
                PlayerPrefs.Save();
            }
            SetMoodHappy();
            ApplyPromptRootVisibility();

            ResolveRewardOfferService();
            if (rewardCardOfferService != null && medicinePathRewardCardSprite != null &&
                !string.IsNullOrEmpty(medicinePathRewardStorageKey))
            {
                _rewardCardOfferedThisSession = true;
                NpcEventOutcomeAudio.PlayClip2D(rewardVictoryMusicClip, dialogueOutcomeMusicVolume);
                rewardCardOfferService.Offer(
                    medicinePathRewardCardSprite,
                    medicinePathRewardCardTitle,
                    medicinePathRewardStorageKey,
                    persistPlayerPrefs: !skipPersistentProgress);
            }
            else
                EndDialogue();
        }

        void UpdateBranchAfterQNpcTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcThanksForFood ?? string.Empty;

            if (AdvanceOrConfirmKeyDown() && !_lineDone)
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
                _flow = Flow.BranchAfterQWaitSpace;
        }

        void UpdateBranchAfterQWaitSpace()
        {
            if (AdvanceOrConfirmKeyDown())
            {
                EndDialogue();
                ApplyPromptRootVisibility();
                StopQRecoverRoutine(clearFlags: false);
                _qRecoverRoutine = StartCoroutine(QBranchRecoverRoutine());
            }
        }

        void UpdateBranchAfterEHospitalDroneTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            var full = lineDroneOffersHospitalMedicine ?? string.Empty;

            if (AdvanceOrConfirmKeyDown() && !_lineDone)
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
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.BranchAfterEHospitalNpcTyping;
            }
        }

        void UpdateBranchAfterEHospitalNpcTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcHospitalDirections ?? string.Empty;

            if (AdvanceOrConfirmKeyDown() && !_lineDone)
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
                dialoguePanel.ExternalSetChoicePrompt(true, promptHospitalBranchContinue);
                _flow = Flow.BranchAfterEHospitalWaitSpace;
            }
        }

        void UpdateBranchAfterEHospitalWaitSpace()
        {
            if (!AdvanceOrConfirmKeyDown())
                return;

            dialoguePanel.ExternalSetChoicePrompt(false, null);

            if (hospitalMedicineQuest == null)
            {
                EndDialogue();
                return;
            }

            hospitalMedicineQuest.EnterGoingToPickupFromBranchingDialogue();
            if (hospitalMedicineQuest.Phase == HospitalFetchMedicineEvent.QuestPhase.GoingToPickup)
            {
                _awaitingMedicineFromHospital = true;
                ApplyUnwellMood();
                ApplyPromptRootVisibility();
                EndDialogue();
                return;
            }

            EndDialogue();
        }

        IEnumerator QBranchRecoverRoutine()
        {
            yield return new WaitForSecondsRealtime(qBranchNavigatingSeconds);
            _qRecoverRoutine = null;
            _qBranchRecovering = false;
            if (_medicineComplete)
                yield break;
            ApplyUnwellMood();
            ApplyPromptRootVisibility();
        }

        void StopQRecoverRoutine(bool clearFlags)
        {
            if (_qRecoverRoutine != null)
            {
                StopCoroutine(_qRecoverRoutine);
                _qRecoverRoutine = null;
            }
            if (clearFlags)
                _qBranchRecovering = false;
        }

        void UpdateBranchAfterENpcTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcThanksForMedicine ?? string.Empty;

            if (AdvanceOrConfirmKeyDown() && !_lineDone)
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
            if (!AdvanceOrConfirmKeyDown())
                return;

            if (_pendingHospitalQuestCompleteAfterEDialogue)
            {
                _pendingHospitalQuestCompleteAfterEDialogue = false;
                if (hospitalMedicineQuest != null)
                    hospitalMedicineQuest.CompleteQuestIfHasMedicine();
                _medicineComplete = true;
                if (!skipPersistentProgress)
                {
                    PlayerPrefs.SetInt(EffectiveProgressPrefsKey, 1);
                    PlayerPrefs.Save();
                }
                SetMoodHappy();
                ApplyPromptRootVisibility();

                ResolveRewardOfferService();
                if (rewardCardOfferService != null && medicinePathRewardCardSprite != null &&
                    !string.IsNullOrEmpty(medicinePathRewardStorageKey))
                {
                    _rewardCardOfferedThisSession = true;
                    NpcEventOutcomeAudio.PlayClip2D(rewardVictoryMusicClip, dialogueOutcomeMusicVolume);
                    EndDialogue();
                    rewardCardOfferService.Offer(
                        medicinePathRewardCardSprite,
                        medicinePathRewardCardTitle,
                        medicinePathRewardStorageKey,
                        persistPlayerPrefs: !skipPersistentProgress);
                }
                else
                    EndDialogue();
                return;
            }

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

        /// <summary>
        /// 清掉「已取药完成」存档标记，收起对白，恢复不舒服与头顶提示，可再次互动。
        /// 不改变 <see cref="PlayerStatsHud"/> 上的数值（若需一并还原请另写逻辑）。
        /// </summary>
        [ContextMenu("回退到可互动状态")]
        public void ResetToPreInteractState()
        {
            if (dialoguePanel != null && dialoguePanel.IsOpen)
                dialoguePanel.ForceCloseWithoutCallback();

            StopQRecoverRoutine(clearFlags: true);

            _flow = Flow.Idle;
            _visibleChars = 0;
            _typewriterCharsCarry = 0f;
            _lastTick = 0f;
            _lineDone = false;

            if (!skipPersistentProgress)
            {
                PlayerPrefs.DeleteKey(EffectiveProgressPrefsKey);
                if (!string.IsNullOrEmpty(medicinePathRewardStorageKey))
                    PlayerPrefs.DeleteKey(medicinePathRewardStorageKey);
                PlayerPrefs.Save();
            }
            _medicineComplete = false;
            _awaitingMedicineFromHospital = false;
            _pendingHospitalQuestCompleteAfterEDialogue = false;
            if (hospitalMedicineQuest != null &&
                hospitalMedicineQuest.Phase != HospitalFetchMedicineEvent.QuestPhase.Completed)
                hospitalMedicineQuest.CancelCompanionBranchFlowToInactive();

            ApplyUnwellMood();
            ApplyPromptRootVisibility();
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
