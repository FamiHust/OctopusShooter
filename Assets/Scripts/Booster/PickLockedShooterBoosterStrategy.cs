using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Booster 2: Cho phép gắp 1 shooter đang ở trạng thái Lock lên SlotBar.
/// Điều kiện: SlotBar chưa full VÀ có ít nhất 1 shooter đang Lock.
/// 
/// Flow:
///   Execute() → BoosterManager đặt mode = PickLockedShooter
///             → tất cả shooter Lock chạy anim "Booster"
///             → InputManager chỉ cho chọn các shooter Lock
///             → chọn 1 → nhảy lên SlotBar → booster kết thúc
///   Nếu nhấn nút booster lần 2 → Cancel()
/// </summary>
public class PickLockedShooterBoosterStrategy : IBoosterStrategy
{
    private readonly PickLockedShooterBoosterConfig cfg;
    private SlotBar _slotBar;
    private static readonly List<BaseShooter> shooterBuffer = new List<BaseShooter>(128);

    public PickLockedShooterBoosterStrategy(PickLockedShooterBoosterConfig config)
    {
        cfg = config;
    }

    public string BoosterName => cfg.boosterName;
    public Sprite Icon        => cfg.activeIcon;

    public void SetSlotBar(SlotBar slotBar) => _slotBar = slotBar;

    public bool CanUse()
    {
        if (_slotBar == null) return false;

        bool slotAvailable        = !_slotBar.IsFull();
        bool hasSelectableShooter = HasAnySelectableShooter();
        bool hasBooster           = BoosterManager.Instance != null
                                    && BoosterManager.Instance.HasBooster(cfg.boosterName);
        return slotAvailable && hasSelectableShooter && hasBooster;
    }

    public void Execute(System.Action onComplete, RectTransform buttonRect = null)
    {
        // BoosterManager handles the active-mode lifecycle; strategy only marks entry.
        // Tiêu tốn sẽ xảy ra khi player thực sự chọn xong (trong BoosterManager.OnLockedShooterPicked).
        BoosterManager.Instance?.EnterPickLockedShooterMode(cfg, onComplete);
    }

    // ─────────────────────────────────────────────
    private static bool HasAnySelectableShooter()
    {
        BaseShooter.FillRegisteredShooterBuffer(shooterBuffer, true);
        for (int i = 0; i < shooterBuffer.Count; i++)
        {
            BaseShooter s = shooterBuffer[i];
            if (s != null && s.IsSelectableForMoveShooter())
                return true;
        }
        return false;
    }
}
