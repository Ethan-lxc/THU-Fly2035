/// <summary>
/// 由 IntroManager 在点击「开始」时置位；供游戏场景中的 MainGameplayBgmController 播放正式 Main BGM。
/// <see cref="PendingGameplayFadeIn"/>：进入游戏场景后做一次由暗变亮（见 <see cref="GameplaySceneEntranceFader"/>）。
/// </summary>
public static class GameplayBgmGate
{
    public static bool EnteredFromStartButton { get; set; }

    public static bool PendingGameplayFadeIn { get; set; }
}
