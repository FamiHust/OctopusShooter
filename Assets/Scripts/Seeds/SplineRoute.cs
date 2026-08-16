using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Rendering;
using Unity.Mathematics;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Má»™t spline conveyor route â€” main (loop, di chuyá»ƒn liÃªn tá»¥c) hoáº·c side (queue tÄ©nh, cáº¥p seed cho main).
/// KhÃ´ng tá»± config mÃ u â€” nháº­n rowColorPlan tá»« SplineController.Initialize().
/// </summary>
public class SplineRoute : MonoBehaviour
{
    public enum RouteMode { Main, Side }

    [Header("Config (tÃ¹y chá»n â€” Ä‘á»ƒ dÃ¹ng chung giá»¯a cÃ¡c prefab)")]
    [Tooltip("GÃ¡n SplineRouteConfig asset Ä‘á»ƒ Ä‘á»c setting tá»« Ä‘Ã³. Náº¿u Ä‘á»ƒ trá»‘ng sáº½ dÃ¹ng giÃ¡ trá»‹ inline bÃªn dÆ°á»›i.")]
    [SerializeField] private SplineRouteConfig config;

    [Header("Spline")]
    [SerializeField] private SplineContainer spline;
    [SerializeField] private RouteMode routeMode = RouteMode.Main;

