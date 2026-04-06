namespace Gameplay.Events
{
    /// <summary>医院任务等事件根用于切换 NPC 心情，避免强依赖 <see cref="QuestNpcInteractable"/> 类型。</summary>
    public interface IQuestNpcMood
    {
        void ApplyUnwellMood();

        void SetMoodHappy();
    }
}
