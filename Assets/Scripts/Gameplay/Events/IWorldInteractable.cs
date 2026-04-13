using UnityEngine;

namespace Gameplay.Events
{
    public interface IWorldInteractable
    {
        bool CanInteract(Transform interactor);

        void BeginInteract(Transform interactor);

        Transform GetPromptAnchor();

        void SetProximityHighlight(bool highlighted);
    }

    /// <summary>
    /// 可选：同屏多个可互动目标时数值越大越优先（距离作为次级排序）。
    /// </summary>
    public interface IWorldInteractableResolvePriority
    {
        int WorldInteractResolvePriority { get; }
    }
}
