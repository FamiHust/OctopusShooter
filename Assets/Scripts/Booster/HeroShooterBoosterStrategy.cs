using UnityEngine;
using System.Linq;

/// <summary>
/// Booster 3: Hero Shooter.
/// 2-step:
///   Step 1 — nhấn button.
///   Step 2 — chọn 1 shooter đang Idle trên SlotBar làm hero.
/// Hero bay lên trời, camera focus, tự bắn hết target rồi trở về (nếu còn đạn)
/// hoặc disappear bình thường (nếu hết đạn trong lúc bắn).
/// </summary>
public class HeroShooterBoosterStrategy : IBoosterStrategy
{
    private readonly HeroShooterBoosterConfig cfg;
    private SlotBar _slotBar;

    public HeroShooterBoosterStrategy(HeroShooterBoosterConfig config) => cfg = config;

    public string BoosterName => cfg.boosterName;
    public Sprite Icon        => cfg.activeIcon;

    public void SetSlotBar(SlotBar slotBar) => _slotBar = slotBar;

    public bool CanUse()
    {
        if (_slotBar == null) return false;

        // Check if there's any shooter in Idle state on the slot bar
        bool hasIdleShooter = _slotBar.GetAllShooters()
                              .Any(shooter => shooter != null && shooter.GetCurrentState() == ShooterState.Idle);
        bool hasBooster     = BoosterManager.Instance != null
                              && BoosterManager.Instance.HasBooster(cfg.boosterName);
        return hasIdleShooter && hasBooster;
    }

    public void Execute(System.Action onComplete, RectTransform buttonRect = null)
    {
        BoosterManager.Instance?.EnterHeroShooterMode(cfg, onComplete);
    }
}