    [Header("Movement (Main only) â€” dÃ¹ng khi khÃ´ng cÃ³ Config")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float despawnDistance = 20f;

    [Header("Prefabs")]
    [SerializeField] private GameObject blockRowPrefab;
    [SerializeField] private GameObject seedPrefab;

    [Header("Spacing â€” dÃ¹ng khi khÃ´ng cÃ³ Config")]
    [SerializeField] private bool autoCalculateLength = true;
    [SerializeField] private bool distributeEvenly = true;
    [SerializeField] private float fixedSpacing = 2f;

    [Header("Performance (Side Route)")]
    [SerializeField] private bool optimizeSideSeedVisuals = true;
    [SerializeField] private int sideAlwaysVisibleFrontRows = 2;
    [SerializeField] private float sideVisualRefreshInterval = 0.2f;
    [SerializeField] private float sideViewportPadding = 0.15f;
    [SerializeField] private bool strictViewportSeedCulling = true;
    [SerializeField] private bool optimizeMainSeedVisuals = true;
    [SerializeField] private float mainVisualRefreshInterval = 0.12f;
    [SerializeField] private int maxRenderedRowsMainRoute = 10;
    [SerializeField] private int maxRenderedRowsSideRoute = 6;

    [Header("Performance (Row Renderers)")]
    [SerializeField] private bool optimizeRowRendererCulling = true;
    [SerializeField] private bool optimizeRowRendererSettings = true;
    [SerializeField] private bool allowPbrProbeLightingForRows = true;
    [SerializeField] private bool disableRowShadowCasting = true;
    [SerializeField] private bool disableRowReceiveShadows = true;
    [SerializeField] private bool disableRowLightProbes = false;
    [SerializeField] private bool disableRowReflectionProbes = false;

    [Header("Spawn Performance")]
    [SerializeField] private bool deferRowSpawnOnLowEnd = true;
    [SerializeField] private bool forceDeferredRowSpawn = false;
    [SerializeField, Min(1)] private int deferredSpawnRowsPerFrame = 10;
    [SerializeField, Min(1)] private int deferredSpawnMinRowsThreshold = 30;
    [SerializeField] private int deferredSpawnLowEndSystemMemoryMb = 3000;
    [SerializeField] private int deferredSpawnLowEndProcessorCount = 4;

    // â”€â”€ Config helper (fallback vá» giÃ¡ trá»‹ inline náº¿u chÆ°a gÃ¡n Config) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private float MoveSpeed          => config != null ? config.moveSpeed          : moveSpeed;
    private float DespawnDistance    => config != null ? config.despawnDistance    : despawnDistance;
    private GameObject BlockRowPrefab => config != null && config.blockRowPrefab != null ? config.blockRowPrefab : blockRowPrefab;
    private GameObject BlockPrefab    => config != null && config.blockPrefab != null ? config.blockPrefab : seedPrefab;
    private bool  AutoCalcLength     => config != null ? config.autoCalculateLength: autoCalculateLength;
    private bool  DistributeEvenly   => config != null ? config.distributeEvenly   : distributeEvenly;
    private float FixedSpacing       => 0.04f;
    private float QueueShiftDuration => config != null ? config.queueShiftDuration : 0.35f;

    // â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private List<SeedColor> rowColorPlan = new List<SeedColor>();
    private int rowsToSpawn;
    private int rowsSpawned;
    private float globalDistance;
    private float splineLength;
    private float blockRowLength = 2f;
    private float spacing = 2f;
    private bool isInitialized;

    private readonly List<BlockRowData> activeRows  = new List<BlockRowData>();
    private readonly List<GameObject>   parkedRows  = new List<GameObject>();
    private SplineRoute cachedMainRouteForSync;
    private bool hasAttemptedMainRouteResolve;
    private float nextSideVisualRefreshTime;
    private float nextMainVisualRefreshTime;
    private Camera cachedRenderCamera;
    private bool isTutorialPaused;
    private bool isBoosterFocusPaused;
    private bool isMechanicPaused;
    private bool isStoryPaused;
    private readonly List<VisualCandidate> visualCandidatesBuffer = new List<VisualCandidate>(64);
    private readonly HashSet<int> selectedVisibleRowIds = new HashSet<int>();
    private int lastVisualBudgetRowCount = -1;
    private Vector3 lastVisualBudgetCameraPosition;
    private Quaternion lastVisualBudgetCameraRotation;
    private bool hasVisualBudgetCameraSnapshot;
    private Coroutine pendingDeferredSpawnRoutine;
    private bool isDeferredSpawning;

    private const float visualBudgetCameraMoveThresholdSqr = 0.04f;
    private const float visualBudgetCameraRotateThreshold = 1.5f;

    private struct VisualCandidate
    {
        public BlockRowData data;
        public float score;
    }

    private class BlockRowData
    {
        public GameObject row;
        public float spawnDistance;
        public BlockRowSeedSpawner seeder;
        public Collider rowCollider;
        public Renderer rowRenderer;
        public Renderer[] childRenderers;

        public BlockRowData(GameObject r, float d)
        {
            row = r;
            spawnDistance = d;
        }
    }

    // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Khá»Ÿi táº¡o route vá»›i danh sÃ¡ch mÃ u Ä‘Ã£ Ä‘Æ°á»£c slice tá»« SplineController.
    /// Spawn táº¥t cáº£ BlockRows ngay láº­p tá»©c.
    /// </summary>
    public void Initialize(List<SeedColor> colorPlan)
    {
        StopPendingDeferredSpawnRoutine();

        // Dá»n dáº¹p state cÅ© (náº¿u cÃ³)
        foreach (var d in activeRows) { if (d.row != null) ReleaseRowWithSeeds(d.row); }
        foreach (var r in parkedRows) { if (r != null)    ReleaseRowWithSeeds(r); }
        activeRows.Clear();
        parkedRows.Clear();

        rowColorPlan   = new List<SeedColor>(colorPlan);
        rowsToSpawn    = rowColorPlan.Count;
        rowsSpawned    = 0;
        globalDistance = 0f;
        hasAttemptedMainRouteResolve = false;
        cachedMainRouteForSync = null;
        hasVisualBudgetCameraSnapshot = false;
        lastVisualBudgetRowCount = -1;

        if (spline == null)
        {
            ;
            return;
        }

        splineLength = spline.CalculateLength();

        if (AutoCalcLength && BlockRowPrefab != null)
            MeasureBlockRowLength();

        CalculateSpacing();

        isInitialized = true;

        if (ShouldUseDeferredSpawn())
        {
            isDeferredSpawning = true;
            pendingDeferredSpawnRoutine = StartCoroutine(SpawnAllDeferredRoutine());
            return;
        }

        isDeferredSpawning = false;
        SpawnAll();

        // Snap ngay vá» Ä‘Ãºng vá»‹ trÃ­ Ä‘á»ƒ trÃ¡nh 1-frame overlap
        if (routeMode == RouteMode.Main)
            RefreshMainPositions();
    }

    public bool IsComplete()           => rowsSpawned >= rowsToSpawn;
    public float GetMoveSpeed()        => MoveSpeed;
    public float GetSpacing()          => spacing;
    public RouteMode GetRouteMode()    => routeMode;
    public SplineContainer GetSplineContainer() => spline;
    public float GetSplineLength()     => splineLength;
    public bool IsMovementPaused()     => IsRoutePaused();

    public int GetActiveRowCount()
    {
        RemoveNullRowsFromActiveList(activeRows);
        return activeRows.Count;
    }

    /// <summary>
    /// Swap all active rows between two Side routes.
    /// Row order is preserved per source route, then re-laid out on destination route.
    /// </summary>
    public bool TrySwapAllRowsWith(SplineRoute otherSideRoute)
    {
        if (otherSideRoute == null || otherSideRoute == this)
        {
            return false;
        }

        if (routeMode != RouteMode.Side || otherSideRoute.routeMode != RouteMode.Side)
        {
            return false;
        }

        RemoveNullRowsFromActiveList(activeRows);
        RemoveNullRowsFromActiveList(otherSideRoute.activeRows);

        List<BlockRowData> thisSnapshot = new List<BlockRowData>(activeRows);
        List<BlockRowData> otherSnapshot = new List<BlockRowData>(otherSideRoute.activeRows);

        activeRows.Clear();
        activeRows.AddRange(otherSnapshot);

        otherSideRoute.activeRows.Clear();
        otherSideRoute.activeRows.AddRange(thisSnapshot);

        ReparentRowsToRoute(activeRows, transform);
        ReparentRowsToRoute(otherSideRoute.activeRows, otherSideRoute.transform);

        hasVisualBudgetCameraSnapshot = false;
        lastVisualBudgetRowCount = -1;
        otherSideRoute.hasVisualBudgetCameraSnapshot = false;
        otherSideRoute.lastVisualBudgetRowCount = -1;

        RebuildStaticPositions();
        otherSideRoute.RebuildStaticPositions();

        return true;
    }

    public void ReleaseAllRowsToPoolNow()
    {
        for (int i = activeRows.Count - 1; i >= 0; i--)
        {
            BlockRowData data = activeRows[i];
            if (data != null && data.row != null)
            {
                ReleaseRowWithSeeds(data.row);
            }
        }
        activeRows.Clear();

        for (int i = parkedRows.Count - 1; i >= 0; i--)
        {
            GameObject row = parkedRows[i];
            if (row != null)
            {
                ReleaseRowWithSeeds(row);
            }
        }
        parkedRows.Clear();
    }

    public void SetTutorialPaused(bool paused)
    {
        if (isTutorialPaused == paused)
        {
            return;
        }

        isTutorialPaused = paused;
        ApplyRoutePauseState();
    }

    public void SetBoosterFocusPaused(bool paused)
    {
        if (isBoosterFocusPaused == paused)
        {
            return;
        }

        isBoosterFocusPaused = paused;
        ApplyRoutePauseState();
    }

    public void SetMechanicPaused(bool paused)
    {
        if (isMechanicPaused == paused)
        {
            return;
        }

        isMechanicPaused = paused;
        ApplyRoutePauseState();
    }

    public void SetStoryPaused(bool paused)
    {
        if (isStoryPaused == paused)
        {
            return;
        }

        isStoryPaused = paused;
        ApplyRoutePauseState();
    }

    private bool IsRoutePaused()
    {
        if (isStoryPaused || isTutorialPaused || isBoosterFocusPaused || isMechanicPaused)
        {
            return true;
        }

        if (StoryManager.Instance != null && StoryManager.Instance.IsPlayingStory)
        {
            return true;
        }

        return false;
    }

    private void ApplyRoutePauseState()
    {
        bool shouldPause = IsRoutePaused();

        for (int i = 0; i < activeRows.Count; i++)
        {
            BlockRowData data = activeRows[i];
            if (data == null || data.row == null)
            {
                continue;
            }

            if (shouldPause)
            {
                DOTween.Pause(data.row.transform);
            }
            else
            {
                DOTween.Play(data.row.transform);
            }
        }
    }

    public List<GameObject> GetActiveBlockRows()
    {
        var result = new List<GameObject>(activeRows.Count);
        foreach (var d in activeRows)
            if (d.row != null) result.Add(d.row);
        return result;
    }

    public int FillActiveBlockRowSeeders(List<BlockRowSeedSpawner> output, bool descending = false)
    {
        if (output == null)
        {
            return 0;
        }

        output.Clear();
        if (activeRows == null || activeRows.Count == 0)
        {
            return 0;
        }

        if (descending)
        {
            for (int i = activeRows.Count - 1; i >= 0; i--)
            {
                BlockRowData data = activeRows[i];
                if (data == null || data.row == null)
                {
                    continue;
                }

                BlockRowSeedSpawner seeder = GetCachedSeeder(data);
                if (seeder != null)
                {
                    output.Add(seeder);
                }
            }

            return output.Count;
        }

        for (int i = 0; i < activeRows.Count; i++)
        {
            BlockRowData data = activeRows[i];
            if (data == null || data.row == null)
            {
                continue;
            }

            BlockRowSeedSpawner seeder = GetCachedSeeder(data);
            if (seeder != null)
            {
                output.Add(seeder);
            }
        }

        return output.Count;
    }

    /// <summary>
    /// Kiá»ƒm tra má»™t row cÃ³ Ä‘ang thuá»™c route hiá»‡n táº¡i hay khÃ´ng.
    /// DÃ¹ng Ä‘á»ƒ cháº·n trigger refill bá»‹ báº¯t nháº§m row tá»« route khÃ¡c.
    /// </summary>
    public bool ContainsRow(GameObject row)
    {
        if (row == null)
        {
            return false;
        }

        for (int i = 0; i < activeRows.Count; i++)
        {
            BlockRowData data = activeRows[i];
            if (data == null || data.row == null)
            {
                continue;
            }

            if (data.row == row)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gá»i khi má»™t block row bá»‹ phÃ¡ há»§y hoÃ n toÃ n (tá»« hero shooter hoáº·c bullet).
    /// XÃ³a row khá»i queue vÃ  animate cÃ¡c rows phÃ­a trÆ°á»›c dá»“n lÃªn.
    /// </summary>
    public void OnRowDestroyed(GameObject destroyedRow)
    {
        if (destroyedRow == null) return;

        // TÃ¬m vÃ  xÃ³a row khá»i activeRows
        BlockRowData targetData = null;
        foreach (var d in activeRows)
        {
            if (d.row == destroyedRow)
            {
                targetData = d;
                break;
            }
        }

        if (targetData != null)
        {
            HandleDepletedRow(targetData);
        }
    }

    /// <summary>
    /// Vá»‹ trÃ­ world cá»§a front supply row (row Ä‘áº§u tiÃªn cÃ²n Ä‘á»§ háº¡t).
    /// DÃ¹ng cho transfer animation trong SplineController.
    /// </summary>
    public Vector3 GetFrontSupplyWorldPosition()
    {
        var front = FindFirstRowWithSeeds(1);
        if (front?.row != null) return front.row.transform.position;
        if (spline != null) return spline.EvaluatePosition(routeMode == RouteMode.Side ? 1f : 0f);
        return transform.position;
    }

    /// <summary>
    /// Consume 5 háº¡t Ä‘áº§u tiÃªn tá»« front row, tráº£ vá» mÃ u + vá»‹ trÃ­ world cá»§a tá»«ng háº¡t.
    /// DÃ¹ng bá»Ÿi SplineController khi trigger refill animation.
    /// </summary>
    public bool TryTakeFullFrontRow(SeedColor[] colorsBuffer, Vector3[] worldPositionsBuffer, out int consumedCount, out GameObject consumedFrontRow, bool deferQueueShift = false)
    {
        consumedCount = 0;
        consumedFrontRow = null;

        if (colorsBuffer == null || worldPositionsBuffer == null || colorsBuffer.Length < 5 || worldPositionsBuffer.Length < 5)
        {
            return false;
        }

        var front = FindFirstRowWithSeeds(5);
        if (front?.row == null) return false;

        consumedFrontRow = front.row;

        BlockRowSeedSpawner seeder = GetCachedSeeder(front);
        if (seeder == null || seeder.GetSeedCount() < 5) return false;

        for (int i = 0; i < 5; i++)
        {
            if (!seeder.TryConsumeFirstSeed(out SeedColor c, out Vector3 p, true))
            {
                consumedCount = 0;
                return false;
            }

            colorsBuffer[i] = c;
            worldPositionsBuffer[i] = p;
            consumedCount++;
        }

        if (!deferQueueShift)
        {
            HandleDepletedRow(front);
        }

        return true;
    }

    public bool TryTakeFullFrontRow(out List<SeedColor> colors, out List<Vector3> worldPositions, out GameObject consumedFrontRow, bool deferQueueShift = false)
    {
        colors = new List<SeedColor>(5);
        worldPositions = new List<Vector3>(5);

        SeedColor[] colorsBuffer = new SeedColor[5];
        Vector3[] positionsBuffer = new Vector3[5];
        if (!TryTakeFullFrontRow(colorsBuffer, positionsBuffer, out int consumedCount, out consumedFrontRow, deferQueueShift))
        {
            return false;
        }

        for (int i = 0; i < consumedCount; i++)
        {
            colors.Add(colorsBuffer[i]);
            worldPositions.Add(positionsBuffer[i]);
        }

        return true;
    }

    public void CompleteDeferredFrontRowConsume(GameObject consumedFrontRow)
    {
        if (consumedFrontRow == null)
        {
            return;
        }

        for (int i = 0; i < activeRows.Count; i++)
        {
            BlockRowData rowData = activeRows[i];
            if (rowData == null || rowData.row != consumedFrontRow)
            {
                continue;
            }

            HandleDepletedRow(rowData);
            return;
        }
    }

    // â”€â”€ Unity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    void OnValidate()
    {
        TryAutoAssignConfigByRouteMode();
        TryAutoAssignSplineByObjectName();
    }

    void Update()
    {
        if (!isInitialized) return;

        if (isDeferredSpawning)
        {
            return;
        }

        if (IsRoutePaused())
        {
            return;
        }

        if (routeMode == RouteMode.Main)
        {
            float effectiveSpeed = MoveSpeed * SpeedMultiplierManager.GetEffectiveMultiplier();
            globalDistance += effectiveSpeed * Time.deltaTime;
            RefreshMainPositions();

            if (optimizeMainSeedVisuals && Time.time >= nextMainVisualRefreshTime)
            {
                nextMainVisualRefreshTime = Time.time + Mathf.Max(0.05f, mainVisualRefreshInterval);
                if (ShouldRunTimedVisualBudgetRefresh())
                {
                    RefreshMainSeedVisualBudget();
                }
            }

            // DespawnFarRows â€” intentionally disabled: block rows loop forever
            return;
        }

        if (optimizeSideSeedVisuals && Time.time >= nextSideVisualRefreshTime)
        {
            nextSideVisualRefreshTime = Time.time + Mathf.Max(0.05f, sideVisualRefreshInterval);
            if (ShouldRunTimedVisualBudgetRefresh())
            {
                RefreshSideSeedVisualBudget();
            }
        }
    }

    private bool ShouldRunTimedVisualBudgetRefresh()
    {
        int currentRowCount = activeRows.Count;
        if (currentRowCount != lastVisualBudgetRowCount)
        {
            return true;
        }

        Camera cam = ResolveRenderCamera();
        if (cam == null)
        {
            return !hasVisualBudgetCameraSnapshot;
        }

        if (!hasVisualBudgetCameraSnapshot)
        {
            return true;
        }

        if ((cam.transform.position - lastVisualBudgetCameraPosition).sqrMagnitude > visualBudgetCameraMoveThresholdSqr)
        {
            return true;
        }

        if (Quaternion.Angle(cam.transform.rotation, lastVisualBudgetCameraRotation) > visualBudgetCameraRotateThreshold)
        {
            return true;
        }

        return false;
    }

    void OnDestroy()
    {
        StopPendingDeferredSpawnRoutine();

        // Tráº£ seed vá» pool trÆ°á»›c khi há»§y row Ä‘á»ƒ trÃ¡nh há»¥t pool giá»¯a cÃ¡c level.
        foreach (var d in activeRows)
        {
            if (d.row != null)
                ReleaseRowWithSeeds(d.row);
        }
        activeRows.Clear();

        foreach (var r in parkedRows)
        {
            if (r != null)
                ReleaseRowWithSeeds(r);
        }
        parkedRows.Clear();
    }

    private bool ShouldUseDeferredSpawn()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        if (forceDeferredRowSpawn)
        {
            return true;
        }

        if (!deferRowSpawnOnLowEnd)
        {
            return false;
        }

        if (rowsToSpawn < Mathf.Max(1, deferredSpawnMinRowsThreshold))
        {
            return false;
        }

        int memoryMb = SystemInfo.systemMemorySize;
        if (memoryMb > 0 && memoryMb <= Mathf.Max(512, deferredSpawnLowEndSystemMemoryMb))
        {
            return true;
        }

        return SystemInfo.processorCount <= Mathf.Max(1, deferredSpawnLowEndProcessorCount);
    }

    private IEnumerator SpawnAllDeferredRoutine()
    {
        int rowsPerFrame = Mathf.Max(1, deferredSpawnRowsPerFrame);

        while (rowsSpawned < rowsToSpawn)
        {
            int spawnedThisFrame = 0;
            while (rowsSpawned < rowsToSpawn && spawnedThisFrame < rowsPerFrame)
            {
                SpawnNextRow();
                spawnedThisFrame++;
            }

            yield return null;
        }

        pendingDeferredSpawnRoutine = null;
        isDeferredSpawning = false;

        if (routeMode == RouteMode.Side)
        {
            RebuildStaticPositions();
            RefreshSideSeedVisualBudget();
        }
        else
        {
            RefreshMainPositions();
            if (optimizeMainSeedVisuals)
            {
                RefreshMainSeedVisualBudget();
            }
        }
    }

    private void StopPendingDeferredSpawnRoutine()
    {
        if (pendingDeferredSpawnRoutine != null)
        {
            StopCoroutine(pendingDeferredSpawnRoutine);
            pendingDeferredSpawnRoutine = null;
        }

        isDeferredSpawning = false;
    }

    private void TryAutoAssignConfigByRouteMode()
    {
#if UNITY_EDITOR
        string targetName = routeMode == RouteMode.Main
            ? "SplineRouteConfigDistributeEven"
            : "SplineRouteConfigNoDistribute";

        if (config != null && string.Equals(config.name, targetName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets($"{targetName} t:SplineRouteConfig");
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        SplineRouteConfig foundConfig = AssetDatabase.LoadAssetAtPath<SplineRouteConfig>(path);
        if (foundConfig == null)
        {
            return;
        }

        config = foundConfig;
        EditorUtility.SetDirty(this);
#endif
    }

    private void TryAutoAssignSplineByObjectName()
    {
        string currentName = gameObject.name;
        if (string.IsNullOrEmpty(currentName))
        {
            return;
        }

        Func<string, bool> isWayWithL = value =>
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf("Way_L", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("WayL", StringComparison.OrdinalIgnoreCase) >= 0;
        };

        Func<string, bool> isSlidesRouteL = value =>
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf("SlidesRouteL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("SlidesRoute_L", StringComparison.OrdinalIgnoreCase) >= 0;
        };

        Predicate<Transform> targetNamePredicate = null;

        // Rule 1: object name chá»©a Belt => tÃ¬m object name chá»©a MainSpline.
        if (currentName.IndexOf("Belt", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            targetNamePredicate = tr => tr != null &&
                                        tr.name.IndexOf("MainSpline", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        // Rule 2: object name chá»©a Way + cÃ³ L => tÃ¬m object name chá»©a SlidesRoute + cÃ³ L.
        else if (currentName.IndexOf("Way", StringComparison.OrdinalIgnoreCase) >= 0 && isWayWithL(currentName))
        {
            targetNamePredicate = tr => tr != null &&
                                        isSlidesRouteL(tr.name);
        }
        // Rule 3: object name chá»©a Way + khÃ´ng cÃ³ L => tÃ¬m object name chá»©a SlidesRoute + khÃ´ng cÃ³ L.
        else if (currentName.IndexOf("Way", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            targetNamePredicate = tr => tr != null &&
                                        tr.name.IndexOf("SlidesRoute", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                        !isSlidesRouteL(tr.name);
        }

        if (targetNamePredicate == null)
        {
            return;
        }

        Transform searchRoot = transform.root != null ? transform.root : transform;
        SplineContainer[] containers = searchRoot.GetComponentsInChildren<SplineContainer>(true);
        if (containers == null || containers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < containers.Length; i++)
        {
            SplineContainer candidate = containers[i];
            if (candidate == null)
            {
                continue;
            }

            if (!targetNamePredicate(candidate.transform))
            {
                continue;
            }

            if (spline == candidate)
            {
                return;
            }

            spline = candidate;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            return;
        }
    }

    // â”€â”€ Internal: Spawn â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    void SpawnAll()
    {
        while (rowsSpawned < rowsToSpawn)
            SpawnNextRow();

        if (routeMode == RouteMode.Side)
        {
            RebuildStaticPositions();
            RefreshSideSeedVisualBudget();
        }
        else if (optimizeMainSeedVisuals)
        {
            RefreshMainSeedVisualBudget();
        }
    }

    void SpawnNextRow()
    {
        if (spline == null || rowsSpawned >= rowsToSpawn) return;

        SeedColor color = rowColorPlan[rowsSpawned];
        float t;
        if (routeMode == RouteMode.Side)
        {
            // Xáº¿p sÃ¡t nhau tá»« t=1 (cáº¡nh main spline) ra ngoÃ i
            float step = splineLength > 0f ? blockRowLength / splineLength : 0.1f;
            t = Mathf.Clamp01(1f - rowsSpawned * step);
        }
        else
        {
            float actualDist = rowsSpawned * spacing;
            t = (actualDist % splineLength) / splineLength;
            if (t < 0f) t += 1f;
        }
        Vector3 pos     = spline.EvaluatePosition(t);
        Vector3 fwd     = math.normalize(spline.EvaluateTangent(t));

        GameObject row;
        if (BlockRowPrefab != null && Application.isPlaying)
        {
            row = ObjectPoolManager.SpawnObject(
                BlockRowPrefab,
                pos,
                Quaternion.identity,
                gameObject,
                ObjectPoolManager.PoolType.BlockRow);
        }
        else if (BlockRowPrefab != null)
        {
            row = Instantiate(BlockRowPrefab, pos, Quaternion.identity, transform);
        }
        else
        {
            row = new GameObject($"BlockRow_{rowsSpawned:000}_{color}");
        }

        DOTween.Kill(row.transform);
        row.transform.rotation = Quaternion.LookRotation(fwd);
        row.transform.SetParent(transform);
        
        ConfigureSeeds(row, color);
        ConfigureRowRenderersForBatching(row);

        float spawnDist = globalDistance - (rowsSpawned * spacing);
        activeRows.Add(new BlockRowData(row, spawnDist));
        rowsSpawned++;
    }

    void ConfigureSeeds(GameObject row, SeedColor color)
    {
        var seeder = row.GetComponent<BlockRowSeedSpawner>() ?? row.AddComponent<BlockRowSeedSpawner>();
        if (BlockPrefab != null)
        {
            seeder.InitializeFromSpawner(BlockPrefab, color, true, true);
            EnsureBlockRowSeedCount(seeder, color);
        }
    }

    private void EnsureBlockRowSeedCount(BlockRowSeedSpawner seeder, SeedColor fallbackColor)
    {
        if (seeder == null)
        {
            return;
        }

        const int requiredSeedsPerRow = 5;
        const int maxTopUpPasses = 3;

        if (seeder.GetSeedCount() >= requiredSeedsPerRow)
        {
            return;
        }

        for (int pass = 0; pass < maxTopUpPasses && seeder.GetSeedCount() < requiredSeedsPerRow; pass++)
        {
            int filled = seeder.FillEmptySlots();
            if (filled <= 0)
            {
                break;
            }
        }

        if (seeder.GetSeedCount() >= requiredSeedsPerRow)
        {
            return;
        }

        // Fallback cho row láº¥y tá»« pool bá»‹ thiáº¿u slot do state cÅ©: reset nháº¹ 1 láº§n rá»“i fill láº¡i.
        seeder.ClearAllSeedsOnly(true);
        seeder.InitializeFromSpawner(BlockPrefab, fallbackColor, true, true);

        for (int pass = 0; pass < maxTopUpPasses && seeder.GetSeedCount() < requiredSeedsPerRow; pass++)
        {
            int filled = seeder.FillEmptySlots();
            if (filled <= 0)
            {
                break;
            }
        }

        if (seeder.GetSeedCount() < requiredSeedsPerRow)
        {
            ;
        }
    }

    // â”€â”€ Internal: Movement â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Main mode: cáº­p nháº­t vá»‹ trÃ­ má»—i frame theo globalDistance.</summary>
    void RefreshMainPositions()
    {
        foreach (var d in activeRows)
        {
            if (d.row == null) continue;
            float dist = globalDistance - d.spawnDistance;
            float t    = (dist % splineLength) / splineLength;
            if (t < 0f) t += 1f;
            d.row.transform.position = spline.EvaluatePosition(t);
            d.row.transform.rotation = Quaternion.LookRotation(math.normalize(spline.EvaluateTangent(t)));
        }
    }

    void DespawnFarRows()
    {
        for (int i = activeRows.Count - 1; i >= 0; i--)
        {
            if (activeRows[i].row == null) { activeRows.RemoveAt(i); continue; }
            if (globalDistance - activeRows[i].spawnDistance > DespawnDistance)
            {
                ReleaseRowWithSeeds(activeRows[i].row);
                activeRows.RemoveAt(i);
            }
        }
    }

    // â”€â”€ Internal: Side queue positioning â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // â”€â”€ Internal: Side queue positioning â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Side mode: tÃ­nh láº¡i vá»‹ trÃ­ tÄ©nh cá»§a táº¥t cáº£ row theo index.
    /// DÃ¹ng rowsToSpawn (tá»•ng sá»‘ lÆ°á»£ng cá»‘ Ä‘á»‹nh) Ä‘á»ƒ chia t, giÃºp khoáº£ng cÃ¡ch khÃ´ng bá»‹ giÃ£n ra khi activeRows giáº£m.
    /// </summary>
    void RebuildStaticPositions()
    {
        if (spline == null || splineLength <= 0f || activeRows.Count == 0) return;

        float step = blockRowLength / splineLength;

        for (int i = 0; i < activeRows.Count; i++)
        {
            var d = activeRows[i];
            if (d?.row == null) continue;

            // Xáº¿p sÃ¡t tá»« t=1 (miá»‡ng vÃ o main spline) ra ngoÃ i theo bÆ°á»›c blockRowLength
            float t = Mathf.Clamp01(1f - i * step);

            d.row.transform.position = spline.EvaluatePosition(t);
            d.row.transform.rotation = Quaternion.LookRotation(math.normalize(spline.EvaluateTangent(t)));
        }

        RefreshSideSeedVisualBudget();
    }

    /// <summary>Side mode: animate queue shift khi má»™t row bá»‹ consume.</summary>
    void AnimateStaticPositions(float duration)
    {
        if (spline == null || splineLength <= 0f || activeRows.Count == 0) return;

        float dur  = Mathf.Max(0.01f, duration);
        float step = blockRowLength / splineLength;

        for (int i = 0; i < activeRows.Count; i++)
        {
            var d = activeRows[i];
            if (d?.row == null) continue;

            // Vá»‹ trÃ­ cÅ©: index i+1 (trÆ°á»›c khi dá»“n hÃ ng)
            float startT = Mathf.Clamp01(1f - (i + 1) * step);
            // Vá»‹ trÃ­ Ä‘Ã­ch: index i (sau khi dá»“n hÃ ng â€” tiáº¿n 1 bÆ°á»›c vá» phÃ­a miá»‡ng)
            float endT   = Mathf.Clamp01(1f - i * step);

            // Dá»«ng cÃ¡c hiá»‡u á»©ng di chuyá»ƒn cÅ© trÃªn row nÃ y (náº¿u click quÃ¡ nhanh)
            DOTween.Kill(d.row.transform);

            // DÃ¹ng DOVirtual Ä‘á»ƒ tween giÃ¡ trá»‹ T. 
            // Cá»© má»—i frame, giÃ¡ trá»‹ tValue cháº¡y dáº§n tá»« startT Ä‘áº¿n endT, Ã©p block Ã´m sÃ¡t Ä‘Æ°á»ng ray Spline.
            Tween shiftTween = DOVirtual.Float(startT, endT, dur, (tValue) =>
            {
                if (d.row != null)
                {
                    d.row.transform.position = spline.EvaluatePosition(tValue);
                    d.row.transform.rotation = Quaternion.LookRotation(math.normalize(spline.EvaluateTangent(tValue)));
                }
            })
            .SetTarget(d.row.transform) // Gáº¯n tag Ä‘á»ƒ lá»‡nh DOTween.Kill á»Ÿ trÃªn cÃ³ thá»ƒ tÃ¬m tháº¥y
            .SetEase(Ease.Linear);      // Linear: trÆ°á»£t Ä‘á»u nhÆ° bÄƒng chuyá»n. (Báº¡n cÃ³ thá»ƒ thá»­ Ease.InOutSine náº¿u muá»‘n cÃ³ gia tá»‘c mÆ°á»£t)

            if (IsRoutePaused())
            {
                shiftTween.Pause();
            }
        }

        // Apply ngay budget má»›i Ä‘á»ƒ giáº£m render cost tá»©c thá»i sau khi consume.
        RefreshSideSeedVisualBudget();
    }

    private void RefreshSideSeedVisualBudget()
    {
        if (!optimizeSideSeedVisuals || routeMode != RouteMode.Side)
        {
            return;
        }

        RefreshRouteSeedVisualBudget(Mathf.Max(0, sideAlwaysVisibleFrontRows));
    }

    private void RefreshRouteSeedVisualBudget(int alwaysVisibleFrontRows)
    {
        Camera cam = ResolveRenderCamera();
        Plane[] frustumPlanes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;
        float padding = strictViewportSeedCulling
            ? Mathf.Min(0f, sideViewportPadding)
            : Mathf.Max(0f, sideViewportPadding);
        int guaranteedFrontRows = strictViewportSeedCulling ? 0 : Mathf.Max(0, alwaysVisibleFrontRows);

        int maxRenderedRows = GetMaxRenderedRowsForRoute();
        bool useHardCap = maxRenderedRows > 0;

        visualCandidatesBuffer.Clear();
        selectedVisibleRowIds.Clear();

        for (int i = 0; i < activeRows.Count; i++)
        {
            BlockRowData data = activeRows[i];
            if (data == null || data.row == null)
            {
                continue;
            }

            bool isGuaranteedFront = i < guaranteedFrontRows;
            bool isVisibleInCamera = IsRowVisibleForCamera(data, cam, frustumPlanes, padding, strictViewportSeedCulling);

            if (!isGuaranteedFront && !isVisibleInCamera)
            {
                continue;
            }

            // Always keep full on-screen visibility.
            if (isVisibleInCamera)
            {
                selectedVisibleRowIds.Add(data.row.GetInstanceID());
                continue;
            }

            if (!useHardCap)
            {
                selectedVisibleRowIds.Add(data.row.GetInstanceID());
                continue;
            }

            visualCandidatesBuffer.Add(new VisualCandidate
            {
                data = data,
                score = GetVisualCandidateScore(data, cam, i)
            });
        }

        if (useHardCap && visualCandidatesBuffer.Count > 0)
        {
            visualCandidatesBuffer.Sort((a, b) => a.score.CompareTo(b.score));

            int remainingSlots = Mathf.Max(0, maxRenderedRows - selectedVisibleRowIds.Count);
            int keepCount = Mathf.Min(remainingSlots, visualCandidatesBuffer.Count);
            for (int i = 0; i < keepCount; i++)
            {
                BlockRowData data = visualCandidatesBuffer[i].data;
                if (data?.row != null)
                {
                    selectedVisibleRowIds.Add(data.row.GetInstanceID());
                }
            }
        }

        for (int i = 0; i < activeRows.Count; i++)
        {
            BlockRowData data = activeRows[i];
            if (data == null || data.row == null)
            {
                continue;
            }

            bool shouldRender = selectedVisibleRowIds.Contains(data.row.GetInstanceID());
            ApplyRowRenderersVisibleState(data, shouldRender);

            BlockRowSeedSpawner seeder = GetCachedSeeder(data);
            seeder?.SetSeedVisualEnabled(shouldRender);
        }

        lastVisualBudgetRowCount = activeRows.Count;
        if (cam != null)
        {
            lastVisualBudgetCameraPosition = cam.transform.position;
            lastVisualBudgetCameraRotation = cam.transform.rotation;
            hasVisualBudgetCameraSnapshot = true;
        }
    }

    private int GetMaxRenderedRowsForRoute()
    {
        return routeMode == RouteMode.Main
            ? Mathf.Max(0, maxRenderedRowsMainRoute)
            : Mathf.Max(0, maxRenderedRowsSideRoute);
    }

    private static float GetVisualCandidateScore(BlockRowData data, Camera cam, int fallbackIndex)
    {
        if (data == null || data.row == null)
        {
            return float.MaxValue;
        }

        if (cam == null)
        {
            return fallbackIndex;
        }

        return (data.row.transform.position - cam.transform.position).sqrMagnitude;
    }

    private void ConfigureRowRenderersForBatching(GameObject row)
    {
        if (!optimizeRowRendererSettings || row == null)
        {
            return;
        }

        Renderer[] renderers = row.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (allowPbrProbeLightingForRows)
            {
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            }
            else
            {
                if (disableRowLightProbes)
                {
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                }

                if (disableRowReflectionProbes)
                {
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }

            if (renderer is MeshRenderer meshRenderer)
            {
                if (disableRowShadowCasting)
                {
                    meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                if (disableRowReceiveShadows)
                {
                    meshRenderer.receiveShadows = false;
                }
            }

            Material[] mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                continue;
            }

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat != null && !mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                }
            }
        }
    }

    private void ApplyRowRenderersVisibleState(BlockRowData data, bool isVisible)
    {
        if (!optimizeRowRendererCulling || data == null || data.row == null)
        {
            return;
        }

        Renderer rowRenderer = GetCachedRowRenderer(data);
        if (rowRenderer != null)
        {
            rowRenderer.enabled = isVisible;
        }

        Renderer[] childRenderers = GetCachedChildRenderers(data);
        if (childRenderers == null || childRenderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < childRenderers.Length; i++)
        {
            Renderer childRenderer = childRenderers[i];
            if (childRenderer == null || childRenderer == rowRenderer)
            {
                continue;
            }

            childRenderer.enabled = isVisible;
        }
    }

    private Camera ResolveRenderCamera()
    {
        if (cachedRenderCamera == null || !cachedRenderCamera.isActiveAndEnabled)
        {
            cachedRenderCamera = Camera.main;
        }

        return cachedRenderCamera;
    }

    private void RefreshMainSeedVisualBudget()
    {
        RefreshRouteSeedVisualBudget(0);
    }

    private bool IsRowVisibleForCamera(BlockRowData data, Camera cam, Plane[] frustumPlanes, float viewportPadding, bool strictViewport)
    {
        if (data == null)
        {
            return false;
        }

        GameObject row = data.row;
        if (row == null)
        {
            return false;
        }

        // KhÃ´ng cÃ³ camera thÃ¬ máº·c Ä‘á»‹nh giá»¯ render Ä‘á»ƒ trÃ¡nh máº¥t hÃ¬nh ngoÃ i Ã½ muá»‘n.
        if (cam == null)
        {
            return true;
        }

        Collider rowCollider = GetCachedRowCollider(data);
        if (rowCollider != null && rowCollider.enabled && frustumPlanes != null)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, rowCollider.bounds))
            {
                return true;
            }
        }

        Vector3 centerVp = cam.WorldToViewportPoint(row.transform.position);
        if (IsViewportPointVisible(centerVp, viewportPadding))
        {
            return true;
        }

        // Strict mode: cull aggressively by frustum+center checks only.
        if (strictViewport)
        {
            return false;
        }

        Bounds sampleBounds;
        if (TryGetRowBounds(data, out sampleBounds))
        {
            return IsAnyBoundPointVisible(cam, sampleBounds, viewportPadding);
        }

        return false;
    }

    private bool TryGetRowBounds(BlockRowData data, out Bounds bounds)
    {
        bounds = default;
        if (data == null || data.row == null)
        {
            return false;
        }

        Collider rowCollider = GetCachedRowCollider(data);
        if (rowCollider != null && rowCollider.enabled)
        {
            bounds = rowCollider.bounds;
            return true;
        }

        Renderer rowRenderer = GetCachedRowRenderer(data);
        if (rowRenderer != null)
        {
            bounds = rowRenderer.bounds;
            return true;
        }

        Renderer[] childRenderers = GetCachedChildRenderers(data);
        if (childRenderers != null && childRenderers.Length > 0)
        {
            bool hasBound = false;
            for (int i = 0; i < childRenderers.Length; i++)
            {
                Renderer childRenderer = childRenderers[i];
                if (childRenderer == null)
                {
                    continue;
                }

                if (!hasBound)
                {
                    bounds = childRenderer.bounds;
                    hasBound = true;
                }
                else
                {
                    bounds.Encapsulate(childRenderer.bounds);
                }
            }

            if (hasBound)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAnyBoundPointVisible(Camera cam, Bounds bounds, float viewportPadding)
    {
        Vector3 center = bounds.center;
        Vector3 ext = bounds.extents;

        Vector3[] samplePoints =
        {
            center,
            center + new Vector3(ext.x, ext.y, ext.z),
            center + new Vector3(ext.x, ext.y, -ext.z),
            center + new Vector3(ext.x, -ext.y, ext.z),
            center + new Vector3(ext.x, -ext.y, -ext.z),
            center + new Vector3(-ext.x, ext.y, ext.z),
            center + new Vector3(-ext.x, ext.y, -ext.z),
            center + new Vector3(-ext.x, -ext.y, ext.z),
            center + new Vector3(-ext.x, -ext.y, -ext.z)
        };

        for (int i = 0; i < samplePoints.Length; i++)
        {
            Vector3 vp = cam.WorldToViewportPoint(samplePoints[i]);
            if (IsViewportPointVisible(vp, viewportPadding))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsViewportPointVisible(Vector3 viewportPoint, float viewportPadding)
    {
        return viewportPoint.z > 0f
               && viewportPoint.x >= -viewportPadding
               && viewportPoint.x <= 1f + viewportPadding
               && viewportPoint.y >= -viewportPadding
               && viewportPoint.y <= 1f + viewportPadding;
    }

    private static void RemoveNullRowsFromActiveList(List<BlockRowData> rows)
    {
        if (rows == null)
        {
            return;
        }

        for (int i = rows.Count - 1; i >= 0; i--)
        {
            BlockRowData data = rows[i];
            if (data == null || data.row == null)
            {
                rows.RemoveAt(i);
            }
        }
    }

    private static void ReparentRowsToRoute(List<BlockRowData> rows, Transform routeTransform)
    {
        if (rows == null || routeTransform == null)
        {
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            BlockRowData data = rows[i];
            if (data == null || data.row == null)
            {
                continue;
            }

            DOTween.Kill(data.row.transform);
            data.row.transform.SetParent(routeTransform, true);
        }
    }

    // â”€â”€ Internal: Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    BlockRowData FindFirstRowWithSeeds(int minSeeds)
    {
        foreach (var d in activeRows)
        {
            if (d.row == null) continue;
            BlockRowSeedSpawner s = GetCachedSeeder(d);
            if (s != null && s.GetSeedCount() >= minSeeds) return d;
        }
        return null;
    }

    private BlockRowSeedSpawner GetCachedSeeder(BlockRowData data)
    {
        if (data == null || data.row == null)
        {
            return null;
        }

        if (data.seeder == null)
        {
            data.seeder = data.row.GetComponent<BlockRowSeedSpawner>();
        }

        return data.seeder;
    }

    private Collider GetCachedRowCollider(BlockRowData data)
    {
        if (data == null || data.row == null)
        {
            return null;
        }

        if (data.rowCollider == null)
        {
            data.rowCollider = data.row.GetComponent<Collider>();
        }

        return data.rowCollider;
    }

    private Renderer GetCachedRowRenderer(BlockRowData data)
    {
        if (data == null || data.row == null)
        {
            return null;
        }

        if (data.rowRenderer == null)
        {
            data.rowRenderer = data.row.GetComponent<Renderer>();
        }

        return data.rowRenderer;
    }

    private Renderer[] GetCachedChildRenderers(BlockRowData data)
    {
        if (data == null || data.row == null)
        {
            return null;
        }

        if (data.childRenderers == null || data.childRenderers.Length == 0)
        {
            data.childRenderers = data.row.GetComponentsInChildren<Renderer>(true);
        }

        return data.childRenderers;
    }

    void HandleDepletedRow(BlockRowData depleted)
    {
        int idx = activeRows.IndexOf(depleted);
        if (idx < 0) return;

        if (depleted.row != null)
            ReleaseRowWithSeeds(depleted.row);

        activeRows.RemoveAt(idx);

        if (routeMode == RouteMode.Side)
        {
            float dur = GetSyncedShiftDuration();
            if (dur > 0f) AnimateStaticPositions(dur);
            else          RebuildStaticPositions();
        }
    }

    float GetSyncedShiftDuration()
    {
        // Side queue má»—i láº§n refill sáº½ tiáº¿n lÃªn Ä‘Ãºng 1 block row.
        // Äá»“ng bá»™ thá»i gian dá»‹ch nÃ y theo tá»‘c Ä‘á»™ thá»±c (Ä‘Ã£ tÃ­nh speed multiplier) cá»§a main route.
        float shiftDistance = Mathf.Max(0.0001f, blockRowLength);

        SplineRoute mainRouteForSync = ResolveMainRouteForSync();
        if (mainRouteForSync != null)
        {
            float effectiveMainSpeed = Mathf.Abs(mainRouteForSync.GetMoveSpeed() * SpeedMultiplierManager.GetEffectiveMultiplier());
            if (effectiveMainSpeed > 0.0001f)
            {
                return Mathf.Max(0.01f, shiftDistance / effectiveMainSpeed);
            }
        }

        float fallbackSpeed = Mathf.Abs(MoveSpeed * SpeedMultiplierManager.GetEffectiveMultiplier());
        if (fallbackSpeed > 0.0001f)
        {
            return Mathf.Max(0.01f, shiftDistance / fallbackSpeed);
        }

        return Mathf.Max(0.01f, QueueShiftDuration);
    }

    private SplineRoute ResolveMainRouteForSync()
    {
        if (cachedMainRouteForSync != null && cachedMainRouteForSync != this)
        {
            return cachedMainRouteForSync;
        }

        if (hasAttemptedMainRouteResolve)
        {
            return null;
        }

        hasAttemptedMainRouteResolve = true;

        Transform searchRoot = transform.root != null ? transform.root : transform;
        SplineRoute[] routes = searchRoot.GetComponentsInChildren<SplineRoute>(true);
        for (int i = 0; i < routes.Length; i++)
        {
            SplineRoute candidate = routes[i];
            if (candidate == null || candidate == this)
            {
                continue;
            }

            if (candidate.GetRouteMode() != RouteMode.Main)
            {
                continue;
            }

            cachedMainRouteForSync = candidate;
            return cachedMainRouteForSync;
        }

        return null;
    }

    private static void ReleaseRowWithSeeds(GameObject row)
    {
        if (row == null)
        {
            return;
        }

        DOTween.Kill(row.transform);

        BlockRowSeedSpawner seedSpawner = row.GetComponent<BlockRowSeedSpawner>();
        if (seedSpawner != null)
        {
            seedSpawner.ClearAllSeedsOnly(true);
        }

        if (Application.isPlaying)
        {
            ObjectPoolManager.ReturnObject(row, ObjectPoolManager.PoolType.BlockRow);
        }
        else
        {
            DestroyImmediate(row);
        }
    }

    void MeasureBlockRowLength()
    {
        if (BlockRowPrefab == null)
        {
            return;
        }

        var temp = Instantiate(BlockRowPrefab);
        var cols = temp.GetComponentsInChildren<Collider>();
        if (cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            foreach (var c in cols) b.Encapsulate(c.bounds);
            blockRowLength = b.size.z;
        }
        else
        {
            var rends = temp.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                blockRowLength = b.size.z;
            }
        }
        Destroy(temp);
    }

    void CalculateSpacing()
    {
        if (routeMode == RouteMode.Side)
        {
            spacing = Mathf.Max(0.0001f, FixedSpacing);
            return;
        }

        // Main: phÃ¢n bá»‘ Ä‘á»u trÃªn spline hoáº·c ná»‘i sÃ¡t nhau
        if (DistributeEvenly && splineLength > 0f && rowsToSpawn > 1)
            spacing = Mathf.Max(blockRowLength, splineLength / rowsToSpawn);
        else
            spacing = Mathf.Max(0.0001f, blockRowLength);
    }

    // â”€â”€ Gizmos â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    void OnDrawGizmosSelected()
    {
        if (spline == null) return;
        Gizmos.color = routeMode == RouteMode.Main ? Color.cyan : Color.yellow;
        Vector3 prev = spline.EvaluatePosition(0f);
        for (int i = 1; i <= 50; i++)
        {
            Vector3 cur = spline.EvaluatePosition(i / 50f);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spline.EvaluatePosition(routeMode == RouteMode.Side ? 1f : 0f), 0.25f);
    }
}


