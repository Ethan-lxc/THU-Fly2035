using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>
    /// 最小可互动 NPC：开局不舒服动画 + 头顶提示（可配 <see cref="BobbingWorldSprite"/>）；提示在未互动前<strong>始终显示</strong>，与无人机距离无关；按 E 后变舒服并关提示。
    /// 不依赖医院任务；后续可再换成完整 <see cref="NpcInteractable"/> 流程。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SimpleMoodNpcInteractable : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("空则自动 GetComponentInChildren")]
        public Animator animator;

        [Tooltip("为 true：Animator bool，false=不舒服，true=舒服")]
        public bool useFeelingBoolParameter = true;

        public string feelingGoodBoolParameter = "IsFeelingGood";

        [Tooltip("为 true：用 Animator.Play；需填状态名")]
        public bool useAnimatorPlayStates;

        public string unwellStateName = "Unwell";
        public string happyStateName = "Happy";

        public int animatorLayer;

        [Tooltip("头顶提示根物体（下挂 Sprite + 可选 BobbingWorldSprite）；未互动前一直显示，按 E 后关闭")]
        public GameObject promptRoot;

        public Transform promptAnchor;

        bool _needsHelp = true;

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            var c = GetComponent<Collider2D>();
            if (c != null)
                c.isTrigger = true;

            if (promptRoot != null && _needsHelp)
                promptRoot.SetActive(true);

            ApplyUnwellMood();
        }

        public bool CanInteract(Transform interactor)
        {
            return _needsHelp;
        }

        public void BeginInteract(Transform interactor)
        {
            if (!_needsHelp)
                return;

            _needsHelp = false;
            SetMoodHappy();
            if (promptRoot != null)
                promptRoot.SetActive(false);
        }

        public Transform GetPromptAnchor()
        {
            return promptAnchor != null ? promptAnchor : transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
            // 提示常显，不随无人机是否在范围内切换
        }

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
    }
}
