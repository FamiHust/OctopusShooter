using UnityEngine;

/// <summary>
/// Booster 1: Thêm 1 slot vào SlotBar.
/// Điều kiện: số slot hiện tại < maxSlots VÀ player có ít nhất 1 lượt trong inventory.
/// </summary>
public class AddSlotBoosterStrategy : IBoosterStrategy
{
    private readonly AddSlotBoosterConfig cfg;
    private SlotBar _slotBar;

    public AddSlotBoosterStrategy(AddSlotBoosterConfig config)
    {
        cfg = config;
    }

    public string BoosterName => cfg.boosterName;
    public Sprite Icon        => cfg.activeIcon;

    public void SetSlotBar(SlotBar slotBar) => _slotBar = slotBar;

    public bool CanUse()
    {
        if (_slotBar == null) return false;

        bool notMaxed   = _slotBar.GetSlotCount() < cfg.maxSlots;
        bool hasBooster = BoosterManager.Instance != null
                          && BoosterManager.Instance.HasBooster(cfg.boosterName);
        return notMaxed && hasBooster;
    }

    public void Execute(System.Action onComplete, RectTransform buttonRect = null)
    {
        if (_slotBar == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (BoosterManager.Instance == null || !BoosterManager.Instance.TryConsumeBooster(cfg.boosterName, 1))
        {
            onComplete?.Invoke();
            return;
        }

        _slotBar.AddSlotWithAnimation(cfg, onComplete);

        GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, null);
    }
}
