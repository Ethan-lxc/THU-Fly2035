using UnityEngine;

namespace Gameplay.Events
{
    [RequireComponent(typeof(Collider2D))]
    public class MedicinePickupInteractable : MonoBehaviour, IWorldInteractable
    {
        public HospitalFetchMedicineEvent hospitalEvent;

        [Tooltip("靠近取药点时显示（可与浮动提示共用父物体）")]
        public GameObject proximityPromptRoot;

        void Awake()
        {
            var c = GetComponent<Collider2D>();
            if (c != null)
                c.isTrigger = true;
            if (proximityPromptRoot != null)
                proximityPromptRoot.SetActive(false);
        }

        public bool CanInteract(Transform interactor)
        {
            return hospitalEvent != null && hospitalEvent.CanPickupMedicine();
        }

        public void BeginInteract(Transform interactor)
        {
            if (hospitalEvent != null && hospitalEvent.CanPickupMedicine())
                hospitalEvent.OnMedicinePickedUp();
        }

        public Transform GetPromptAnchor()
        {
            return transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
            if (proximityPromptRoot == null)
                return;
            var ok = hospitalEvent != null && hospitalEvent.CanPickupMedicine();
            proximityPromptRoot.SetActive(highlighted && ok);
        }
    }
}
