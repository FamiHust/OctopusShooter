using UnityEngine;

/// <summary>
/// Config ScriptableObject cho booster "Gắp shooter bị lock lên SlotBar".
/// </summary>
[CreateAssetMenu(menuName = "FlowBlast/Booster/Pick Locked Shooter Booster", fileName = "PickLockedShooterBoosterConfig")]
public class PickLockedShooterBoosterConfig : BoosterStrategyConfig
{
    // Không cần tham số tuning đặc biệt: logic điều kiện nằm trong strategy.
    // Có thể thêm hiệu ứng highlight ở đây nếu muốn mở rộng.

    public override IBoosterStrategy CreateStrategy()
    {
        return new PickLockedShooterBoosterStrategy(this);
    }
}
