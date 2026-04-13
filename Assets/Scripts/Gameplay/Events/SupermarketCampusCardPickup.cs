using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.Events
{
    /// <summary>
    /// 超市校园卡：Q=直接搜索（E 拾取）与 E=扫描（提示音 + Space 拾取）共挂一个脚本；
    /// 按 <see cref="SupermarketCampusCardFlowBinder.CurrentPath"/> 切换行为。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class SupermarketCampusCardPickup : MonoBehaviour, IWorldInteractable, IWorldInteractableResolvePriority
    {
        [SerializeField] HospitalFetchMedicineEvent hospitalEvent;

        [Tooltip("未指定则按 Pickup Collider Root 层级自动匹配 HospitalFetchMedicineEvent（与医院取药点规则一致）")]
        [SerializeField] SupermarketCampusCardFlowBinder pickupFlowBinder;

        [Tooltip("空则 FindObjectOfType（超市场景内需有独立实例）")]
        [SerializeField] SupermarketScanModeController scanModeController;

        [Header("Q 分支 · 直接搜索")]
        [Tooltip("按 E 成功拾取时播放（2D 一次性）")]
        [SerializeField] AudioClip pickupInteractSfx;

        [Range(0f, 1f)]
        [SerializeField] float pickupInteractSfxVolume = 1f;

        [Header("E 分支 · 扫描模式")]
        [Min(0.1f)]
        [SerializeField] float scanRadius = 6f;

        [Tooltip("显形前隐藏的根（子物体 Sprite 等）；仅扫描路径生效")]
        [SerializeField] GameObject visualRoot;

        [Tooltip("进入扫描范围且扫描模式开启时循环播放")]
        [SerializeField] AudioClip proximityBeepClip;

        [SerializeField] [Range(0f, 1f)] float beepVolume = 0.6f;

        [SerializeField] KeyCode pickupKey = KeyCode.Space;

        [Tooltip("为 true 时扫描路径下不响应世界互动 E，避免与 Space 重复")]
        [SerializeField] bool pickupUsesSpaceKeyOnly = true;

        [Header("共用")]
        [Tooltip("靠近且可互动时在 Scene 中高亮用的提示根，可选")]
        [SerializeField] GameObject proximityPromptRoot;

        AudioSource _audio;
        IsoDroneController _drone;

        void ResolveHospitalEvent()
        {
            if (hospitalEvent != null)
                return;
            hospitalEvent = HospitalFetchMedicineEvent.FindBestForPickupTransform(transform);
        }

        public int WorldInteractResolvePriority
        {
            get
            {
                ResolveHospitalEvent();
                if (hospitalEvent == null || !hospitalEvent.CanPickupMedicine())
                    return 0;
                if (IsDirectPath())
                    return 200;
                if (IsScanPath())
                {
                    if (scanModeController == null)
                        scanModeController = FindObjectOfType<SupermarketScanModeController>(true);
                    if (scanModeController != null && scanModeController.IsScanModeActive &&
                        IsDroneInScanRange())
                        return 200;
                }
                return 0;
            }
        }

        void Awake()
        {
            var c = GetComponent<Collider2D>();
            if (c != null)
                c.isTrigger = true;

            if (proximityPromptRoot != null)
                proximityPromptRoot.SetActive(false);

            ResolveHospitalEvent();
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

        void OnEnable()
        {
            ResolveHospitalEvent();
            if (visualRoot == null)
                return;
            if (IsDirectPath())
                visualRoot.SetActive(true);
            else if (IsScanPath())
                visualRoot.SetActive(false);
        }

        void Update()
        {
            ResolveHospitalEvent();
            if (!IsScanPath())
            {
                if (_audio != null && _audio.isPlaying)
                    _audio.Stop();
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

            if (pickupUsesSpaceKeyOnly && CanPickupWithSpace(inRange) &&
                (Input.GetKeyDown(pickupKey) || Input.GetKeyDown(KeyCode.E)))
                ExecutePickup();
        }

        bool IsDirectPath()
        {
            if (pickupFlowBinder == null)
                return false;
            return pickupFlowBinder.CurrentPath == SupermarketCampusCardFlowBinder.CampusCardPickupPath.DirectSearch;
        }

        bool IsScanPath()
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
            ResolveHospitalEvent();
            if (!IsDirectPath())
                return false;
            if (hospitalEvent == null || !hospitalEvent.CanPickupMedicine())
                return false;
            return true;
        }

        public void BeginInteract(Transform interactor)
        {
            ResolveHospitalEvent();
            if (!IsDirectPath())
                return;
            if (hospitalEvent == null || !hospitalEvent.CanPickupMedicine())
                return;
            if (pickupInteractSfx != null)
                NpcEventOutcomeAudio.PlayClip2D(pickupInteractSfx, pickupInteractSfxVolume);
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

            ResolveHospitalEvent();

            if (IsDirectPath())
            {
                var ok = hospitalEvent != null && hospitalEvent.CanPickupMedicine();
                proximityPromptRoot.SetActive(highlighted && ok);
                return;
            }

            if (IsScanPath())
            {
                if (!highlighted)
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
                return;
            }

            proximityPromptRoot.SetActive(false);
        }
    }
}
