using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Events
{
    /// <summary>
    /// 指路事件 NPC：分支对白与奖励卡流程与 <see cref="SickClassmateNpcController"/> 相同，仅类名与默认存档键不同，便于单独挂载而无需与同学事件共用组件类型。
    /// 未挂 Collider2D 时会自动添加 <see cref="CircleCollider2D"/>（Trigger），避免 RequireComponent 导致无法在 Inspector 添加脚本。
    /// </summary>
    public class DirectionGuideNpcController : MonoBehaviour, IWorldInteractable, IInteractionRewindTarget
    {
        [Header("对话 UI")]
        public GameplayDialoguePanel dialoguePanel;

        [Tooltip("空则 FindObjectOfType")]
        public PlayerStatsHud statsHud;

        [Header("肖像")]
        public Sprite npcPortrait;
        public Sprite dronePortrait;

        [Header("调试 / 测试")]
        [Tooltip("勾选后：不写完成存档、回退时不删 PlayerPrefs；奖励卡按空格入库也不写去重键。发布前务必取消勾选。")]
        public bool skipPersistentProgress;

        [Header("存档")]
        [Tooltip("完成 E 线后的 PlayerPrefs 键。留空则回退为 DirectionGuide_BranchComplete。")]
        public string progressPlayerPrefsKey = "DirectionGuide_BranchComplete";

        [Header("奖励（选 E 主分支并完成对话后；由 RewardCardOfferService 处理）")]
        [Tooltip("空则从 GameplayHudLayout 或场景中查找")]
        public RewardCardOfferService rewardCardOfferService;

        [Tooltip("与入库去重一致，勿与其它事件重复")]
        public string medicinePathRewardStorageKey = "DirectionGuide_RewardCardStored";

        [Tooltip("成就卡图案；空则不弹出奖励层")]
        public Sprite medicinePathRewardCardSprite;

        public string medicinePathRewardCardTitle = "指路谢礼";

        [Header("台词")]
        [TextArea(1, 4)]
        public string lineNpcAsksHelp = "打扰一下，我有些认路……能帮我一下吗？";

        [TextArea(1, 4)]
        public string lineDroneRefuses = "对不起，我现在不方便帮助你。";

        [TextArea(1, 4)]
        [Tooltip("按 Esc 拒绝后：无人机拒绝对白播完后须再按一次空格，才会清空并播放本句 NPC 台词。留空则跳过 NPC 句，直接进入按空格关闭对话")]
        public string lineNpcAfterRejectCold = "真是个冷漠的家伙";

        [TextArea(1, 4)]
        public string lineDroneAcceptsHelp = "好吧让我想想办法";

        [TextArea(1, 3)]
        public string promptAcceptOrDecline = "按 空格 接受帮助，按 Esc 拒绝";

        [TextArea(1, 4)]
        public string promptBranchChoices = "按 Q …，按 E …（按你的剧情填写）";

        [TextArea(1, 3)]
        [Tooltip("拒绝分支等仅按空格推进时的对话底栏提示")]
        public string promptPressSpaceContinue = "按space继续对话";

        [TextArea(1, 4)]
        public string lineNpcThanksForMedicine = "谢谢你，我知道该怎么走了。";

        [TextArea(1, 4)]
        [Tooltip("选 Q 分支后由 NPC 说的台词")]
        public string lineNpcThanksForFood = "谢谢，不过我可能还需要别的帮助……";

        [Header("打字机")]
        [Tooltip("NPC 等正文打字音效")]
        public AudioClip typewriterTickClip;

        [Tooltip("无人机台词打字音效；不指定则沿用 NPC 音效")]
        public AudioClip typewriterTickClipDrone;

        [Tooltip("NPC 台词：每秒显示字符数（越大越快）")]
        public float npcCharsPerSecond = 24f;

        [Tooltip("无人机台词：每秒显示字符数")]
        public float droneCharsPerSecond = 34f;

        [Tooltip("NPC 打字音最短间隔（秒）")]
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
        [Tooltip("控制问路/寻路序列的 Animator；空则在本物体与子级上查找")]
        public Animator animator;

        public bool useFeelingBoolParameter = true;
        public string feelingGoodBoolParameter = "IsFeelingGood";

        [Tooltip("勾选后 Play 状态名；可与 Bool 同时勾选（控制器主要靠 Bool 过渡时请保持 Bool 勾选）。")]
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

        [Tooltip("关闭则不再摆动（仍控制显隐）。用 unscaled 时间，对白 timeScale=0 时仍会动。若 prompt 带 Rigidbody2D，不要用物理驱动位移。")]
        public bool enablePromptBobbing = true;

        [Tooltip("摆动所在本地轴。灯泡「几乎不动」时可尝试 LocalX 或 LocalZ。")]
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

        [Header("回退")]
        [Tooltip("运行中按下此键执行回退；选 None 则不监听键盘")]
        public KeyCode resetToPreInteractKey = KeyCode.None;

        enum Flow
        {
            Idle,
            NpcAskTyping,
            FirstChoice,
            AcceptDroneTyping,
            RejectDroneTyping,
            RejectAfterDroneWaitSpace,
            RejectNpcColdTyping,
            RejectWaitConfirm,
            BranchWaitKey,
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

        AudioClip DroneTickClip => typewriterTickClipDrone != null ? typewriterTickClipDrone : typewriterTickClip;

        string EffectiveProgressPrefsKey =>
            string.IsNullOrWhiteSpace(progressPlayerPrefsKey)
                ? "DirectionGuide_BranchComplete"
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
                col = Undo.AddComponent<CircleCollider2D>(gameObject);
            if (col != null && !col.isTrigger)
            {
                Undo.RecordObject(col, "DirectionGuide Collider isTrigger");
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
            ApplyPromptRootVisibility();
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

        public bool CanInteract(Transform interactor)
        {
            if (_medicineComplete)
                return false;
            if (_qBranchRecovering)
                return false;
            if (_flow != Flow.Idle)
                return false;
            if (dialoguePanel != null && dialoguePanel.IsOpen)
                return false;
            return true;
        }

        public void BeginInteract(Transform interactor)
        {
            if (!CanInteract(interactor) || dialoguePanel == null)
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
                case Flow.RejectAfterDroneWaitSpace:
                    UpdateRejectAfterDroneWaitSpace();
                    break;
                case Flow.RejectNpcColdTyping:
                    UpdateRejectNpcColdTyping();
                    break;
                case Flow.RejectWaitConfirm:
                    UpdateRejectWaitConfirm();
                    break;
                case Flow.BranchWaitKey:
                    UpdateBranchWaitKey();
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
                dialoguePanel.ExternalSetChoicePrompt(true, promptPressSpaceContinue);
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
            {
                dialoguePanel.ExternalSetChoicePrompt(true, promptPressSpaceContinue);
                if (string.IsNullOrWhiteSpace(lineNpcAfterRejectCold))
                    _flow = Flow.RejectWaitConfirm;
                else
                    _flow = Flow.RejectAfterDroneWaitSpace;
            }
        }

        void UpdateRejectAfterDroneWaitSpace()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);

            if (!Input.GetKeyDown(KeyCode.Space))
                return;

            dialoguePanel.ExternalClearBody();
            dialoguePanel.ExternalEnsureBodyVisible();
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            dialoguePanel.ExternalSetChoicePrompt(true, promptPressSpaceContinue);
            _visibleChars = 0;
            _typewriterCharsCarry = 0f;
            _lastTick = 0f;
            _lineDone = false;
            _flow = Flow.RejectNpcColdTyping;
        }

        void UpdateRejectNpcColdTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcAfterRejectCold ?? string.Empty;

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptPressSpaceContinue);
                _flow = Flow.RejectWaitConfirm;
            }
        }

        void UpdateRejectWaitConfirm()
        {
            dialoguePanel.ExternalSetChoicePrompt(true, promptPressSpaceContinue);

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
                ResolveStatsHud();
                if (statsHud != null)
                {
                    statsHud.AddProfessionalism(-1f);
                    statsHud.AddEmotion(1f);
                }

                StopQRecoverRoutine(clearFlags: false);
                _qBranchRecovering = true;
                SetMoodHappy();
                ApplyPromptRootVisibility();
                EndDialogue();
                _qRecoverRoutine = StartCoroutine(QBranchRecoverRoutine());
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                StopQRecoverRoutine(clearFlags: true);

                dialoguePanel.ExternalSetChoicePrompt(false, null);
                ResolveStatsHud();
                if (statsHud != null)
                {
                    statsHud.AddProfessionalism(1f);
                    statsHud.AddEmotion(-1f);
                }

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
            if (Input.GetKeyDown(KeyCode.Space))
            {
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
            }
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
        /// 清掉完成存档、收起对白，恢复「未完成」动画与头顶提示，可再次互动。
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
