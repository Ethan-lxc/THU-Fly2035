using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Events
{
    /// <summary>
    /// 找自行车失主 · 分支对白 NPC：逻辑与 <see cref="SickClassmateNpcController"/> 相同，默认台词与存档键独立。
    /// 与同物体上其它 <see cref="IWorldInteractable"/> 二选一，勿同时挂两个。
    /// 未挂 Collider2D 时会自动添加 <see cref="CircleCollider2D"/>（Trigger）。
    /// </summary>
    [AddComponentMenu("Gameplay/Events/Find Bike NPC Controller")]
    public class FindBikeNpcController : MonoBehaviour, IWorldInteractable
    {
        [Header("对话 UI")]
        public GameplayDialoguePanel dialoguePanel;

        [Tooltip("空则 FindObjectOfType")]
        public PlayerStatsHud statsHud;

        [Header("肖像")]
        public Sprite npcPortrait;
        public Sprite dronePortrait;

        [Header("调试 / 测试")]
        [Tooltip("勾选后：不写「任务完成」存档、回退时不删 PlayerPrefs；奖励卡按空格入库也不写去重键。适合反复试流程；发布前务必取消勾选。")]
        public bool skipPersistentProgress;

        [Header("存档")]
        [Tooltip("完成 E 线后的进度键。每名 NPC 须填不同键，避免串档。留空则回退为 Quest_FindBike_NPCComplete。")]
        public string progressPlayerPrefsKey = "Quest_FindBike_NPCComplete";

        [Header("奖励（选 E 答应找车并完成对话后；流程由 RewardCardOfferService 统一处理）")]
        [Tooltip("空则从 GameplayHudLayout 或场景中查找")]
        public RewardCardOfferService rewardCardOfferService;

        [Tooltip("与入库去重一致，勿与其它事件重复")]
        public string findBikeRewardStorageKey = "FindBike_RewardCardStored";

        [Tooltip("成就卡图案；空则不弹出奖励层")]
        public Sprite findBikeRewardCardSprite;

        public string findBikeRewardCardTitle = "寻回自行车";

        [Header("台词")]
        [TextArea(1, 4)]
        public string lineNpcAsksHelp = "我的自行车不见了，你能帮我找回来吗？";

        [TextArea(1, 4)]
        public string lineDroneRefuses = "对不起，我现在不方便帮你。";

        [TextArea(1, 4)]
        public string lineDroneAcceptsHelp = "好吧，我试试看。";

        [TextArea(1, 3)]
        public string promptAcceptOrDecline = "按 空格 接受帮助，按 Esc 拒绝";

        [TextArea(1, 4)]
        public string promptBranchChoices = "按 Q 暂时离开，按 E 答应帮忙找车";

        [TextArea(1, 4)]
        public string lineNpcThanksAfterBike = "太谢谢你了，车找到了！";

        [TextArea(1, 4)]
        [Tooltip("选 Q 分支后由失主说的台词")]
        public string lineNpcAfterQBranch = "好吧，那我先自己找找……";

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

        [Header("分支 Q（临时离开）")]
        [Tooltip("按 Q 后保持「已答应帮忙」表现时长（秒，真实时间，不受 timeScale 影响）")]
        [Min(0.1f)]
        public float qBranchNavigatingSeconds = 3f;

        [Header("回退（任务未完成前）")]
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
            BranchAfterENpcTyping,
            BranchAfterEWaitSpace
        }

        Flow _flow = Flow.Idle;
        int _visibleChars;
        float _typewriterCharsCarry;
        float _lastTick;
        bool _lineDone;
        bool _bikeQuestComplete;
        bool _rewardCardOfferedThisSession;

        bool _proximityHighlight;
        Vector3 _promptBobBaseLocal;
        bool _promptBobBaseReady;

        bool _qBranchRecovering;
        Coroutine _qRecoverRoutine;

        AudioClip DroneTickClip => typewriterTickClipDrone != null ? typewriterTickClipDrone : typewriterTickClip;

        string EffectiveProgressPrefsKey =>
            string.IsNullOrWhiteSpace(progressPlayerPrefsKey)
                ? "Quest_FindBike_NPCComplete"
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
                Undo.RecordObject(col, "FindBike Collider isTrigger");
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

            _bikeQuestComplete = !skipPersistentProgress && PlayerPrefs.GetInt(EffectiveProgressPrefsKey, 0) != 0;

            if (!_bikeQuestComplete)
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
                _bikeQuestComplete,
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
            if (_bikeQuestComplete)
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

            dialoguePanel.ExternalSessionBegin();
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

                _bikeQuestComplete = true;
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
            if (_bikeQuestComplete)
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
            var full = lineNpcThanksAfterBike ?? string.Empty;

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
                if (rewardCardOfferService != null && findBikeRewardCardSprite != null &&
                    !string.IsNullOrEmpty(findBikeRewardStorageKey))
                {
                    _rewardCardOfferedThisSession = true;
                    NpcEventOutcomeAudio.PlayClip2D(rewardVictoryMusicClip, dialogueOutcomeMusicVolume);
                    EndDialogue();
                    rewardCardOfferService.Offer(
                        findBikeRewardCardSprite,
                        findBikeRewardCardTitle,
                        findBikeRewardStorageKey,
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
        /// 清掉「找车任务完成」存档标记，收起对白，恢复着急与头顶提示，可再次互动。
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
                if (!string.IsNullOrEmpty(findBikeRewardStorageKey))
                    PlayerPrefs.DeleteKey(findBikeRewardStorageKey);
                PlayerPrefs.Save();
            }
            _bikeQuestComplete = false;

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
