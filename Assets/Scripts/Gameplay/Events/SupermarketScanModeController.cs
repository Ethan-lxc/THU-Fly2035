using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>
    /// 超市场景：专业度达阈值后可切换「扫描模式」，供 <see cref="HiddenPickupInScanMode"/> 等查询。
    /// 建议挂在超市场景内独立空物体（无人机为 DDOL 时不宜仅依赖场景内引用）。
    /// </summary>
    public sealed class SupermarketScanModeController : MonoBehaviour
    {
        public static SupermarketScanModeController Instance { get; private set; }

        [Tooltip("空则 Awake 时 FindObjectOfType")]
        [SerializeField] PlayerStatsHud statsHud;

        [Tooltip("低于此专业度无法保持/开启扫描模式")]
        [SerializeField] float minProfessionalismToUnlock = 5f;

        [SerializeField] KeyCode toggleScanKey = KeyCode.R;

        bool _scanModeActive;

        /// <summary>与 Inspector 中「最低专业度」一致，供 UI/NPC 显示或判定。</summary>
        public float MinProfessionalismToUnlock => minProfessionalismToUnlock;

        void Awake()
        {
            Instance = this;
            if (statsHud == null)
                statsHud = FindObjectOfType<PlayerStatsHud>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (statsHud == null)
                statsHud = FindObjectOfType<PlayerStatsHud>();

            if (statsHud != null && statsHud.Professionalism < minProfessionalismToUnlock)
                _scanModeActive = false;

            if (Input.GetKeyDown(toggleScanKey))
            {
                if (statsHud == null)
                    return;
                if (statsHud.Professionalism < minProfessionalismToUnlock)
                    return;
                _scanModeActive = !_scanModeActive;
            }
        }

        /// <summary>专业度足够且玩家已开启扫描模式。</summary>
        public bool IsScanModeActive =>
            _scanModeActive && statsHud != null && statsHud.Professionalism >= minProfessionalismToUnlock;

        /// <summary>
        /// 由超市校园卡对白等任务入口尝试开启扫描（不切换 R 键逻辑）。
        /// 专业度不足时返回 false，且不会打开扫描。
        /// </summary>
        public bool TryEnableScanModeFromQuest(out string failReason)
        {
            failReason = null;
            if (statsHud == null)
                statsHud = FindObjectOfType<PlayerStatsHud>();
            if (statsHud == null)
            {
                failReason = "未找到 PlayerStatsHud";
                return false;
            }

            if (statsHud.Professionalism < minProfessionalismToUnlock)
            {
                failReason = "professionalism_too_low";
                return false;
            }

            _scanModeActive = true;
            return true;
        }
    }
}
