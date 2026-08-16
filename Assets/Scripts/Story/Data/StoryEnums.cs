using UnityEngine;

/// <summary>
/// Phân loại ngữ cảnh của từng Story/Comic trong game (Intro, Blocker mới, Booster mới...)
/// </summary>
public enum StoryType
{
    [InspectorName("0. None (Không phát Story)")]
    None = -1,

    [InspectorName("1. Intro (Mở đầu cốt truyện / Đầu game)")]
    Intro = 0,

    [InspectorName("2. Complete Level 1 (Hoàn thành Level 1)")]
    Complete = 1,

    // Blockers
    [InspectorName("3. Blocker - Hidden (Shooter ẩn/hộp bí ẩn)")]
    Blocker_Hidden = 10,

    [InspectorName("4. Blocker - Ice (Băng phong tỏa)")]
    Blocker_Ice = 11,

    [InspectorName("5. Blocker - Portal (Cổng dịch chuyển)")]
    Blocker_Portal = 12,

    [InspectorName("6. Blocker - Tunnel (Đường hầm)")]
    Blocker_Tunnel = 13,

    // Boosters
    [InspectorName("7. Booster - Add Slot (Mở rộng thêm ô chờ)")]
    Booster_AddSlot = 20,

    [InspectorName("8. Booster - Move Shooter (Di chuyển Shooter bị khóa)")]
    Booster_MoveShooter = 21,

    [InspectorName("9. Booster - Super Shooter (Shooter bắn giải cứu đặc biệt)")]
    Booster_SuperShooter = 22,

    [InspectorName("10. Booster - Magic Stone (Đá ma thuật)")]
    Booster_MagicStone = 23,

    // Khác
    [InspectorName("11. Outro (Kết thúc / Chiến thắng)")]
    Outro = 90,

    [InspectorName("12. Custom Story")]
    Custom = 100
}

/// <summary>
/// Các hiệu ứng xuất hiện cho từng ô truyện tranh
/// </summary>
public enum PanelAnimationEffect
{
    [InspectorName("1. Fade Only (Mờ dần thành rõ)")]
    FadeOnly,

    [InspectorName("2. Pop & Scale (Nảy phóng to)")]
    PopScale,

    [InspectorName("3. Zoom In (Phóng to từ tâm)")]
    ZoomIn,

    [InspectorName("4. Slide From Left (Trượt từ trái sang)")]
    SlideFromLeft,

    [InspectorName("5. Slide From Right (Trượt từ phải sang)")]
    SlideFromRight,

    [InspectorName("6. Slide From Bottom (Trượt từ dưới lên)")]
    SlideFromBottom,

    [InspectorName("7. Slide From Top (Trượt từ trên xuống)")]
    SlideFromTop,

    [InspectorName("8. Flip Horizontal (Lật mở 3D)")]
    FlipHorizontal,

    [InspectorName("9. Punch Shake (Rung nảy mạnh mẽ)")]
    PunchShake
}
