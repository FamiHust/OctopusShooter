using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Äiá»u phá»‘i toÃ n bá»™ spline system:
///   - 1 main route (loop di chuyá»ƒn) + N side routes (queue tÄ©nh)
///   - Slice List&lt;SeedColor&gt; tá»« LevelController vÃ  phÃ¢n phá»‘i cho tá»«ng route
///   - Quáº£n lÃ½ refill logic: khi main row trá»‘ng cháº¡y qua side collider â†’ transfer animation tá»« side â†’ main
/// </summary>
public class SplineController : MonoBehaviour
{
    public static SplineController Instance { get; private set; }
    public event System.Action<SplineRoute> OnRefillCompleted;

    [Header("Config (tÃ¹y chá»n â€” dÃ¹ng chung giá»¯a cÃ¡c prefab)")]
    [Tooltip("GÃ¡n SplineRouteConfig asset Ä‘á»ƒ Ä‘á»c Transfer Animation settings tá»« Ä‘Ã³. Náº¿u Ä‘á»ƒ trá»‘ng dÃ¹ng giÃ¡ trá»‹ inline bÃªn dÆ°á»›i.")]
    [SerializeField] private SplineRouteConfig config;

    [Header("Routes")]
    [SerializeField] private SplineRoute mainRoute;
    [SerializeField] private SplineRoute[] sidesRoute;

    [Header("Side Colliders")]
    [Tooltip("Má»—i element tÆ°Æ¡ng á»©ng vá»›i sidesRoute cÃ¹ng index. Chá»©a SplineRouteRefillTrigger.")]
    [SerializeField] private Collider[] sideColliders;

    [Header("Transfer Animation â€” dÃ¹ng khi khÃ´ng cÃ³ Config")]
    [SerializeField] private float transferDuration      = 0.2f;
    [SerializeField] private float curveStrength         = 0.6f;
    [SerializeField] private float tailSeedSpeedBoost    = 0.45f;
    [SerializeField] private float farDistanceSpeedBoost = 0.45f;
    [SerializeField] private float refillSeedClockwiseYaw = 90f;

    // â”€â”€ Config helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private float TransferDuration      => config != null ? config.transferDuration      : transferDuration;
    private float CurveStrength         => config != null ? config.curveStrength         : curveStrength;
    private float TailSeedSpeedBoost    => config != null ? config.tailSeedSpeedBoost    : tailSeedSpeedBoost;
    private float FarDistanceSpeedBoost => config != null ? config.farDistanceSpeedBoost : farDistanceSpeedBoost;
    private float RefillSeedClockwiseYaw => config != null ? config.refillSeedClockwiseYaw : refillSeedClockwiseYaw;

    private readonly HashSet<int> processingRowIds = new HashSet<int>();
    private readonly HashSet<SplineRoute> processingSources = new HashSet<SplineRoute>();
    private readonly Dictionary<SplineRoute, Queue<GameObject>> pendingRowsBySource = new Dictionary<SplineRoute, Queue<GameObject>>();
    private readonly Dictionary<SplineRoute, HashSet<int>> pendingRowIdsBySource = new Dictionary<SplineRoute, HashSet<int>>();
    private readonly Stack<RefillTransferBuffers> refillBufferPool = new Stack<RefillTransferBuffers>(4);

    private sealed class RefillTransferBuffers
    {
        public readonly SeedColor[] colors = new SeedColor[5];
        public readonly Vector3[] consumedPositions = new Vector3[5];
        public readonly GameObject[] targetSeeds = new GameObject[5];
        public readonly Vector3[] startPositions = new Vector3[5];
        public readonly Tween[] tweens = new Tween[5];
        public readonly float[] distances = new float[5];
        public readonly float[] durations = new float[5];

        public void Reset()
        {
            for (int i = 0; i < 5; i++)
            {
                targetSeeds[i] = null;
                tweens[i] = null;
                distances[i] = 0f;
                durations[i] = 0f;
            }
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        TryAutoAssignDistributeEvenConfig();
        TryAutoAssignMainRoute();
        TryAutoAssignSideRoutes();
    }

