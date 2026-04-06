using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay.Events
{
    /// <summary>
    /// 通用「任务 NPC」集成：头顶提示、靠近高亮、Animator 心情、与任意实现 <see cref="IQuestWorldEvent"/> 的任务根通信。
    /// 取药等任务在 Inspector 将 <see cref="questEvent"/> 指到对应事件组件。
    /// </summary>
    [AddComponentMenu("Gameplay/Events/Quest NPC Interactable")]
    [RequireComponent(typeof(Collider2D))]
    public class QuestNpcInteractable : MonoBehaviour, IWorldInteractable, IQuestNpcMood
    {
        [FormerlySerializedAs("hospitalEvent")]
        [Tooltip("任务根上的事件组件（须实现 IQuestWorldEvent），如 HospitalFetchMedicineEvent")]
        public MonoBehaviour questEvent;

        [Tooltip("头顶互动图标；靠近时显示")]
        public GameObject promptRoot;

        [Tooltip("提示图挂点；空则用本物体")]
        public Transform promptAnchor;

        [Header("表现 · 心情动画")]
        [Tooltip("空则在本物体及子物体上查找")]
        public Animator animator;

        [Tooltip("为 true 时用 Animator bool：false=任务未完成/不舒服，true=完成后愉快")]
        public bool useFeelingBoolParameter = true;

        [Tooltip("需在 Animator 中创建同名 bool 参数")]
        public string feelingGoodBoolParameter = "IsFeelingGood";

        [Tooltip("为 true 时用 Animator.Play 指定状态名（与上面二选一，Play 优先若勾选且状态名非空）")]
        public bool useAnimatorPlayStates;

        [Tooltip("任务未完成 / 进行中：播放的状态名")]
        public string unwellStateName = "Unwell";

        [Tooltip("任务完成后：播放的状态名")]
        public string happyStateName = "Happy";

        public int animatorLayer;

        IQuestWorldEvent _questBinding;

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            var c = GetComponent<Collider2D>();
            if (c != null)
                c.isTrigger = true;
            if (promptRoot != null)
                promptRoot.SetActive(false);

            ResolveBinding();
        }

        void ResolveBinding()
        {
            _questBinding = questEvent as IQuestWorldEvent;
            if (_questBinding == null && questEvent != null)
                Debug.LogWarning(
                    $"{nameof(QuestNpcInteractable)} on {name}: {questEvent.GetType().Name} 未实现 {nameof(IQuestWorldEvent)}，互动将无效。",
                    this);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                return;
            ResolveBinding();
        }
#endif

        /// <summary>任务未完成时的默认表现。由事件在开局或未完成时调用。</summary>
        public void ApplyUnwellMood()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            if (useAnimatorPlayStates && !string.IsNullOrEmpty(unwellStateName))
                animator.Play(unwellStateName, animatorLayer, 0f);
            else if (useFeelingBoolParameter && !string.IsNullOrEmpty(feelingGoodBoolParameter))
                animator.SetBool(feelingGoodBoolParameter, false);
        }

        /// <summary>任务完成后的愉快状态。</summary>
        public void SetMoodHappy()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            if (useAnimatorPlayStates && !string.IsNullOrEmpty(happyStateName))
                animator.Play(happyStateName, animatorLayer, 0f);
            else if (useFeelingBoolParameter && !string.IsNullOrEmpty(feelingGoodBoolParameter))
                animator.SetBool(feelingGoodBoolParameter, true);
        }

        public bool CanInteract(Transform interactor)
        {
            if (_questBinding == null)
                ResolveBinding();
            return _questBinding != null && _questBinding.CanInteractNpc(this);
        }

        public void BeginInteract(Transform interactor)
        {
            if (_questBinding == null)
                ResolveBinding();
            _questBinding?.OnNpcInteract(this);
        }

        public Transform GetPromptAnchor()
        {
            return promptAnchor != null ? promptAnchor : transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
            if (promptRoot == null)
                return;
            if (_questBinding == null)
                ResolveBinding();
            var ok = _questBinding != null && _questBinding.CanInteractNpc(this);
            promptRoot.SetActive(highlighted && ok);
        }
    }

    /// <summary>
    /// 兼容旧 Prefab / 场景：与 <see cref="QuestNpcInteractable"/> 行为一致。
    /// 新场景建议直接挂 <see cref="QuestNpcInteractable"/>，并将 <c>questEvent</c> 指向实现 <see cref="IQuestWorldEvent"/> 的任务根。
    /// </summary>
    [AddComponentMenu("Gameplay/Events/Npc Interactable (Quest, Legacy)")]
    public class NpcInteractable : QuestNpcInteractable
    {
    }
}
