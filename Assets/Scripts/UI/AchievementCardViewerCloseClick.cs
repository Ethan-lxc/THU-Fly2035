using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>点击关闭欣赏层（挂在遮罩与大图上）。</summary>
[DisallowMultipleComponent]
public class AchievementCardViewerCloseClick : MonoBehaviour, IPointerClickHandler
{
    public AchievementCardViewerOverlay Owner;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Application.isPlaying)
            return;
        if (Owner != null && Owner.suppressUserDismiss)
            return;
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        Owner?.PlayPreviewCloseClickSound();
        Owner?.Hide();
    }
}
