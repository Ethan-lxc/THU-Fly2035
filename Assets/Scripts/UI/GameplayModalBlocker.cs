/// <summary>
/// 全屏对话、暂停等需要屏蔽无人机点击移动时的引用计数。
/// </summary>
public static class GameplayModalBlocker
{
    static int _count;

    public static void Push()
    {
        _count++;
    }

    public static void Pop()
    {
        if (_count > 0)
            _count--;
    }

    public static bool IsBlockingInput => _count > 0;
}