    private void TryAutoAssignDistributeEvenConfig()
    {
#if UNITY_EDITOR
        const string targetConfigName = "SplineRouteConfigDistributeEven";

        if (config != null && string.Equals(config.name, targetConfigName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets($"{targetConfigName} t:SplineRouteConfig");
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        SplineRouteConfig found = AssetDatabase.LoadAssetAtPath<SplineRouteConfig>(path);
        if (found == null)
        {
            return;
        }

        config = found;
        EditorUtility.SetDirty(this);
#endif
    }

    private void TryAutoAssignMainRoute()
    {
        Transform searchRoot = transform.root != null ? transform.root : transform;
        SplineRoute[] routes = searchRoot.GetComponentsInChildren<SplineRoute>(true);
        if (routes == null || routes.Length == 0)
        {
            return;
        }

        for (int i = 0; i < routes.Length; i++)
        {
            SplineRoute route = routes[i];
            if (route == null)
            {
                continue;
            }

            if (route.GetRouteMode() != SplineRoute.RouteMode.Main)
            {
                continue;
            }

            mainRoute = route;
            return;
        }
    }

    private void TryAutoAssignSideRoutes()
    {
        Transform searchRoot = transform.root != null ? transform.root : transform;
        SplineRoute[] routes = searchRoot.GetComponentsInChildren<SplineRoute>(true);
        if (routes == null || routes.Length == 0)
        {
            sidesRoute = new SplineRoute[0];
            return;
        }

        SplineRoute wayNoL = null;
        SplineRoute wayWithL = null;
        List<SplineRoute> others = new List<SplineRoute>();

        for (int i = 0; i < routes.Length; i++)
        {
            SplineRoute route = routes[i];
            if (route == null || route.GetRouteMode() != SplineRoute.RouteMode.Side)
            {
                continue;
            }

            string routeName = route.gameObject.name;
            if (IsWayWithoutL(routeName))
            {
                if (wayNoL == null)
                {
                    wayNoL = route;
                    continue;
                }
            }
            else if (IsWayWithL(routeName))
            {
                if (wayWithL == null)
                {
                    wayWithL = route;
                    continue;
                }
            }

            others.Add(route);
        }

        List<SplineRoute> ordered = new List<SplineRoute>();
        if (wayNoL != null)
        {
            ordered.Add(wayNoL);
        }
        if (wayWithL != null)
        {
            ordered.Add(wayWithL);
        }
        ordered.AddRange(others);

        sidesRoute = ordered.ToArray();
    }

    private bool IsWayWithL(string routeName)
    {
        if (string.IsNullOrEmpty(routeName))
        {
            return false;
        }

        bool hasWay = routeName.IndexOf("Way", System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (!hasWay)
        {
            return false;
        }

        return routeName.IndexOf("Way_L", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               routeName.IndexOf("WayL", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsWayWithoutL(string routeName)
    {
        if (string.IsNullOrEmpty(routeName))
        {
            return false;
        }

        bool hasWay = routeName.IndexOf("Way", System.StringComparison.OrdinalIgnoreCase) >= 0;
        return hasWay && !IsWayWithL(routeName);
    }

    // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Má»—i 1 SeedColor trong listColor = 50 seed = 10 block row
    private const int RowsPerColor = 10;

    /// <summary>
    /// Khá»Ÿi táº¡o toÃ n bá»™ há»‡ thá»‘ng spline tá»« LevelController.InitLevel().
    /// Má»—i SeedColor trong listColor Ä‘Æ°á»£c expand thÃ nh 10 row.
    /// Fill main trÆ°á»›c (mainRowCount row), overflow sang side theo thá»© tá»±.
    /// </summary>
    /// <param name="listColor">Danh sÃ¡ch SeedColor â€” má»—i pháº§n tá»­ = 10 row = 50 seed.</param>
    /// <param name="mainRowCount">Sá»©c chá»©a cá»§a main route (sá»‘ row).</param>
    /// <param name="sideRowCounts">Sá»©c chá»©a cá»§a side route[0], [1], ... theo thá»© tá»±.</param>
    public void Initialize(List<SeedColor> listColor, int mainRowCount, List<int> sideRowCounts)
    {
        if (listColor == null || listColor.Count == 0)
        {
            ;
            return;
        }

        // Expand: má»—i color â†’ RowsPerColor rows
        var allRows = new List<SeedColor>(listColor.Count * RowsPerColor);
        foreach (var color in listColor)
            for (int i = 0; i < RowsPerColor; i++)
                allRows.Add(color);

        int cursor = 0;

        // â”€â”€ Main route â€” fill trÆ°á»›c â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (mainRoute != null && mainRowCount > 0)
        {
            int take = Mathf.Min(mainRowCount, allRows.Count - cursor);
            if (take > 0)
            {
                mainRoute.Initialize(allRows.GetRange(cursor, take));
                cursor += take;
            }
        }

        // â”€â”€ Side routes â€” fill theo thá»© tá»± khi main cÃ²n dÆ° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (sidesRoute == null) return;

        for (int i = 0; i < sidesRoute.Length && cursor < allRows.Count; i++)
        {
            if (sidesRoute[i] == null) continue;

            int cap  = (sideRowCounts != null && i < sideRowCounts.Count) ? sideRowCounts[i] : 0;
            int take = cap > 0
                ? Mathf.Min(cap, allRows.Count - cursor)
                : allRows.Count - cursor;   // náº¿u khÃ´ng giá»›i háº¡n â†’ láº¥y háº¿t pháº§n cÃ²n láº¡i

            if (take <= 0) continue;

            sidesRoute[i].Initialize(allRows.GetRange(cursor, take));
            cursor += take;
        }

        ;
    }

    // â”€â”€ Public route accessors (used by HeroShooter booster) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public SplineRoute   GetMainRoute()  => mainRoute;
    public SplineRoute[] GetSideRoutes() => sidesRoute;

    public void SetStoryPaused(bool paused)
    {
        if (mainRoute != null)
        {
            mainRoute.SetStoryPaused(paused);
        }

        if (sidesRoute != null)
        {
            for (int i = 0; i < sidesRoute.Length; i++)
            {
                if (sidesRoute[i] != null)
                {
                    sidesRoute[i].SetStoryPaused(paused);
                }
            }
        }
    }

    public bool IsAnyRefillInProgress()
    {
        return processingRowIds.Count > 0 || processingSources.Count > 0;
    }

    /// <summary>
    /// Gá»i tá»« SplineRouteRefillTrigger khi mainBlockRow cháº¡y qua collider
    /// cá»§a side route cÃ³ index = sideIndex.
    /// </summary>
    public void TriggerRefill(int sideIndex, GameObject mainBlockRow)
    {
        if (mainBlockRow == null) return;
        if (mainRoute == null || mainRoute.IsMovementPaused() || !mainRoute.ContainsRow(mainBlockRow)) return;
        if (sidesRoute == null || sideIndex < 0 || sideIndex >= sidesRoute.Length) return;

        SplineRoute source = sidesRoute[sideIndex];
        if (source == null || source.IsMovementPaused()) return;

        var mainSeeder = mainBlockRow.GetComponent<BlockRowSeedSpawner>();
        if (mainSeeder == null || !mainSeeder.HasEmptySlot()) return;

        int rowId = mainBlockRow.GetInstanceID();
        if (processingRowIds.Contains(rowId)) return;

        if (processingSources.Contains(source))
        {
            EnqueuePendingRefill(source, mainBlockRow);
            return;
        }

        processingSources.Add(source);
        StartCoroutine(FillRowCoroutine(rowId, mainSeeder, source));
    }

    private void EnqueuePendingRefill(SplineRoute source, GameObject mainBlockRow)
    {
        if (source == null || mainBlockRow == null)
        {
            return;
        }

        int rowId = mainBlockRow.GetInstanceID();

        if (!pendingRowsBySource.TryGetValue(source, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pendingRowsBySource[source] = queue;
        }

        if (!pendingRowIdsBySource.TryGetValue(source, out HashSet<int> idSet))
        {
            idSet = new HashSet<int>();
            pendingRowIdsBySource[source] = idSet;
        }

        if (!idSet.Add(rowId))
        {
            return;
        }

        queue.Enqueue(mainBlockRow);
    }

    private void StartNextPendingRefill(SplineRoute source)
    {
        if (source == null || processingSources.Contains(source))
        {
            return;
        }

        if (!pendingRowsBySource.TryGetValue(source, out Queue<GameObject> queue) || queue == null)
        {
            return;
        }

        pendingRowIdsBySource.TryGetValue(source, out HashSet<int> idSet);

        while (queue.Count > 0)
        {
            GameObject pendingRow = queue.Dequeue();
            if (pendingRow == null)
            {
                continue;
            }

            int rowId = pendingRow.GetInstanceID();
            idSet?.Remove(rowId);

            if (processingRowIds.Contains(rowId))
            {
                continue;
            }

            BlockRowSeedSpawner seeder = pendingRow.GetComponent<BlockRowSeedSpawner>();
            if (seeder == null || !seeder.HasEmptySlot())
            {
                continue;
            }

            processingSources.Add(source);
            StartCoroutine(FillRowCoroutine(rowId, seeder, source));
            return;
        }
    }

    private void CompleteRefillProcess(int rowId, SplineRoute source, bool notifyRefillCompleted)
    {
        processingRowIds.Remove(rowId);
        processingSources.Remove(source);

        if (notifyRefillCompleted)
        {
            OnRefillCompleted?.Invoke(source);
        }

        StartNextPendingRefill(source);
    }

    private RefillTransferBuffers RentRefillTransferBuffers()
    {
        if (refillBufferPool.Count > 0)
        {
            RefillTransferBuffers pooled = refillBufferPool.Pop();
            pooled.Reset();
            return pooled;
        }

        return new RefillTransferBuffers();
    }

    private void ReturnRefillTransferBuffers(RefillTransferBuffers buffers)
    {
        if (buffers == null)
        {
            return;
        }

        buffers.Reset();
        refillBufferPool.Push(buffers);
    }

    // â”€â”€ Coroutines â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    IEnumerator FillRowCoroutine(int rowId, BlockRowSeedSpawner mainSeeder, SplineRoute source)
    {
        processingRowIds.Add(rowId);
        GamePlayController gamePlayController = GamePlayController.Instance;
        RefillTransferBuffers buffers = RentRefillTransferBuffers();

        try
        {
            // YÃªu cáº§u táº¥t cáº£ 5 slot Ä‘á»u trá»‘ng
            for (int i = 0; i < 5; i++)
            {
                if (mainSeeder.GetSeed(i) != null)
                {
                    CompleteRefillProcess(rowId, source, false);
                    yield break;
                }
            }

            if (!source.TryTakeFullFrontRow(buffers.colors, buffers.consumedPositions, out int consumedCount, out GameObject consumedFrontRow, true))
            {
                CompleteRefillProcess(rowId, source, false);
                yield break;
            }

            int targetCount = 0;

            for (int i = 0; i < consumedCount && i < 5; i++)
            {
                if (!mainSeeder.FillSlotWithColor(i, buffers.colors[i]))
                {
                    continue;
                }

                GameObject seed = mainSeeder.GetSeed(i);
                if (seed == null)
                {
                    continue;
                }

                buffers.targetSeeds[targetCount] = seed;
                buffers.startPositions[targetCount] = buffers.consumedPositions[i];
                targetCount++;
            }

            if (targetCount > 0)
            {
                yield return PlayTransferAnimation(buffers, targetCount, mainSeeder);
            }

            // Chá»‰ dá»“n hÃ ng side route sau khi transfer animation Ä‘Ã£ hoÃ n táº¥t.
            source.CompleteDeferredFrontRowConsume(consumedFrontRow);

            // Sau khi refill xong váº«n trigger thÃªm 1 láº§n Ä‘á»ƒ chá»‘t tráº¡ng thÃ¡i cuá»‘i.
            if (gamePlayController != null)
            {
                gamePlayController.CheckLoseAfterRefill();
            }

            CompleteRefillProcess(rowId, source, true);
        }
        finally
        {
            ReturnRefillTransferBuffers(buffers);
        }
    }

    IEnumerator PlayTransferAnimation(RefillTransferBuffers buffers, int seedCount, BlockRowSeedSpawner mainSeeder)
    {
        if (buffers == null || seedCount <= 0)
        {
            yield break;
        }

        // â”€â”€ TÃ­nh duration cÃ³ trá»ng sá»‘ Ä‘á»ƒ táº¥t cáº£ háº¡t Ä‘áº¿n cÃ¹ng lÃºc â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        float minDist  = float.MaxValue;
        float maxDist  = float.MinValue;

        for (int i = 0; i < seedCount; i++)
        {
            GameObject seed = buffers.targetSeeds[i];
            if (seed == null)
            {
                buffers.distances[i] = 0f;
                continue;
            }

            float dist = Vector3.Distance(buffers.startPositions[i], seed.transform.position);
            buffers.distances[i] = dist;
            if (dist < minDist) minDist = dist;
            if (dist > maxDist) maxDist = dist;
        }

        float distRange  = Mathf.Max(0.0001f, maxDist - minDist);
        float maxDur     = 0f;
        int tweenCount = 0;

        for (int i = 0; i < seedCount; i++)
        {
            GameObject seed = buffers.targetSeeds[i];
            if (seed == null)
            {
                continue;
            }

            float tailRatio = seedCount > 1
                ? (float)(seedCount - 1 - i) / (seedCount - 1)
                : 0f;
            float tailBoost  = tailRatio * Mathf.Max(0f, TailSeedSpeedBoost);
            float distBoost  = ((buffers.distances[i] - minDist) / distRange) * Mathf.Max(0f, FarDistanceSpeedBoost);
            float baseDur    = Mathf.Max(0.01f, TransferDuration / Mathf.Max(0.1f, 1f + tailBoost + distBoost));
            float mul        = SpeedMultiplierManager.GetBaseMultiplier();
            float dur        = baseDur / Mathf.Max(0.1f, mul);
            buffers.durations[tweenCount] = dur;

            Tween t = BuildSeedMoveTween(seed, mainSeeder, buffers.startPositions[i], dur);
            if (t == null)
            {
                continue;
            }

            buffers.tweens[tweenCount] = t;
            tweenCount++;
            if (dur > maxDur) maxDur = dur;
        }

        if (tweenCount <= 0)
        {
            yield break;
        }

        // Stagger: háº¡t ngáº¯n nháº¥t Ä‘á»£i trÆ°á»›c, táº¥t cáº£ káº¿t thÃºc cÃ¹ng lÃºc
        Sequence seq = DOTween.Sequence();
        for (int i = 0; i < tweenCount; i++)
        {
            seq.Insert(Mathf.Max(0f, maxDur - buffers.durations[i]), buffers.tweens[i]);
        }

        yield return seq.WaitForCompletion();
    }

    /// <summary>
    /// Di chuyá»ƒn seed tá»« startWorldPos â†’ localPosition zero cá»§a slot,
    /// kÃ¨m parabolic arc ngang + rotation align vá»›i mainSeeder.
    /// </summary>
    Tween BuildSeedMoveTween(
        GameObject          seed,
        BlockRowSeedSpawner mainSeeder,
        Vector3             startWorldPos,
        float               duration)
    {
        if (seed == null || mainSeeder == null) return null;

        Transform slotTf = seed.transform.parent;
        if (slotTf == null) return null;

        // Snap seed Ä‘áº¿n vá»‹ trÃ­ start (giá»¯ nguyÃªn Y)
        Vector3 snapStart = new Vector3(startWorldPos.x, seed.transform.position.y, startWorldPos.z);
        seed.transform.position = snapStart;
        seed.transform.SetParent(null, true);

        float signedAngle = Vector3.SignedAngle(
            seed.transform.forward,
            mainSeeder.transform.forward,
            Vector3.up);
        float extraClockwiseYaw = Mathf.Abs(RefillSeedClockwiseYaw);
        float targetYaw = signedAngle + extraClockwiseYaw;

        seed.transform.SetParent(slotTf, true);

        Vector3 localStart = seed.transform.localPosition;
        Vector3 localEnd   = Vector3.zero;
        Vector3 moveDir    = localEnd - localStart;
        Vector3 sideDir    = -Vector3.Cross(Vector3.up, moveDir).normalized;
        float   arcDist    = moveDir.magnitude * Mathf.Max(0f, CurveStrength);
        if (sideDir.sqrMagnitude < 0.0001f) sideDir = Vector3.right;

        Quaternion startRotation = seed.transform.rotation;
        Quaternion endRotation = Quaternion.AngleAxis(targetYaw, Vector3.up) * startRotation;

        Tween tween = DOTween.To(() => 0f, prog =>
        {
            if (seed == null)
            {
                return;
            }

            Vector3 lin = Vector3.LerpUnclamped(localStart, localEnd, prog);
            lin += sideDir * (4f * arcDist * prog * (1f - prog));
            lin.y = localStart.y;
            seed.transform.localPosition = lin;

            float rotProg = DOVirtual.EasedValue(0f, 1f, prog, Ease.OutQuad);
            seed.transform.rotation = Quaternion.SlerpUnclamped(startRotation, endRotation, rotProg);
        }, 1f, duration).SetEase(Ease.Linear);

        tween.OnComplete(() =>
        {
            if (seed == null) return;
            seed.transform.SetParent(slotTf, false);
            seed.transform.localPosition = Vector3.zero;
            seed.transform.localRotation = Quaternion.identity;
        });

        return tween;
    }
}


