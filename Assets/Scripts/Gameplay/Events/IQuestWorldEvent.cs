using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>
    /// 与 <see cref="QuestNpcInteractable"/> 配对的世界任务入口（如 <see cref="HospitalFetchMedicineEvent"/>）。
    /// 参数使用 <see cref="MonoBehaviour"/> 以避免与 <see cref="QuestNpcInteractable"/> 循环引用导致编译失败。
    /// </summary>
    public interface IQuestWorldEvent
    {
        bool CanInteractNpc(MonoBehaviour npc);

        void OnNpcInteract(MonoBehaviour npc);
    }
}
