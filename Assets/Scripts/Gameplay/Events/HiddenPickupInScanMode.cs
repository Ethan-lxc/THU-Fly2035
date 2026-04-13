using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.Events
{
    /// <summary>
    /// 校园卡类隐藏拾取：默认隐藏显示；扫描模式开启且无人机进入半径内播放提示音；
    /// 扫描路径下默认按 <see cref="pickupKey"/>（默认 Space）拾取；若 <see cref="pickupFlowBinder"/> 指向 Direct 分支则本组件不参与逻辑。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class HiddenPickupInScanMode : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] HospitalFetchMedicineEvent hospitalEvent;

        [Tooltip("超市校园卡 Q/E 分流；未指定则始终视为扫描路径")]
        [SerializeField] SupermarketCampusCardFlowBinder pickupFlowBinder;

        [Tooltip("空则 FindObjectOfType（超市场景内需有独立实例）")]
        [SerializeField] SupermarketScanModeController scanModeController;

        [Tooltip("相对无人机位置的扫描判定半径（世界单位）")]
        [Min(0.1f)]
        [SerializeField] float scanRadius = 6f;

        [Tooltip("显形前隐藏的根（子物体 Sprite 等）；接受任务后仍隐藏直至 E 互动")]
        [SerializeField] GameObject visualRoot;

        [Tooltip("靠近且可互动时在 Scene 中高亮用的提示根，可选")]
        [SerializeField] GameObject proximityPromptRoot;

        [Tooltip("提示音；进入扫描范围且扫描模式开启时循环播放")]
        [SerializeField] AudioClip proximityBeepClip;

        [SerializeField] [Range(0f, 1f)] float beepVolume = 0.6f;

        [Tooltip("扫描模式下拾取键（默认 Space）；为 true 时 BeginInteract 不再拾取，避免与无人机世界互动 E 重复")]
        [SerializeField] KeyCode pickupKey = KeyCode.Space;

        [SerializeField] bool pickupUsesSpaceKeyOnly = true;

        AudioSource _audio;
        IsoDroneController _drone;

        void Awake()
        {
            var c = GetComponent<Collider2D>();
            if (c != null)
                c.isTrigger = true;

            if (visualRoot != null)
                visualRoot.SetActive(false);
            if (proximityPromptRoot != null)
                proximityPromptRoot.SetActive(false);

            if (hospitalEvent == null)
                hospitalEvent = HospitalFetchMedicineEvent.FindBestForPickupTransform(transform);
            if (scanModeController == null)
                scanModeController = FindObjectOfType<SupermarketScanModeController>();
            if (pickupFlowBinder == null)
                pickupFlowBinder = FindObjectOfType<SupermarketCampusCardFlowBinder>();

            if (proximityBeepClip != null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.loop = true;
                _audio.spatialBlend = 0f;
                _audio.clip = proximityBeepClip;
                _audio.volume = beepVolume;
            }
        }

        void Update()
        {
            if (!IsScanPathActive())
            {
                if (_audio != null && _audio.isPlaying)
                    _audio.Stop();
                if (proximityPromptRoot != null)
                    proximityPromptRoot.SetActive(false);
                return;
            }

            if (scanModeController == null)
                scanModeController = FindObjectOfType<SupermarketScanModeController>();
            if (_drone == null)
                _drone = FindObjectOfType<IsoDroneController>();

            var inRange = IsDroneInScanRange();
            var shouldBeep = hospitalEvent != null &&
                             hospitalEvent.CanPickupMedicine() &&
                             scanModeController != null &&
                             scanModeController.IsScanModeActive &&
                             inRange;

            if (_audio != null)
            {
                if (shouldBeep && !_audio.isPlaying)
                    _audio.Play();
                else if (!shouldBeep && _audio.isPlaying)
                    _audio.Stop();
            }

            if (proximityPromptRoot != null)
                proximityPromptRoot.SetActive(shouldBeep);

            if (pickupUsesSpaceKeyOnly && CanPickupWithSpace(inRange) && Input.GetKeyDown(pickupKey))
                ExecutePickup();
        }

        bool IsScanPathActive()
        {
            if (pickupFlowBinder == null)
                return true;
            return pickupFlowBinder.CurrentPath == SupermarketCampusCardFlowBinder.CampusCardPickupPath.ScanMode;
        }

        bool CanPickupWithSpace(bool inRange)
        {
            if (GameplayModalBlocker.IsBlockingInput)
                return false;
            if (IsDialogueOpen())
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (hospitalEvent == null || !hospitalEvent.CanPickupMedicine())
                return false;
            if (scanModeController == null || !scanModeController.IsScanModeActive)
                return false;
            return inRange;
        }

        static bool IsDialogueOpen()
        {
            var panels = FindObjectsOfType<GameplayDialoguePanel>(false);
            for (var i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i].IsOpen)
                    return true;
            }
            return false;
        }

        void ExecutePickup()
        {
            if (_audio != null && _audio.isPlaying)
                _audio.Stop();

            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (hospitalEvent != null)
                hospitalEvent.OnMedicinePickedUp();
        }

        bool IsDroneInScanRange()
        {
            if (_drone == null)
                _drone = FindObjectOfType<IsoDroneController>();
            if (_drone == null)
                return false;
            var p = (Vector2)_drone.transform.position;
            return Vector2.Distance(p, (Vector2)transform.position) <= scanRadius;
        }

        public bool CanInteract(Transform interactor)
        {
            if (!IsScanPathActive())
                return false;
            if (pickupUsesSpaceKeyOnly)
                return false;
            if (hospitalEvent == null || !hospitalEvent.CanPickupMedicine())
                return false;
            if (scanModeController == null || !scanModeController.IsScanModeActive)
                return false;
            return IsDroneInScanRange();
        }

        public void BeginInteract(Transform interactor)
        {
            if (pickupUsesSpaceKeyOnly)
                return;
            if (!CanInteract(interactor))
                return;
            ExecutePickup();
        }

        public Transform GetPromptAnchor()
        {
            return transform;
        }

        public void SetProximityHighlight(bool highlighted)
        {
            if (proximityPromptRoot == null)
                return;
            if (!highlighted || !IsScanPathActive())
            {
                proximityPromptRoot.SetActive(false);
                return;
            }

            if (_drone == null)
                _drone = FindObjectOfType<IsoDroneController>();
            var inRange = IsDroneInScanRange();
            var eligible = hospitalEvent != null && hospitalEvent.CanPickupMedicine() &&
                           scanModeController != null && scanModeController.IsScanModeActive && inRange;
            proximityPromptRoot.SetActive(eligible);
        }
    }
}
