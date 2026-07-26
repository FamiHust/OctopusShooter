using UnityEngine;

/// <summary>
/// Base abstract class cho tất cả Booster Config
/// Mỗi booster type sẽ kế thừa class này và tự implement CreateStrategy()
/// </summary>
public abstract class BoosterStrategyConfig : ScriptableObject
{
    
    [Tooltip("Tên hiển thị")]
    public string boosterName;
    [Tooltip("Description")]
    public string description;
    
    [Tooltip("Icon hiển thị trên UI button")]
    public Sprite activeIcon;
    public Sprite inactiveIcon;
    
    [Header("Behavior Settings")]
    [Tooltip("Có lock input của player khi booster đang active không?")]
    public bool lockInputWhileActive = true;

    [Header("Two-Step Settings")]
    [Tooltip("True = booster cần player chọn target sau khi nhấn (ví dụ: Pick Locked Shooter).")]
    public bool isTwoStep = false;
    [Tooltip("Text hướng dẫn hiển thị trên instruction panel khi isTwoStep = true.")]
    public string instructionText = "";

    [Header("Shop Settings")]
    [Tooltip("Giá coin để mua booster này")]
    public int coinPrice = 100; // Giá mặc định    
    [Min(1)]
    [Tooltip("Số lượng booster nhận được mỗi lần mua qua coin")]
    public int purchaseAmount = 3;

    [Header("Unlock Settings")]
    [Min(1)]
    [Tooltip("Booster sẽ tự mở khóa khi player đạt level này")]
    public int unlockAtLevel = 1;

    [Header("Inventory (Prefab Source)")]
    [Min(0)]
    [Tooltip("Số lượng booster lấy trực tiếp từ prefab/config (không dùng PlayerData)")]
    public int initialCount = 0;

    /// <summary>
    /// Factory method: Tạo strategy instance tương ứng
    /// Mỗi subclass tự implement
    /// </summary>
    public abstract IBoosterStrategy CreateStrategy();
}
