using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay.Events
{
    /// <summary>
    /// 超市校园卡：Q=直接搜索 / E=扫描 两条逻辑由同一拾取根上的 <see cref="SupermarketCampusCardPickup"/> 处理；本组件只记录路径并控制拾取根显隐。
    /// 挂在任务根（与 <see cref="HospitalFetchMedicineEvent"/> 同物体或子级均可），由 <see cref="SupermarketCampusCardNpcController"/> 设置路径。
    /// </summary>
    public sealed class SupermarketCampusCardFlowBinder : MonoBehaviour
    {
        public enum CampusCardPickupPath
        {
            None,
            DirectSearch,
            ScanMode
        }

        [Tooltip("可选；用于校验任务阶段")]
        [SerializeField] HospitalFetchMedicineEvent hospitalEvent;

        [Tooltip("挂 SupermarketCampusCardPickup 的校园卡拾取根；Q 与 E 共用")]
        [FormerlySerializedAs("directPickupRoot")]
        [SerializeField] GameObject campusCardPickupRoot;

        [Tooltip("旧版第二路拾取根；若 campusCardPickupRoot 为空则自动沿用此引用")]
        [FormerlySerializedAs("scanPickupRoot")]
        [SerializeField] GameObject _legacyScanPickupRoot;

        [SerializeField] CampusCardPickupPath _path = CampusCardPickupPath.None;

        public CampusCardPickupPath CurrentPath => _path;

        void Awake()
        {
            if (hospitalEvent == null)
                hospitalEvent = GetComponent<HospitalFetchMedicineEvent>() ?? FindObjectOfType<HospitalFetchMedicineEvent>();
            if (campusCardPickupRoot == null && _legacyScanPickupRoot != null)
                campusCardPickupRoot = _legacyScanPickupRoot;

            _path = CampusCardPickupPath.None;
            if (campusCardPickupRoot != null)
                campusCardPickupRoot.SetActive(false);
        }

        public void SetPickupPath(CampusCardPickupPath path)
        {
            _path = path;
            ApplyRoots();
        }

        void ApplyRoots()
        {
            if (campusCardPickupRoot == null)
                return;
            var on = _path == CampusCardPickupPath.DirectSearch || _path == CampusCardPickupPath.ScanMode;
            campusCardPickupRoot.SetActive(on);
        }

        /// <summary>与 <see cref="HospitalFetchMedicineEvent.CancelCompanionBranchFlowToInactive"/> 同步：清空路径并隐藏拾取。</summary>
        public void ResetToInactive()
        {
            SetPickupPath(CampusCardPickupPath.None);
        }
    }
}
