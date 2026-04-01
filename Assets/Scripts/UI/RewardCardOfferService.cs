using UnityEngine;

/// <summary>
/// 全项目共用的奖励卡入口：任意小事件只调 <see cref="Offer"/>，不直接挂 <see cref="RewardCardRevealPopup"/>。
/// 建议挂在场景里 <c>GameplayHUD</c> 根物体（与 <see cref="GameplayHudLayout"/> 同节点），并在布局里引用一次。
/// </summary>
[DisallowMultipleComponent]
public class RewardCardOfferService : MonoBehaviour
{
    [Tooltip("空则 Awake 时在本物体上找，再 FindObjectOfType")]
    public RewardCardRevealPopup popup;

    void Awake()
    {
        ResolvePopup();
    }

    void ResolvePopup()
    {
        if (popup != null)
            return;
        popup = GetComponent<RewardCardRevealPopup>()
                ?? FindObjectOfType<RewardCardRevealPopup>(true);
    }

    /// <param name="storagePrefsKey">入库去重用，建议唯一，如 SickClassmate_MedicineCardStored。</param>
    /// <param name="persistPlayerPrefs">为 false 时不写入去重键，仍可当场往成就台加卡（与 NPC 调试勾选配合）。</param>
    public void Offer(Sprite cardSprite, string cardTitle, string storagePrefsKey, bool persistPlayerPrefs = true)
    {
        if (cardSprite == null || string.IsNullOrEmpty(storagePrefsKey))
            return;

        ResolvePopup();
        if (popup == null)
            return;

        popup.Show(cardSprite, cardTitle ?? string.Empty, storagePrefsKey, persistPlayerPrefs);
    }
}
