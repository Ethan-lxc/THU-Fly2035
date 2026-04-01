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
}
