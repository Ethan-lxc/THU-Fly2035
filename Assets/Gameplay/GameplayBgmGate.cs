/// <summary>
/// 由 IntroManager 在点击「开始」时置位；供游戏场景中的 MainGameplayBgmController 播放正式 Main BGM。
/// </summary>
public static class GameplayBgmGate
{
    public static bool EnteredFromStartButton { get; set; }
}
