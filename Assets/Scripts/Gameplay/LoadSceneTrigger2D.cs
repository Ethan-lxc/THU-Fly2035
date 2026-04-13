using Gameplay.Events;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// 2D 触发器或世界交互：进入触发器时加载目标场景（默认 Player 标签），或仅用无人机 E 交互加载。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LoadSceneTrigger2D : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] string targetSceneName = GameplaySceneNames.SupermarketSceneName;

        [Tooltip("勾选时仅用靠近+E 加载，不响应触发器进入")]
        [SerializeField] bool useInteractInsteadOfTriggerEnter;

        [Tooltip("未勾选 useInteract 时：进入触发器即加载")]
        [SerializeField] bool loadOnTriggerEnter = true;

        void Awake()
        {
            var c = GetComponent<Collider2D>();
            if (c != null)
                c.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (useInteractInsteadOfTriggerEnter || !loadOnTriggerEnter)
                return;
            if (!other.CompareTag("Player"))
                return;
            TryLoad();
        }

        void TryLoad()
        {
            if (string.IsNullOrEmpty(targetSceneName))
                return;
            GameplaySceneLoader.LoadSceneByGameplayName(targetSceneName);
        }

        public bool CanInteract(Transform interactor)
        {
            return useInteractInsteadOfTriggerEnter && !string.IsNullOrEmpty(targetSceneName);
        }

        public void BeginInteract(Transform interactor)
        {
            if (!useInteractInsteadOfTriggerEnter)
                return;
            TryLoad();
        }

        public Transform GetPromptAnchor()
        {
            return transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
        }
    }
}
