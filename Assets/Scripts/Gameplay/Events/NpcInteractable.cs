using UnityEngine;

namespace Gameplay.Events
{
    [RequireComponent(typeof(Collider2D))]
    public class NpcInteractable : MonoBehaviour, IWorldInteractable
    {
        public HospitalFetchMedicineEvent hospitalEvent;

        [Tooltip("头顶互动图标；靠近时显示")]
        public GameObject promptRoot;

        [Tooltip("提示图挂点；空则用本物体")]
        public Transform promptAnchor;

        [Header("表现 · 心情动画")]
        [Tooltip("空则在本物体及子物体上查找")]
        public Animator animator;

        [Tooltip("为 true 时用 Animator bool：false=不舒服，true=送药后愉快")]
        public bool useFeelingBoolParameter = true;

        [Tooltip("需在 Animator 中创建同名 bool 参数")]
        public string feelingGoodBoolParameter = "IsFeelingGood";

        [Tooltip("为 true 时用 Animator.Play 指定状态名（与上面二选一，Play 优先若勾选且状态名非空）")]
        public bool useAnimatorPlayStates;

        [Tooltip("未送药 / 任务进行中：播放的状态名")]
        public string unwellStateName = "Unwell";

        [Tooltip("送药完成任务后：播放的状态名")]
        public string happyStateName = "Happy";

        public int animatorLayer;

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            var c = GetComponent<Collider2D>();
            if (c != null)
                c.isTrigger = true;
            if (promptRoot != null)
                promptRoot.SetActive(false);
        }

        /// <summary>不舒服（默认待机）。由任务系统在开局或未完成时调用。</summary>
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

        /// <summary>送药完成后的愉快状态。</summary>
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
            return hospitalEvent != null && hospitalEvent.CanInteractNpc(this);
        }

        public void BeginInteract(Transform interactor)
        {
            hospitalEvent?.OnNpcInteract(this);
        }

        public Transform GetPromptAnchor()
        {
            return promptAnchor != null ? promptAnchor : transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
            if (promptRoot == null)
                return;
            var ok = hospitalEvent != null && hospitalEvent.CanInteractNpc(this);
            promptRoot.SetActive(highlighted && ok);
        }
    }
}
