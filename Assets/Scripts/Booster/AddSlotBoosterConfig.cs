using UnityEngine;

/// <summary>
/// Config ScriptableObject cho booster "Thêm 1 slot vào SlotBar".
/// </summary>
[CreateAssetMenu(menuName = "FlowBlast/Booster/Add Slot Booster", fileName = "AddSlotBoosterConfig")]
public class AddSlotBoosterConfig : BoosterStrategyConfig
{
    [Header("Add Slot Settings")]
    [Tooltip("Số slot tối đa cho phép trong màn chơi")]
    public int maxSlots = 5;

    [Tooltip("Thời gian slot mới trồi lên (DOTween)")]
    public float riseDuration = 0.45f;

    [Tooltip("Thời gian các slot cũ trượt sang trái để cân chỉnh")]
    public float shiftDuration = 0.35f;

    [Tooltip("Ease khi slot mới trồi lên")]
    public DG.Tweening.Ease riseEase = DG.Tweening.Ease.OutBack;

    [Tooltip("Ease khi các slot cũ dịch chuyển")]
    public DG.Tweening.Ease shiftEase = DG.Tweening.Ease.OutCubic;

    public override IBoosterStrategy CreateStrategy()
    {
        return new AddSlotBoosterStrategy(this);
    }
}
