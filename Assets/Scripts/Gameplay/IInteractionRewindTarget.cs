/// <summary>
/// 分支对白 NPC 等在失败回退时需恢复到「可再次互动」状态；与 <see cref="GameplayInteractionCheckpoint"/> 配合。
/// </summary>
public interface IInteractionRewindTarget
{
    void ResetToPreInteractState();
}
