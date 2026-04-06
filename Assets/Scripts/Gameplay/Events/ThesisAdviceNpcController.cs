using System.Globalization;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Events
{
    /// <summary>
    /// 论文建议事件 · 分支对白 NPC：流程与 <see cref="SickClassmateNpcController"/> 相同，默认台词与存档键独立。
    /// 与同物体其它 <see cref="IWorldInteractable"/> 二选一，勿同时挂两个。
    /// 未挂 Collider2D 时会自动添加 <see cref="CircleCollider2D"/>（Trigger）。
    /// </summary>
    [AddComponentMenu("Gameplay/Events/Thesis Advice NPC Controller")]
    public class ThesisAdviceNpcController : MonoBehaviour, IWorldInteractable
    {
        [Header("对话 UI")]
        public GameplayDialoguePanel dialoguePanel;

        [Tooltip("空则 FindObjectOfType")]
        public PlayerStatsHud statsHud;

        [Header("肖像")]
        public Sprite npcPortrait;
        public Sprite dronePortrait;

        [Header("调试 / 测试")]
        [Tooltip("勾选后：不写「事件完成」存档、回退时不删 PlayerPrefs；奖励卡按空格入库也不写去重键。适合反复试流程；发布前务必取消勾选。")]
        public bool skipPersistentProgress;

        [Header("存档")]
        [Tooltip("完成 E 线后的进度键。勿与其它事件重复。留空则回退为 ThesisAdvice_EventComplete。")]
        public string progressPlayerPrefsKey = "ThesisAdvice_EventComplete";

        [Header("互动门槛（统计不足时仅无人机提示，不进入求助对白）")]
        [Tooltip("勾选：玩家互动时若专业/情感任一低于下列阈值，则拦截并提示")]
        public bool requireMinStatsToInteract = true;
        [Tooltip("所需最低专业值（PlayerStatsHud.Professionalism）")]
        public float minProfessionalismToInteract = 80f;
        [Tooltip("所需最低情感值（PlayerStatsHud.Emotion）")]
        public float minEmotionToInteract = 50f;
        [TextArea(2, 5)]
        [Tooltip("占位符 {0}=专业门槛数值，{1}=情感门槛数值")]
        public string lineDroneStatsInsufficient =
            "我需要先提升我的专业值和情感值分别达到{0}和{1}，才能帮助他。";
        [TextArea(1, 2)]
        public string promptStatsGateReturn = "按 空格 回到游戏";

        [Header("奖励（Q / E / W 任一分支在 NPC 说完后统一发放；由 RewardCardOfferService 处理）")]
        [Tooltip("空则从 GameplayHudLayout 或场景中查找")]
        public RewardCardOfferService rewardCardOfferService;

        [Tooltip("与入库去重一致，勿与其它事件重复")]
        public string thesisAdviceRewardStorageKey = "ThesisAdvice_RewardCardStored";

        [Tooltip("成就卡图案；空则不弹出奖励层")]
        public Sprite thesisAdviceRewardCardSprite;

        public string thesisAdviceRewardCardTitle = "研思同行";

        [Header("台词")]
        [TextArea(1, 4)]
        public string lineNpcAsksHelp = "我最近总是失眠，睡不好，你能给我一些建议吗？";

        [TextArea(1, 4)]
        [Tooltip("玩家按 Esc 拒绝帮助后，由 NPC 说出本句，说完即结束互动")]
        public string lineNpcSaysWhenEscRejects = "果然，你只是个无人机，还是帮不了我。";

        [TextArea(1, 4)]
        public string lineDroneAcceptsHelp = "我想想怎么帮你。";

        [TextArea(1, 3)]
        public string promptAcceptOrDecline = "按 空格 接受聊聊，按 Esc 拒绝";

        [TextArea(1, 4)]
        public string promptBranchChoices = "按 Q 操场散心，按 E 梳理汇报，按 W 去医院";

        [Header("分支过渡（选 Q/E/W 后无人机先说一句；说完后按空格再轮到 NPC）")]
        [TextArea(1, 3)]
        public string lineDroneLeadInQ = "我们去操场吧。";

        [TextArea(1, 3)]
        public string lineDroneLeadInW = "要不要去医院看一下呢吧";

        [TextArea(1, 4)]
        public string lineDroneLeadInE = "我来带你梳理\n下明天的汇报吧。";

        [TextArea(1, 3)]
        [Tooltip("无人机过渡句打完后，底部提示")]
        public string promptAfterLeadIn = "按 空格 继续";

        [TextArea(1, 3)]
        [Tooltip("NPC 主台词打完后，底部提示（按空格领奖并结束对话）")]
        public string promptAfterNpcLine = "按 空格 继续";

        [TextArea(1, 4)]
        [Tooltip("选 Q：无人机过渡并按空格后，由 NPC 说本句")]
        public string lineNpcSaysOnQ = "果然在操场上吹风会心情变好，谢谢你，我已经不担心啦";

        [TextArea(1, 4)]
        [Tooltip("选 E：无人机过渡并按空格后，由 NPC 说本句")]
        public string lineNpcSaysOnE = "谢谢你！我对明天的汇报放心多了，只不过我们梳理到现在了，还有睡觉的时间吗？";

        [TextArea(1, 4)]
        [Tooltip("选 W：无人机过渡并按空格后，由 NPC 说本句")]
        public string lineNpcSaysOnW = "去医院干什么，现在时间这么紧，你还是别管我了";

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

        [Header("回退（事件未完成前）")]
        [Tooltip("运行中按下此键执行回退；选 None 则不监听键盘")]
        public KeyCode resetToPreInteractKey = KeyCode.None;

        enum Flow
        {
            Idle,
            StatsGateDroneTyping,
            StatsGateWaitSpace,
            NpcAskTyping,
            FirstChoice,
            AcceptDroneTyping,
            NpcTypingAfterEscReject,
            BranchWaitKey,
            BranchDroneLeadInQ,
            LeadInQWaitSpace,
            NpcSaysQTyping,
            NpcSaysQWaitSpace,
            BranchDroneLeadInE,
            LeadInEWaitSpace,
            NpcSaysETyping,
            NpcSaysEWaitSpace,
            BranchDroneLeadInW,
            LeadInWWaitSpace,
            NpcSaysWTyping,
            NpcSaysWWaitSpace
        }

        Flow _flow = Flow.Idle;
        int _visibleChars;
        float _typewriterCharsCarry;
        float _lastTick;
        bool _lineDone;
        bool _adviceComplete;
        bool _rewardCardOfferedThisSession;
        bool _suppressFailureOutcomeMusic;

        bool _proximityHighlight;
        Vector3 _promptBobBaseLocal;
        bool _promptBobBaseReady;

        AudioClip DroneTickClip => typewriterTickClipDrone != null ? typewriterTickClipDrone : typewriterTickClip;

        string EffectiveProgressPrefsKey =>
            string.IsNullOrWhiteSpace(progressPlayerPrefsKey)
                ? "ThesisAdvice_EventComplete"
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
                Undo.RecordObject(col, "ThesisAdvice Collider isTrigger");
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

            _adviceComplete = !skipPersistentProgress && PlayerPrefs.GetInt(EffectiveProgressPrefsKey, 0) != 0;

            if (!_adviceComplete)
                ApplyUnwellMood();
            else
                SetMoodHappy();

            BranchingQuestNpcPromptUtil.DisableBobbingWorldSpritesUnder(promptRoot);
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
                _adviceComplete,
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
            if (_adviceComplete)
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

            ResolveStatsHud();
            if (requireMinStatsToInteract && statsHud != null &&
                (statsHud.Professionalism < minProfessionalismToInteract ||
                 statsHud.Emotion < minEmotionToInteract))
            {
                BeginStatsInsufficientFlow();
                return;
            }

            dialoguePanel.ExternalSessionBegin();
            _flow = Flow.NpcAskTyping;
            _visibleChars = 0;
            _typewriterCharsCarry = 0f;
            _lastTick = 0f;
            _lineDone = false;
            _rewardCardOfferedThisSession = false;
            _suppressFailureOutcomeMusic = false;

            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            dialoguePanel.ExternalClearBody();
            dialoguePanel.ExternalEnsureBodyVisible();
            dialoguePanel.ExternalSetChoicePrompt(false, null);
            ApplyPromptRootVisibility();
        }

        void BeginStatsInsufficientFlow()
        {
            dialoguePanel.ExternalSessionBegin();
            _flow = Flow.StatsGateDroneTyping;
            _visibleChars = 0;
            _typewriterCharsCarry = 0f;
            _lastTick = 0f;
            _lineDone = false;
            _rewardCardOfferedThisSession = false;
            _suppressFailureOutcomeMusic = true;

            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            dialoguePanel.ExternalClearBody();
            dialoguePanel.ExternalEnsureBodyVisible();
            dialoguePanel.ExternalSetChoicePrompt(false, null);
            ApplyPromptRootVisibility();
        }

        string StatsGateLineFormatted()
        {
            var proShow = Mathf.RoundToInt(minProfessionalismToInteract).ToString(CultureInfo.InvariantCulture);
            var emoShow = Mathf.RoundToInt(minEmotionToInteract).ToString(CultureInfo.InvariantCulture);
            var fmt = lineDroneStatsInsufficient ?? string.Empty;
            try
            {
                return string.Format(fmt, proShow, emoShow);
            }
            catch (System.FormatException)
            {
                return fmt;
            }
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
                case Flow.StatsGateDroneTyping:
                    UpdateStatsGateDroneTyping();
                    break;
                case Flow.StatsGateWaitSpace:
                    UpdateStatsGateWaitSpace();
                    break;
                case Flow.NpcAskTyping:
                    UpdateNpcAskTyping();
                    break;
                case Flow.FirstChoice:
                    UpdateFirstChoice();
                    break;
                case Flow.AcceptDroneTyping:
                    UpdateAcceptDroneTyping();
                    break;
                case Flow.NpcTypingAfterEscReject:
                    UpdateNpcTypingAfterEscReject();
                    break;
                case Flow.BranchWaitKey:
                    UpdateBranchWaitKey();
                    break;
                case Flow.BranchDroneLeadInQ:
                    UpdateBranchDroneLeadInQ();
                    break;
                case Flow.LeadInQWaitSpace:
                    UpdateLeadInQWaitSpace();
                    break;
                case Flow.NpcSaysQTyping:
                    UpdateNpcSaysQTyping();
                    break;
                case Flow.NpcSaysQWaitSpace:
                    UpdateNpcSaysQWaitSpace();
                    break;
                case Flow.BranchDroneLeadInE:
                    UpdateBranchDroneLeadInE();
                    break;
                case Flow.LeadInEWaitSpace:
                    UpdateLeadInEWaitSpace();
                    break;
                case Flow.NpcSaysETyping:
                    UpdateNpcSaysETyping();
                    break;
                case Flow.NpcSaysEWaitSpace:
                    UpdateNpcSaysEWaitSpace();
                    break;
                case Flow.BranchDroneLeadInW:
                    UpdateBranchDroneLeadInW();
                    break;
                case Flow.LeadInWWaitSpace:
                    UpdateLeadInWWaitSpace();
                    break;
                case Flow.NpcSaysWTyping:
                    UpdateNpcSaysWTyping();
                    break;
                case Flow.NpcSaysWWaitSpace:
                    UpdateNpcSaysWWaitSpace();
                    break;
            }
        }

        void UpdateStatsGateDroneTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            var full = StatsGateLineFormatted();

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptStatsGateReturn);
                _flow = Flow.StatsGateWaitSpace;
            }
        }

        void UpdateStatsGateWaitSpace()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            if (Input.GetKeyDown(KeyCode.Space))
                EndDialogue();
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
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.NpcTypingAfterEscReject;
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

        void UpdateNpcTypingAfterEscReject()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcSaysWhenEscRejects ?? string.Empty;

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
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.BranchDroneLeadInQ;
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.BranchDroneLeadInE;
                return;
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.BranchDroneLeadInW;
            }
        }

        void FinishThesisAdviceBranchRewardAndCalm()
        {
            _adviceComplete = true;
            if (!skipPersistentProgress)
            {
                PlayerPrefs.SetInt(EffectiveProgressPrefsKey, 1);
                PlayerPrefs.Save();
            }

            SetMoodHappy();
            ApplyPromptRootVisibility();
            ResolveRewardOfferService();
            if (rewardCardOfferService != null && thesisAdviceRewardCardSprite != null &&
                !string.IsNullOrEmpty(thesisAdviceRewardStorageKey))
            {
                _rewardCardOfferedThisSession = true;
                NpcEventOutcomeAudio.PlayClip2D(rewardVictoryMusicClip, dialogueOutcomeMusicVolume);
                EndDialogue();
                rewardCardOfferService.Offer(
                    thesisAdviceRewardCardSprite,
                    thesisAdviceRewardCardTitle,
                    thesisAdviceRewardStorageKey,
                    persistPlayerPrefs: !skipPersistentProgress);
            }
            else
                EndDialogue();
        }

        void UpdateBranchDroneLeadInQ()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            var full = lineDroneLeadInQ ?? string.Empty;

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptAfterLeadIn);
                _flow = Flow.LeadInQWaitSpace;
            }
        }

        void UpdateLeadInQWaitSpace()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.NpcSaysQTyping;
            }
        }

        void UpdateNpcSaysQTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcSaysOnQ ?? string.Empty;

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptAfterNpcLine);
                _flow = Flow.NpcSaysQWaitSpace;
            }
        }

        void UpdateNpcSaysQWaitSpace()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                ResolveStatsHud();
                if (statsHud != null)
                {
                    statsHud.AddEmotion(3f);
                    statsHud.AddProfessionalism(2f);
                }

                FinishThesisAdviceBranchRewardAndCalm();
            }
        }

        void UpdateBranchDroneLeadInE()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            var full = lineDroneLeadInE ?? string.Empty;

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptAfterLeadIn);
                _flow = Flow.LeadInEWaitSpace;
            }
        }

        void UpdateLeadInEWaitSpace()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.NpcSaysETyping;
            }
        }

        void UpdateNpcSaysETyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcSaysOnE ?? string.Empty;

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptAfterNpcLine);
                _flow = Flow.NpcSaysEWaitSpace;
            }
        }

        void UpdateNpcSaysEWaitSpace()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                ResolveStatsHud();
                if (statsHud != null)
                {
                    statsHud.AddEmotion(2f);
                    statsHud.AddProfessionalism(3f);
                }

                FinishThesisAdviceBranchRewardAndCalm();
            }
        }

        void UpdateBranchDroneLeadInW()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(dronePortrait);
            var full = lineDroneLeadInW ?? string.Empty;

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptAfterLeadIn);
                _flow = Flow.LeadInWWaitSpace;
            }
        }

        void UpdateLeadInWWaitSpace()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                dialoguePanel.ExternalClearBody();
                dialoguePanel.ExternalEnsureBodyVisible();
                dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
                _visibleChars = 0;
                _typewriterCharsCarry = 0f;
                _lastTick = 0f;
                _lineDone = false;
                _flow = Flow.NpcSaysWTyping;
            }
        }

        void UpdateNpcSaysWTyping()
        {
            dialoguePanel.ExternalApplyPortraitRightOnly(npcPortrait);
            var full = lineNpcSaysOnW ?? string.Empty;

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
                dialoguePanel.ExternalSetChoicePrompt(true, promptAfterNpcLine);
                _flow = Flow.NpcSaysWWaitSpace;
            }
        }

        void UpdateNpcSaysWWaitSpace()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                dialoguePanel.ExternalSetChoicePrompt(false, null);
                ResolveStatsHud();
                if (statsHud != null)
                {
                    statsHud.AddEmotion(-2f);
                    statsHud.AddProfessionalism(-1f);
                }

                FinishThesisAdviceBranchRewardAndCalm();
            }
        }

        void EndDialogue()
        {
            if (!_rewardCardOfferedThisSession && !_suppressFailureOutcomeMusic)
                NpcEventOutcomeAudio.PlayClip2D(dialogueEndedWithoutRewardMusicClip, dialogueOutcomeMusicVolume);
            if (dialoguePanel != null)
                dialoguePanel.ExternalSessionEnd();
            _flow = Flow.Idle;
            _suppressFailureOutcomeMusic = false;
            ApplyPromptRootVisibility();
        }

        /// <summary>
        /// 清掉「事件完成」存档标记，收起对白，恢复疲惫/未缓解与头顶提示，可再次互动。
        /// 不改变 <see cref="PlayerStatsHud"/> 上的数值（若需一并还原请另写逻辑）。
        /// </summary>
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
            _suppressFailureOutcomeMusic = false;

            if (!skipPersistentProgress)
            {
                PlayerPrefs.DeleteKey(EffectiveProgressPrefsKey);
                if (!string.IsNullOrEmpty(thesisAdviceRewardStorageKey))
                    PlayerPrefs.DeleteKey(thesisAdviceRewardStorageKey);
                PlayerPrefs.Save();
            }
            _adviceComplete = false;

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
