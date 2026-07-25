using System.Collections.Generic;
using UnityEngine;

public class FireRangeDetector : MonoBehaviour
{
    // Dùng Singleton để các Shooter dễ dàng lấy dữ liệu mà không cần FindObject
    public static FireRangeDetector Instance { get; private set; }
    public LayerMask detectLayer;

    [Header("Performance")]
    [SerializeField, Min(0.02f)] private float rebuildInterval = 0.08f;
    [SerializeField, Min(0.02f)] private float idleRebuildInterval = 0.16f;
    [SerializeField, Range(0.75f, 1f)] private float rebuildIntervalScale = 0.9f;
    [SerializeField, Min(16)] private int overlapBufferSize = 128;

    // Danh sách các mục tiêu ĐANG NẰM TRONG vùng bắn
    public List<BlockRowSeedSpawner> targetsInRange = new List<BlockRowSeedSpawner>();
    private readonly HashSet<BlockRowSeedSpawner> targetsInRangeSet = new HashSet<BlockRowSeedSpawner>();

    private BoxCollider detectorBox;
    private Collider[] overlapResults;
    private readonly HashSet<BlockRowSeedSpawner> snapshotBuffer = new HashSet<BlockRowSeedSpawner>();
    private readonly Dictionary<BlockRowSeedSpawner, Collider> rowColliderCache = new Dictionary<BlockRowSeedSpawner, Collider>();
    private readonly Dictionary<Collider, BlockRowSeedSpawner> hitSpawnerCache = new Dictionary<Collider, BlockRowSeedSpawner>();
    private float nextRebuildTime;
    private bool forceRebuildPending;
    private int targetsStateVersion;
    private int lastTargetsHash;

    public int TargetsStateVersion => targetsStateVersion;

    private void Awake()
    {
        // On level restart, old level objects are destroyed at end-of-frame.
        // Always prefer the newest detector instance so shooters can keep targeting.
        Instance = this;
        detectorBox = GetComponent<BoxCollider>();

        if (detectLayer.value == 0)
        {
            detectLayer = ~0;
        }

        overlapResults = new Collider[Mathf.Max(16, overlapBufferSize)];
        forceRebuildPending = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        targetsInRange.Clear();
        targetsInRangeSet.Clear();
        snapshotBuffer.Clear();
        rowColliderCache.Clear();
        hitSpawnerCache.Clear();
    }

    private void OnValidate()
    {
        if (detectLayer.value == 0)
        {
            detectLayer = ~0;
        }

        rebuildInterval = Mathf.Max(0.02f, rebuildInterval);
        idleRebuildInterval = Mathf.Max(rebuildInterval, idleRebuildInterval);
        rebuildIntervalScale = Mathf.Clamp(rebuildIntervalScale, 0.75f, 1f);
        overlapBufferSize = Mathf.Max(16, overlapBufferSize);
    }

    private void Start()
    {
        if (detectorBox == null)
        {
            ;
            return;
        }

        RebuildTargetsInRange();
        nextRebuildTime = Time.time + GetEffectiveActiveRebuildInterval();
    }

    private void FixedUpdate()
    {
        if (detectorBox == null)
        {
            return;
        }

        if (!forceRebuildPending && Time.time < nextRebuildTime)
        {
            return;
        }

        forceRebuildPending = false;
        float activeInterval = targetsInRange.Count > 0
            ? GetEffectiveActiveRebuildInterval()
            : Mathf.Max(GetEffectiveActiveRebuildInterval(), GetEffectiveIdleRebuildInterval());
        nextRebuildTime = Time.time + activeInterval;
        RebuildTargetsInRange();
    }

    private float GetEffectiveActiveRebuildInterval()
    {
        return Mathf.Max(0.02f, rebuildInterval * rebuildIntervalScale);
    }

    private float GetEffectiveIdleRebuildInterval()
    {
        return Mathf.Max(0.02f, idleRebuildInterval * rebuildIntervalScale);
    }

    private void RebuildTargetsInRange()
    {
        // Tái đồng bộ theo va chạm THẬT giữa collider của row và FireRangeDetector.
        // Không dùng seed collider làm điều kiện xử lý.
        Vector3 worldCenter = transform.TransformPoint(detectorBox.center);
        Vector3 worldHalfExtents = Vector3.Scale(detectorBox.size, transform.lossyScale) * 0.5f;
        int hitCount = Physics.OverlapBoxNonAlloc(
            worldCenter,
            worldHalfExtents,
            overlapResults,
            transform.rotation,
            detectLayer.value,
            QueryTriggerInteraction.Collide);

        if (hitCount >= overlapResults.Length)
        {
            System.Array.Resize(ref overlapResults, overlapResults.Length * 2);
            hitCount = Physics.OverlapBoxNonAlloc(
                worldCenter,
                worldHalfExtents,
                overlapResults,
                transform.rotation,
                detectLayer.value,
                QueryTriggerInteraction.Collide);
        }

        snapshotBuffer.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            if (hit == null)
            {
                continue;
            }

            BlockRowSeedSpawner spawner = ResolveSpawnerFromHit(hit);
            if (IsSpawnerAlive(spawner) && IsRowColliderTouchingDetector(spawner))
            {
                snapshotBuffer.Add(spawner);
            }
        }

