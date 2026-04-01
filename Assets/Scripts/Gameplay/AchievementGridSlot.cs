using UnityEngine;

/// <summary>
/// 成就台 4×4 网格中的单格：卡片实例挂在 <see cref="cardMount"/> 下；<see cref="IsOccupied"/> 根据子物体判断。
/// </summary>
public class AchievementGridSlot : MonoBehaviour
{
    [Tooltip("成就卡 prefab 的父节点，应铺满槽位")]
    public RectTransform cardMount;

    public bool IsOccupied => cardMount != null && cardMount.childCount > 0;
}
