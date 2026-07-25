using UnityEngine;

/// <summary>
/// ScriptableObject chứa toàn bộ config cho SplineRoute + SplineController.
/// Tạo asset: chuột phải trong Project → Create → FlowBlast → Spline Route Config
/// Gán vào field "Config" trên SplineRoute và SplineController để dùng chung.
/// </summary>
[CreateAssetMenu(menuName = "FlowBlast/Spline Route Config", fileName = "SplineRouteConfig")]
public class SplineRouteConfig : ScriptableObject
{
    [Header("Prefabs")]
    [Tooltip("Prefab BlockRow dùng để spawn các hàng block trên route.")]
    public GameObject blockRowPrefab;

    [Tooltip("Prefab block/seed dùng để fill vào BlockRow.")]
    public GameObject blockPrefab;

    [Header("Main Route — Movement")]
    [Tooltip("Tốc độ di chuyển của main conveyor (units/s).")]
    public float moveSpeed = 2f;

    [Tooltip("Khoảng cách tính từ spawnDistance; row vượt quá này sẽ bị despawn (Main mode).")]
    public float despawnDistance = 20f;

    [Header("Spacing")]
    [Tooltip("Tự đo chiều dài BlockRow prefab để tính khoảng cách. Bỏ tick nếu muốn set tay.")]
    public bool autoCalculateLength = true;

    [Tooltip("Main mode: phân bố đều theo splineLength thay vì nối sát.")]
    public bool distributeEvenly = true;

    [Tooltip("Side mode: khoảng cách cố định giữa 2 row liền kề (units).")]
    public float fixedSpacing = 2f;

    [Tooltip("Side mode: thời gian tween trượt xuống khi 1 row bị consume (giây).")]
    public float queueShiftDuration = 0.35f;

    [Header("Transfer Animation (SplineController)")]
    [Tooltip("Thời gian cơ bản để seed bay từ side về main (giây).")]
    public float transferDuration = 0.2f;

    [Tooltip("Độ cong của đường bay parabola. 0 = thẳng, 1 = cong nhiều.")]
    [Range(0f, 2f)]
    public float curveStrength = 0.6f;

    [Tooltip("Hạt cuối queue được boost để đến đích cùng lúc với hạt đầu.")]
    [Range(0f, 2f)]
    public float tailSeedSpeedBoost = 0.45f;

    [Tooltip("Hạt xa hơn được boost để đến đích cùng lúc (cân bằng khoảng cách).")]
    [Range(0f, 2f)]
    public float farDistanceSpeedBoost = 0.45f;

    [Tooltip("Refill transfer: xoay thêm quanh trục Y theo chiều kim đồng hồ (độ) trong lúc bay.")]
    [Range(0f, 360f)]
    public float refillSeedClockwiseYaw = 90f;
}
