using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đặt trên trigger zone GameObject (side collider).
/// Khi một BlockRow của main spline đi qua, gọi SplineController.TriggerRefill()
/// để lấy seed từ side route tương ứng và transfer vào row trống.
///
/// Thay thế MainConveyorRefillCheckpoint — gán SplineController + sideIndex vào Inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SplineRouteRefillTrigger : MonoBehaviour
{
    [SerializeField] private SplineController splineController;
    [Tooltip("Index tương ứng với SplineController.sidesRoute[]")]
    [SerializeField] private int sideIndex = 0;
    [SerializeField] private float retriggerDelay = 0.2f;

    private readonly Dictionary<int, float> nextTriggerTime = new Dictionary<int, float>();

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void OnValidate()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;

        if (splineController == null)
        {
            Transform root = transform.root;
            if (root != null)
            {
                splineController = root.GetComponentInChildren<SplineController>(true);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (splineController == null || other == null) return;

        BlockRowSeedSpawner seeder = other.GetComponentInParent<BlockRowSeedSpawner>();
        if (seeder == null) return;

        SplineRoute ownerRoute = seeder.GetComponentInParent<SplineRoute>();
        if (ownerRoute == null || ownerRoute.GetRouteMode() != SplineRoute.RouteMode.Main)
        {
            return;
        }

        int   rowId = seeder.gameObject.GetInstanceID();
        float now   = Time.time;

        if (nextTriggerTime.TryGetValue(rowId, out float next) && now < next) return;

        splineController.TriggerRefill(sideIndex, seeder.gameObject);
        nextTriggerTime[rowId] = now + Mathf.Max(0.01f, retriggerDelay);
    }
}
