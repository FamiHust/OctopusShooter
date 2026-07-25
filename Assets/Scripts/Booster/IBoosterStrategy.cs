using UnityEngine;

/// <summary>
/// Interface chung cho tất cả Booster strategies
/// Mỗi loại booster implement interface này
/// </summary>
public interface IBoosterStrategy
{
    /// <summary>
    /// Tên hiển thị của booster
    /// </summary>
    string BoosterName { get; }

    /// <summary>
    /// Icon của booster (hiển thị trên UI)
    /// </summary>
    Sprite Icon { get; }

    /// <summary>
    /// Inject SlotBar reference — được gọi bởi BoosterManager khi OnSlotBarInit fires.
    /// </summary>
    void SetSlotBar(SlotBar slotBar);

    /// <summary>
    /// Kiểm tra có thể sử dụng booster lúc này không
    /// </summary>
    bool CanUse();

    /// <summary>
    /// Thực thi logic booster
    /// </summary>
    /// <param name="onComplete">Callback khi hoàn tất</param>
    /// <param name="buttonRect">RectTransform của button (cho animation)</param>
    void Execute(System.Action onComplete, RectTransform buttonRect = null);
}
