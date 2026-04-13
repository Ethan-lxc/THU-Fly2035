using UnityEngine;

/// <summary>
/// 提供卡面「全屏欣赏」能力；成就台与背包等 UI 共用 <see cref="AchievementCardClickZoom"/>。
/// </summary>
public interface ICardPreviewSource
{
    void ShowAchievementCardPreview(Sprite sprite, string title);
}
