/// <summary>
/// Định nghĩa tất cả các event type trong game
/// </summary>
public enum GameEventType
{
    // Shooter events
    OnShooterJumpStart,        // Khi shooter bắt đầu nhảy vào slotbar
    OnBulletCountChanged,      // Khi số đạn thay đổi
    OnShooterSelected,         // Khi shooter được chọn
    OnShooterDisappear,        // Khi shooter biến mất
    OnPortalShooterSwapRequest,// PortalShooter yêu cầu trigger side-route swap sau khi lên deck xong
    
    // GridSystem events
    OnGridControllerInit,
    OnGridItemRemoved,         // Khi vật phẩm được xóa khỏi grid
    OnGridItemAdded,           // Khi vật phẩm được thêm vào grid
    
    // SlotBar events
    OnSlotBarInit,
    OnSlotBarFull,             // Khi slotbar đầy
    OnShooterAddedToSlot,      // Khi thêm shooter vào slot
    
    // Seed events
    OnSeedDestroyed,           // Khi 1 seed được xử lý phá hủy (data = int count)
    OnSeedRowDestroyed,        // Khi 1 hàng seed bị phá hủy hoàn toàn (data = int seedCount)
    OnMagicStoneProgressChanged, // Progress magic stone trong màn (data = int collected)

    // Booster events
    OnBoosterActivated,        // Khi booster được kích hoạt   (data = BoosterManager.ActiveBoosterMode)
    OnBoosterDeactivated,      // Khi booster bị huỷ / hoàn tất
    OnBoosterButtonRefresh,    // Yêu cầu tất cả BoosterButtonPrefab tự cập nhật trạng thái

    // Game state events
    OnGameStart,               // Khi game bắt đầu
    OnGameEnd,                 // Khi game kết thúc
    OnGamePause,               // Khi game tạm dừng
    OnGameWin,                 // Khi thắng
    OnGameLose,                // Khi thua
}