        for (int i = targetsInRange.Count - 1; i >= 0; i--)
        {
            BlockRowSeedSpawner existingTarget = targetsInRange[i];
            if (existingTarget == null || !snapshotBuffer.Contains(existingTarget) || !IsSpawnerAlive(existingTarget))
            {
                targetsInRange.RemoveAt(i);
                targetsInRangeSet.Remove(existingTarget);
                if (existingTarget != null)
                {
                    rowColliderCache.Remove(existingTarget);
                }
            }
        }

        foreach (BlockRowSeedSpawner spawner in snapshotBuffer)
        {
            if (targetsInRangeSet.Add(spawner))
            {
                targetsInRange.Add(spawner);
            }
        }

        SortTargets();
        UpdateTargetsStateVersion();
    }

    private void UpdateTargetsStateVersion()
    {
        int hash = 17;
        hash = (hash * 31) + targetsInRange.Count;

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            BlockRowSeedSpawner target = targetsInRange[i];
            int id = target != null ? target.GetInstanceID() : 0;
            hash = (hash * 31) + id;
        }

        if (hash == lastTargetsHash)
        {
            return;
        }

        lastTargetsHash = hash;
        targetsStateVersion++;
    }

    private void SortTargets()
    {
        // Sắp xếp theo x tăng dần (x nhỏ nhất = gần nhất theo hướng -x, ưu tiên bắn trước)
        RemoveInvalidTargets();

        if (targetsInRange.Count <= 1)
        {
            return;
        }

        targetsInRange.Sort((a, b) =>
        {
            bool aAlive = IsSpawnerAlive(a);
            bool bAlive = IsSpawnerAlive(b);

            if (!aAlive && !bAlive) return 0;
            if (!aAlive) return 1;
            if (!bAlive) return -1;

            return a.transform.position.x.CompareTo(b.transform.position.x);
        });
    }

    private void OnTriggerEnter(Collider other)
    {
        forceRebuildPending = true;
    }

    private void OnTriggerExit(Collider other)
    {
        forceRebuildPending = true;
    }

    public void SyncNow()
    {
        forceRebuildPending = true;
    }

    public List<BlockRowSeedSpawner> GetTargetsInRangeSnapshot()
    {
        return new List<BlockRowSeedSpawner>(targetsInRange);
    }

    public IReadOnlyList<BlockRowSeedSpawner> GetTargetsInRangeView()
    {
        return targetsInRange;
    }

    private void RemoveInvalidTargets()
    {
        for (int i = targetsInRange.Count - 1; i >= 0; i--)
        {
            BlockRowSeedSpawner target = targetsInRange[i];
            if (IsSpawnerAlive(target))
            {
                continue;
            }

            targetsInRange.RemoveAt(i);
            targetsInRangeSet.Remove(target);
            if (target != null)
            {
                rowColliderCache.Remove(target);
            }
        }
    }

    public bool IsTargetInRange(BlockRowSeedSpawner target)
    {
        if (!IsSpawnerAlive(target))
        {
            return false;
        }

        return targetsInRangeSet.Contains(target);
    }

    public Vector3 ClampPointToRange(Vector3 worldPoint)
    {
        if (detectorBox == null)
        {
            return worldPoint;
        }

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 min = detectorBox.center - (detectorBox.size * 0.5f);
        Vector3 max = detectorBox.center + (detectorBox.size * 0.5f);

        localPoint.x = Mathf.Clamp(localPoint.x, min.x, max.x);
        localPoint.y = Mathf.Clamp(localPoint.y, min.y, max.y);
        localPoint.z = Mathf.Clamp(localPoint.z, min.z, max.z);

        return transform.TransformPoint(localPoint);
    }

    private bool IsSpawnerAlive(BlockRowSeedSpawner spawner)
    {
        return spawner != null && spawner.gameObject != null;
    }

    private bool IsRowColliderTouchingDetector(BlockRowSeedSpawner spawner)
    {
        if (!IsSpawnerAlive(spawner) || detectorBox == null)
        {
            return false;
        }

        Collider rowCollider = GetCachedRowCollider(spawner);
        if (rowCollider == null || !rowCollider.enabled || !detectorBox.enabled)
        {
            return false;
        }

        return detectorBox.bounds.Intersects(rowCollider.bounds);
    }

    private Collider GetCachedRowCollider(BlockRowSeedSpawner spawner)
    {
        if (!IsSpawnerAlive(spawner))
        {
            return null;
        }

        if (rowColliderCache.TryGetValue(spawner, out Collider cached) && cached != null)
        {
            return cached;
        }

        Collider rowCollider = spawner.GetComponent<Collider>();
        if (rowCollider != null)
        {
            rowColliderCache[spawner] = rowCollider;
        }

        return rowCollider;
    }

    private BlockRowSeedSpawner ResolveSpawnerFromHit(Collider hit)
    {
        if (hit == null)
        {
            return null;
        }

        if (hitSpawnerCache.TryGetValue(hit, out BlockRowSeedSpawner cachedSpawner))
        {
            if (IsSpawnerAlive(cachedSpawner))
            {
                return cachedSpawner;
            }

            hitSpawnerCache.Remove(hit);
        }

        BlockRowSeedSpawner resolvedSpawner = hit.GetComponentInParent<BlockRowSeedSpawner>();
        if (resolvedSpawner != null)
        {
            hitSpawnerCache[hit] = resolvedSpawner;
        }

        return resolvedSpawner;
    }
}
