using UnityEngine;

/// <summary>
/// Config cho booster Hero Shooter.
/// 2-step: nhấn button → chọn 1 shooter đang Idle trên SlotBar → nó bay lên,
/// camera focus, tự động bắn các block row theo rule route (main/side),
/// rồi trở về slot (nếu còn đạn) hoặc disappear bình thường (hết đạn).
/// </summary>
[CreateAssetMenu(fileName = "HeroShooterBoosterConfig",
                 menuName  = "Booster/Hero Shooter Config",
                 order     = 3)]
public class HeroShooterBoosterConfig : BoosterStrategyConfig
{
    [Header("Hero Shooter Settings")]

    [Tooltip("Độ cao bay lên khỏi vị trí slot (world units)")]
    public float flyHeight = 8f;

    [Tooltip("Thời gian animation bay lên / xuống")]
    public float flyDuration = 0.55f;

    [Tooltip("Thời gian camera pan + zoom đến hero")]
    public float cameraFocusDuration = 0.8f;

    [Tooltip("Offset local Y khi camera focus vào hero. Giảm giá trị này nếu camera bị đẩy lên quá cao.")]
    public float cameraFocusOffsetY = 1.2f;

    [Tooltip("Orthographic size khi zoom vào hero (0 = không đổi size)")]
    public float cameraZoomSize = 4f;

    [Tooltip("Thời gian camera trở về vị trí gốc sau khi hero xong")]
    public float cameraReturnDuration = 0.5f;

    public override IBoosterStrategy CreateStrategy() => new HeroShooterBoosterStrategy(this);
}
