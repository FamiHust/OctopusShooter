using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;

public class BlockRowSeedSpawner : MonoBehaviour
{
    private const int FixedRowCount = 5;

    [Header("Seed Spawning")]
    [SerializeField] private GameObject seedPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool useChildPositions = true; // Sá»­ dá»¥ng vá»‹ trÃ­ cÃ¡c child transforms


    [Header("Empty Slot Refill")]
    [SerializeField] private bool autoRefillEmptySlots = false;
    [SerializeField] private float refillCheckInterval = 0.25f;

    [Header("Child Transform Names")]
    [SerializeField] private string[] childNames = { "Row_0", "Row_1", "Row_2", "Row_3", "Row_4" };

    [Header("Seed Colors")]
    [SerializeField] private SeedColor color;
    [SerializeField] private bool useUniformColor = true; // Táº¥t cáº£ seeds cÃ¹ng mÃ u

    [Header("Render Optimization")]
    [SerializeField] private bool optimizeSeedRenderersForMobile = true;
    [SerializeField] private bool allowPbrProbeLightingForSeeds = true;
    [SerializeField] private bool disableSeedShadowCasting = true;
    [SerializeField] private bool disableSeedReceiveShadows = true;
    [SerializeField] private bool disableSeedLightProbes = false;
    [SerializeField] private bool disableSeedReflectionProbes = false;

    [Header("Sequential Destroy (Shooter)")]
    [SerializeField] private Ease destroyScaleEase = Ease.OutSine;
    [SerializeField] private float totalWaveDuration = 0f;
    [SerializeField] private float singleSeedDuration = 0.3f; // MÃ¬nh tÄƒng nháº¹ lÃªn 0.15s Ä‘á»ƒ hiá»‡u á»©ng Punch nhÃ¬n rÃµ hÆ¡n

    [Header("Debug")]
    [SerializeField] private bool enableVerboseRuntimeLogs = false;

    [Header("Fallback Positions (náº¿u khÃ´ng tÃ¬m tháº¥y children)")]
    [SerializeField]
    private Vector3[] fallbackPositions =
    {
        new Vector3(0, 0, 0),
        new Vector3(0.35f, 0, 0),
        new Vector3(-0.35f, 0, 0),
        new Vector3(0.7f, 0, 0),
        new Vector3(-0.7f, 0, 0)
    };

    private List<Transform> childTransforms = new List<Transform>();
    private List<GameObject> spawnedSeeds = new List<GameObject>();
    private bool initializedBySpawner = false;
    private float nextRefillCheckTime;
    private Coroutine sequentialDestroyCoroutine;
    private Tween activeSeedDestroySequence;
    private bool isDestroySequenceRunning;
    private int visualAimCursor;
    private bool seedVisualEnabled = true;
    private bool seedReferencesDirty = true;
    private readonly Dictionary<GameObject, SeedInfo> seedInfoCache = new Dictionary<GameObject, SeedInfo>();
    private readonly Dictionary<GameObject, Renderer[]> seedRendererCache = new Dictionary<GameObject, Renderer[]>();

    private bool ShouldLogVerboseRuntime()
    {
        return enableVerboseRuntimeLogs && (Application.isEditor || Debug.isDebugBuild);
    }

    private void LogVerboseRuntime(string message)
    {
        if (ShouldLogVerboseRuntime())
        {
            ;
        }
    }

    private void LogVerboseWarning(string message)
    {
        if (ShouldLogVerboseRuntime())
        {
            ;
        }
    }

    public bool IsDestroyingSeedsSequentially => isDestroySequenceRunning;

    /// <summary>
    /// Láº¥y seed tiáº¿p theo Ä‘á»ƒ lÃ m má»¥c tiÃªu visual cho Ä‘áº¡n (khÃ´ng thay Ä‘á»•i dá»¯ liá»‡u phÃ¡ há»§y).
    /// DÃ¹ng round-robin Ä‘á»ƒ Ä‘áº¡n phÃ¢n bá»‘ qua nhiá»u háº¡t, nhÃ¬n tá»± nhiÃªn hÆ¡n.
    /// </summary>
    public bool TryGetNextVisualTargetSeed(out Transform visualTarget)
    {
        visualTarget = null;
        SyncSeedReferencesWithHierarchy();

        if (spawnedSeeds == null || spawnedSeeds.Count == 0)
        {
            return false;
        }

        int count = spawnedSeeds.Count;
        if (count <= 0)
        {
            return false;
        }

        int start = visualAimCursor % count;
        for (int offset = 0; offset < count; offset++)
        {
            int idx = (start + offset) % count;
            GameObject seed = spawnedSeeds[idx];
            if (!IsSeedReferenceUsable(seed))
            {
                spawnedSeeds[idx] = null;
                continue;
            }

            if (seed != null)
            {
                visualTarget = seed.transform;
                visualAimCursor = (idx + 1) % count;
                return true;
            }
        }

        return false;
    }

    void Start()
    {
        if (initializedBySpawner)
        {
            nextRefillCheckTime = Time.time + refillCheckInterval;
            return;
        }

        if (spawnOnStart)
        {
            FindChildTransforms();
            SpawnSeeds();
        }

        nextRefillCheckTime = Time.time + refillCheckInterval;
    }

    void Update()
    {
        if (!autoRefillEmptySlots)
        {
            return;
        }

        if (Time.time < nextRefillCheckTime)
        {
            return;
        }

        nextRefillCheckTime = Time.time + Mathf.Max(0.05f, refillCheckInterval);
        FillEmptySlots();
    }

    void OnTransformChildrenChanged()
    {
        seedReferencesDirty = true;
    }

    void OnDestroy()
    {
        CancelPendingSeedDestroySequence();

        ClearAllSeedsOnly(true);
        seedInfoCache.Clear();
        seedRendererCache.Clear();
    }

    void OnDisable()
    {
        CancelPendingSeedDestroySequence();
    }

    private void CancelPendingSeedDestroySequence()
    {
        if (activeSeedDestroySequence != null && activeSeedDestroySequence.IsActive())
        {
            activeSeedDestroySequence.Kill(false);
        }

        activeSeedDestroySequence = null;

        if (sequentialDestroyCoroutine != null)
        {
            StopCoroutine(sequentialDestroyCoroutine);
            sequentialDestroyCoroutine = null;
        }

        isDestroySequenceRunning = false;
    }

    public void CancelPendingDestroySequenceFromExternal()
    {
        CancelPendingSeedDestroySequence();
    }

    void FindChildTransforms()
    {
        if (childTransforms == null)
        {
            childTransforms = new List<Transform>();
        }

        childTransforms.Clear();

        if (transform == null)
        {
            LogVerboseWarning("BlockRowSeedSpawner transform is null.");
            return;
        }

        if (childNames == null)
        {
            childNames = new string[0];
        }

        if (useChildPositions)
        {
            // TÃ¬m children theo tÃªn
            foreach (string childName in childNames)
            {
                if (string.IsNullOrEmpty(childName))
                {
                    continue;
                }

                Transform child = transform.Find(childName);
                if (child != null)
                {
                    childTransforms.Add(child);
                    LogVerboseRuntime($"Found child: {childName}");
                }
                else
                {
                    LogVerboseWarning($"Child '{childName}' not found in {gameObject.name}");
                }
            }

            // Náº¿u khÃ´ng tÃ¬m tháº¥y Ä‘á»§ children, tÃ¬m táº¥t cáº£ children
            if (childTransforms.Count < FixedRowCount)
            {
                LogVerboseWarning($"Only found {childTransforms.Count} named children. Searching all children...");
                childTransforms.Clear();

                for (int i = 0; i < transform.childCount && i < FixedRowCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child != null)
                    {
                        childTransforms.Add(child);
                        LogVerboseRuntime($"Using child: {child.name}");
                    }
                }
            }
        }

        // Fallback: táº¡o transforms táº¡i fallback positions
        if (childTransforms.Count < FixedRowCount)
        {
            LogVerboseWarning("Not enough child transforms found. Using fallback positions...");
            CreateFallbackTransforms();
        }

        seedReferencesDirty = true;
    }

    void CreateFallbackTransforms()
    {
        if (childTransforms == null)
        {
            childTransforms = new List<Transform>();
        }

        childTransforms.Clear();

        if (fallbackPositions == null)
        {
            fallbackPositions = new Vector3[0];
        }

        for (int i = 0; i < FixedRowCount; i++)
        {
            GameObject fallbackObj = new GameObject($"FallbackRow_{i}");
            fallbackObj.transform.SetParent(transform);

            Vector3 position = i < fallbackPositions.Length ?
                fallbackPositions[i] :
                new Vector3((i - 2) * 0.35f, 0, 0);

            fallbackObj.transform.localPosition = position;
            fallbackObj.transform.localRotation = Quaternion.identity;

            childTransforms.Add(fallbackObj.transform);
            LogVerboseRuntime($"Created fallback transform at {position}");
        }

        seedReferencesDirty = true;
    }

    void SpawnSeeds()
    {
        if (seedPrefab == null)
        {
            ;
            return;
        }

        if (childTransforms == null || childTransforms.Count == 0)
        {
            LogVerboseWarning("No child transforms available to spawn seeds.");
            return;
        }

        if (spawnedSeeds == null)
        {
            spawnedSeeds = new List<GameObject>();
        }

        seedInfoCache.Clear();
        spawnedSeeds.Clear();

        for (int i = 0; i < FixedRowCount; i++)
        {
            spawnedSeeds.Add(null);
        }

        for (int i = 0; i < childTransforms.Count && i < FixedRowCount; i++)
        {
            Transform spawnPoint = childTransforms[i];
            if (spawnPoint == null)
            {
                continue;
            }

            // Reuse seed Ä‘Ã£ cÃ³ táº¡i child Ä‘á»ƒ trÃ¡nh spawn chá»“ng gÃ¢y nháº¥p nhÃ¡y
            GameObject seed = GetExistingSeedAtSpawnPoint(spawnPoint);

            // Náº¿u chÆ°a cÃ³ thÃ¬ má»›i spawn seed má»›i
            if (seed == null)
            {
                seed = SpawnSeedObject(spawnPoint);
                if (seed == null)
                {
                    continue;
                }
            }

            // Set local position vá» zero Ä‘á»ƒ seed á»Ÿ Ä‘Ãºng vá»‹ trÃ­ child
            seed.transform.localPosition = Vector3.zero;
            seed.transform.localRotation = Quaternion.identity;

            // Set mÃ u cho seed
            SetSeedColor(seed, color);

            // Set seed info
            SeedInfo seedInfo = GetCachedSeedInfo(seed);
            if (seedInfo != null)
            {
                seedInfo.SetSeedData(color, i, gameObject.GetInstanceID());
            }

            // Set tÃªn cho seed
            seed.name = $"Seed_{i}_{color}";

            ConfigureSeedRenderersForBatching(seed);
            ApplySeedVisualState(seed);

            spawnedSeeds[i] = seed;
        }

        EnsureFullRowSeedSlots();
        if (ShouldLogVerboseRuntime())
        {
            LogVerboseRuntime($"Total seeds spawned: {GetSeedCount()}");
        }
    }

    private void EnsureFullRowSeedSlots()
    {
        const int maxFillPasses = 4;

        for (int pass = 0; pass < maxFillPasses; pass++)
        {
            seedReferencesDirty = true;
            SyncSeedReferencesWithHierarchy();

            int currentCount = CountSeedSlotsNoSync();
            if (currentCount >= FixedRowCount)
            {
                return;
            }

            int filled = FillEmptySlots();
            if (filled <= 0)
            {
                break;
            }
        }

        seedReferencesDirty = true;
        SyncSeedReferencesWithHierarchy();

        int finalCount = CountSeedSlotsNoSync();
        if (finalCount < FixedRowCount)
        {
            LogVerboseWarning($"[BlockRowSeedSpawner] {gameObject.name} khong du 5 seed sau khi fill. Current={finalCount}.");
        }
    }

    private int CountSeedSlotsNoSync()
    {
        if (spawnedSeeds == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < spawnedSeeds.Count; i++)
        {
            if (spawnedSeeds[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    GameObject GetExistingSeedAtSpawnPoint(Transform spawnPoint)
    {
        if (spawnPoint == null) return null;

        for (int i = 0; i < spawnPoint.childCount; i++)
        {
            Transform child = spawnPoint.GetChild(i);
            if (child == null) continue;

            GameObject candidate = child.gameObject;
            if (!candidate.activeInHierarchy)
            {
                continue;
            }

            SeedInfo seedInfo = GetCachedSeedInfo(candidate);
            if (seedInfo != null && !seedInfo.IsDestroyed())
            {
                return candidate;
            }
        }

        return null;
    }

    SeedColor GetSeedColorForSlot(int slotIndex)
    {
        return color;
    }

    private GameObject SpawnSeedObject(Transform spawnPoint)
    {
        if (seedPrefab == null || spawnPoint == null)
        {
            return null;
        }

        GameObject seed;
        if (Application.isPlaying)
        {
            seed = ObjectPoolManager.SpawnObject(seedPrefab, spawnPoint, ObjectPoolManager.PoolType.Seed);
        }
        else
        {
            seed = Instantiate(seedPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        }

        if (seed == null)
        {
            return null;
        }

        seed.transform.SetParent(spawnPoint, false);
        seed.transform.localPosition = Vector3.zero;
        seed.transform.localRotation = Quaternion.identity;
        seed.transform.localScale = Vector3.one;
        seed.transform.DOKill();
        return seed;
    }

    private void ReleaseSeedObject(GameObject seed)
    {
        ReleaseSeedObject(seed, true);
    }

    private void ReleaseSeedObject(GameObject seed, bool returnToPool)
    {
        if (seed == null)
        {
            return;
        }

        seedInfoCache.Remove(seed);
        seedRendererCache.Remove(seed);

        seed.transform.DOKill();
        seed.transform.localScale = Vector3.one;
        seed.transform.SetParent(null, true);

        if (Application.isPlaying)
        {
            if (!returnToPool)
            {
                Destroy(seed);
                return;
            }

            if (seedPrefab != null)
            {
                seed.name = seedPrefab.name;
            }

            ObjectPoolManager.ReturnObject(seed, ObjectPoolManager.PoolType.Seed);
        }
        else
        {
            DestroyImmediate(seed);
        }
    }

    GameObject SpawnSeedAtSlot(int slotIndex, SeedColor? overrideColor = null)
    {
        if (seedPrefab == null) return null;
        if (childTransforms == null) return null;
        if (slotIndex < 0 || slotIndex >= childTransforms.Count || slotIndex >= FixedRowCount) return null;

        Transform spawnPoint = childTransforms[slotIndex];
        if (spawnPoint == null) return null;

        GameObject seed = GetExistingSeedAtSpawnPoint(spawnPoint);
        if (seed == null)
        {
            seed = SpawnSeedObject(spawnPoint);
            if (seed == null) return null;
        }

        seed.transform.localPosition = Vector3.zero;
        seed.transform.localRotation = Quaternion.identity;

        SeedColor seedColor = overrideColor.HasValue
            ? overrideColor.Value
            : GetSeedColorForSlot(slotIndex);
        SetSeedColor(seed, seedColor);

        SeedInfo seedInfo = GetCachedSeedInfo(seed);
        if (seedInfo != null)
        {
            seedInfo.SetSeedData(seedColor, slotIndex, gameObject.GetInstanceID());
        }

        seed.name = $"Seed_{slotIndex}_{seedColor}";
        ConfigureSeedRenderersForBatching(seed);
        ApplySeedVisualState(seed);
        return seed;
    }

    void EnsureSeedSlotsInitialized()
    {
        if (spawnedSeeds == null)
        {
            spawnedSeeds = new List<GameObject>();
        }

        while (spawnedSeeds.Count < FixedRowCount)
        {
            spawnedSeeds.Add(null);
        }
    }

    private SeedInfo GetCachedSeedInfo(GameObject seed)
    {
        if (seed == null)
        {
            return null;
        }

        if (seedInfoCache.TryGetValue(seed, out SeedInfo cached) && cached != null)
        {
            return cached;
        }

        SeedInfo seedInfo = seed.GetComponent<SeedInfo>();
        if (seedInfo != null)
        {
            seedInfoCache[seed] = seedInfo;
        }

        return seedInfo;
    }

    void SyncSeedReferencesWithHierarchy()
    {
        if (!seedReferencesDirty)
        {
            if (!HasInvalidSeedReferences())
            {
                return;
            }

            seedReferencesDirty = true;
        }

        if (!seedReferencesDirty)
        {
            return;
        }

        EnsureSeedSlotsInitialized();

        if (childTransforms == null || childTransforms.Count == 0)
        {
            FindChildTransforms();
        }

        for (int i = 0; i < FixedRowCount; i++)
        {
            if (i < childTransforms.Count && childTransforms[i] != null)
            {
                GameObject existing = GetExistingSeedAtSpawnPoint(childTransforms[i]);
                if (existing != null)
                {
                    spawnedSeeds[i] = existing;
                    continue;
                }
            }

            spawnedSeeds[i] = null;
        }

        seedReferencesDirty = false;
    }

    private bool HasInvalidSeedReferences()
    {
        if (spawnedSeeds == null)
        {
            return true;
        }

        if (spawnedSeeds.Count < FixedRowCount)
        {
            return true;
        }

        for (int i = 0; i < FixedRowCount; i++)
        {
            GameObject seed = spawnedSeeds[i];
            if (seed == null)
            {
                continue;
            }

            if (!IsSeedReferenceUsable(seed))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSeedReferenceUsable(GameObject seed)
    {
        if (seed == null)
        {
            return false;
        }

        if (!seed.activeInHierarchy)
        {
            return false;
        }

        if (!seed.transform.IsChildOf(transform))
        {
            return false;
        }

        SeedInfo seedInfo = GetCachedSeedInfo(seed);
        if (seedInfo != null && seedInfo.IsDestroyed())
        {
            return false;
        }

        return true;
    }

    Color GetUnityColor(SeedColor seedColor)
    {
        return ColorInfo.GetUnityColor(seedColor);
    }

    void SetSeedColor(GameObject seed, SeedColor color)
    {
        if (seed == null) return;

        // Update tÃªn object Ä‘á»ƒ dá»… debug
        seed.name = $"Seed_{color}";

        // Add hoáº·c update SeedInfo component Ä‘á»ƒ store color info
        SeedInfo seedInfo = GetCachedSeedInfo(seed);
        if (seedInfo == null)
        {
            seedInfo = seed.AddComponent<SeedInfo>();
            seedInfoCache[seed] = seedInfo;
        }
        seedInfo.SetSeedColor(color);
    }

    void ClearExistingSeeds()
    {
        if (spawnedSeeds == null)
        {
            spawnedSeeds = new List<GameObject>();
            return;
        }

        foreach (GameObject seed in spawnedSeeds)
        {
            if (seed != null)
            {
                // Mark as destroyed in SeedInfo before destroying
                SeedInfo seedInfo = GetCachedSeedInfo(seed);
                if (seedInfo != null)
                {
                    seedInfo.MarkAsDestroyed();
                }
            }
        }
        spawnedSeeds.Clear();
    }

    // =========================================================================
    // [PHáº¦N Má»šI THÃŠM] - CÃC HÃ€M PHá»¤C Vá»¤ CHO SÃšNG Báº®N Tá»ªNG Háº T (SHOOTER)
    // =========================================================================

    /// <summary>
    /// Tráº£ vá» háº¡t Ä‘áº§u tiÃªn cÃ²n sá»‘ng Ä‘á»ƒ sÃºng nháº¯m tá»›i, Ä‘á»“ng thá»i loáº¡i háº¡t nÃ y khá»i danh sÃ¡ch quáº£n lÃ½ ná»™i bá»™.
    /// Nhá» Ä‘Ã³ cÃ¡c viÃªn Ä‘áº¡n sau sáº½ khÃ´ng bá»‹ nháº¯m trÃ¹ng má»¥c tiÃªu vÃ o cÃ¹ng 1 háº¡t Ä‘ang bay trÃªn khÃ´ng.
    /// </summary>
    public GameObject GetNextAliveSeed()
    {
        SyncSeedReferencesWithHierarchy();

        for (int i = 0; i < spawnedSeeds.Count; i++)
        {
            GameObject seed = spawnedSeeds[i];
            if (seed != null)
            {

                spawnedSeeds[i] = null;
                return seed;
            }
        }
        return null;
    }

    /// <summary>
    /// ÄÆ°á»£c gá»i bá»Ÿi ViÃªn Ä‘áº¡n (Bullet) khi nÃ³ bay trÃºng má»¥c tiÃªu Ä‘á»ƒ thá»±c sá»± phÃ¡ há»§y háº¡t.
    /// </summary>
    public void DestroySpecificSeed(GameObject targetSeed)
    {
        if (targetSeed == null) return;

        SeedInfo seedInfo = GetCachedSeedInfo(targetSeed);
        if (seedInfo != null)
        {
            seedInfo.MarkAsDestroyed();
        }

        if (Application.isPlaying)
        {
            targetSeed.transform.DOScale(Vector3.zero, 0.05f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                ReleaseSeedObject(targetSeed);

            });

        }
        else
        {
            ReleaseSeedObject(targetSeed);
        }
    }
    public bool TryStartDestroyAllSeedsSequential(System.Action onCompleted = null)
    {
        return TryStartDestroyAllSeedsSequential(out _, int.MaxValue, null, onCompleted);
    }

    public bool TryStartDestroyAllSeedsSequential(out int queuedSeedCount, System.Action onCompleted = null)
    {
        return TryStartDestroyAllSeedsSequential(out queuedSeedCount, int.MaxValue, null, onCompleted);
    }

    public bool TryStartDestroyAllSeedsSequential(out int queuedSeedCount, int maxSeedsToQueue, System.Action<Transform, int> onSeedDestroyStarted, System.Action onCompleted = null)
    {
        queuedSeedCount = 0;
        maxSeedsToQueue = Mathf.Max(0, maxSeedsToQueue);

        if (isDestroySequenceRunning) return false;
        if (maxSeedsToQueue <= 0) return false;
        if (GetSeedCount() <= 0)
        {
            onCompleted?.Invoke();
            return false;
        }

        SyncSeedReferencesWithHierarchy();
        isDestroySequenceRunning = true;

        // 1. TÃNH TOÃN THá»œI GIAN THEO MULTIPLIER
        float mul = SpeedMultiplierManager.GetBaseMultiplier();
        float duration = totalWaveDuration / Mathf.Max(0.1f, mul);
        float singleDuration = singleSeedDuration / Mathf.Max(0.1f, mul);

        Sequence waveSequence = DOTween.Sequence();
        activeSeedDestroySequence = waveSequence;

        // totalWaveDuration Ä‘iá»u khiá»ƒn nhá»‹p wave, nhÆ°ng luÃ´n giá»¯ stagger delay > 0
        // Ä‘á»ƒ háº¡t khÃ´ng bá»‹ trigger cÃ¹ng lÃºc khi singleDuration > totalWaveDuration.
        float waveDelay = 0f;
        if (FixedRowCount > 1)
        {
            float calculatedWaveDelay = (duration - singleDuration) / (FixedRowCount - 1);
            float minStaggerDelay = singleDuration * 0.2f;
            float maxStaggerDelay = singleDuration * 0.9f; // luÃ´n overlap, khÃ´ng chá» tuáº§n tá»± tá»«ng háº¡t xong háº³n.

            waveDelay = calculatedWaveDelay <= 0f
                ? minStaggerDelay
                : Mathf.Clamp(calculatedWaveDelay, minStaggerDelay, maxStaggerDelay);
        }

        bool hasSeedsToDestroy = false;
        int destroyOrder = 0;

        float punchTime = singleDuration * 0.4f;
        float shrinkTime = Mathf.Max(0.02f, singleDuration - punchTime);

        for (int i = 0; i < FixedRowCount; i++)
        {
            if (queuedSeedCount >= maxSeedsToQueue)
            {
                break;
            }

            GameObject seed = GetSeed(i);
            if (seed == null) continue;

            hasSeedsToDestroy = true;
            queuedSeedCount++;
            int index = i;
            int seedOrder = destroyOrder;
            destroyOrder++;
            Transform seedTransform = seed.transform;
            float insertTime = i * waveDelay;

            // BÃ¡o vá» shooter ngay khi háº¡t nÃ y báº¯t Ä‘áº§u vÃ o pha xÃ³a.
            waveSequence.InsertCallback(insertTime, () =>
            {
                onSeedDestroyStarted?.Invoke(seedTransform, seedOrder);
            });

            // 1. NhÃºn to ra (PunchScale). Lá»±c punch lÃ  Vector3.one * 0.5f (phÃ¬nh to 50%)
            if (punchTime > 0.0001f)
            {
                waveSequence.Insert(insertTime, seed.transform.DOPunchScale(Vector3.up * 0.5f, punchTime)
                .SetEase(Ease.OutBack));
            }

            // 2. Scale vá» 0 theo OutBack
            float shrinkStartTime = insertTime + punchTime;
            waveSequence.Insert(shrinkStartTime, seed.transform.DOScale(Vector3.zero, shrinkTime).SetEase(destroyScaleEase).OnComplete(() =>
            {
                SeedInfo seedInfo = GetCachedSeedInfo(seed);
                if (seedInfo != null)
                {
                    seedInfo.MarkAsDestroyed();
                }

                spawnedSeeds[index] = null;
                ReleaseSeedObject(seed);
            }));
        }

        if (!hasSeedsToDestroy)
        {
            isDestroySequenceRunning = false;
            onCompleted?.Invoke();
            return false;
        }

        // ==========================================
        // CÃš TRICK TÄ‚NG Tá»C CHO SHOOTER á»ž ÄÃ‚Y!
        // ==========================================

        // Chá» WaveSequence (Visual) cháº¡y xong tháº­t thÃ¬ má»›i reset cá» tráº¡ng thÃ¡i + callback.
        waveSequence.OnComplete(() =>
        {
            isDestroySequenceRunning = false;
            if (ReferenceEquals(activeSeedDestroySequence, waveSequence))
            {
                activeSeedDestroySequence = null;
            }
            onCompleted?.Invoke();
        });

        waveSequence.OnKill(() =>
        {
            if (ReferenceEquals(activeSeedDestroySequence, waveSequence))
            {
                activeSeedDestroySequence = null;
            }

            isDestroySequenceRunning = false;
        });

        return true;
    }

    // =========================================================================
    // [CÃC PHáº¦N BÃŠN DÆ¯á»šI GIá»® NGUYÃŠN HOÃ€N TOÃ€N Cá»¦A Báº N]
    // =========================================================================

    // Public methods
    public void InitializeFromSpawner(GameObject newSeedPrefab, SeedColor color, bool uniformColor = true, bool respawnNow = true)
    {
        CancelPendingSeedDestroySequence();

        initializedBySpawner = true;
        seedPrefab = newSeedPrefab;
        useUniformColor = uniformColor;
        this.color = color;

        if (respawnNow)
        {
            RespawnSeeds();
        }
    }

    public void RespawnSeeds()
    {
        CancelPendingSeedDestroySequence();
        FindChildTransforms();
        SpawnSeeds();
    }

    public int FillEmptySlots()
    {
        SyncSeedReferencesWithHierarchy();

        int filledCount = 0;
        for (int i = 0; i < FixedRowCount; i++)
        {
            if (spawnedSeeds[i] != null)
            {
                continue;
            }

            GameObject seed = SpawnSeedAtSlot(i);
            if (seed != null)
            {
                spawnedSeeds[i] = seed;
                filledCount++;
            }
        }

        return filledCount;
    }

    public int GetFirstEmptySlotIndex()
    {
        SyncSeedReferencesWithHierarchy();

        for (int i = 0; i < FixedRowCount; i++)
        {
            if (i >= spawnedSeeds.Count || spawnedSeeds[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    public bool HasEmptySlot()
    {
        return GetFirstEmptySlotIndex() >= 0;
    }

    /// <summary>
    /// Chá»‰ tá»‘i Æ°u pháº§n render (batches/tris), khÃ´ng Ä‘á»¥ng logic seed count/collision.
    /// </summary>
    public void SetSeedVisualEnabled(bool enabled)
    {
        if (seedVisualEnabled == enabled)
        {
            return;
        }

        seedVisualEnabled = enabled;
        SyncSeedReferencesWithHierarchy();

        for (int i = 0; i < spawnedSeeds.Count; i++)
        {
            GameObject seed = spawnedSeeds[i];
            if (seed == null)
            {
                continue;
            }

            ApplySeedVisualState(seed);
        }
    }

    private void ApplySeedVisualState(GameObject seed)
    {
        if (seed == null)
        {
            return;
        }

        Renderer[] renderers = GetCachedSeedRenderers(seed);
        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            if (renderer != null)
            {
                renderer.enabled = seedVisualEnabled;
            }
        }
    }

    private Renderer[] GetCachedSeedRenderers(GameObject seed)
    {
        if (seed == null)
        {
            return System.Array.Empty<Renderer>();
        }

        if (seedRendererCache.TryGetValue(seed, out Renderer[] cached) && cached != null && cached.Length > 0)
        {
            return cached;
        }

        Renderer[] renderers = seed.GetComponentsInChildren<Renderer>(true);
        if (renderers == null)
        {
            renderers = System.Array.Empty<Renderer>();
        }

        seedRendererCache[seed] = renderers;
        return renderers;
    }

    private void ConfigureSeedRenderersForBatching(GameObject seed)
    {
        if (!optimizeSeedRenderersForMobile || seed == null)
        {
            return;
        }

        Renderer[] renderers = GetCachedSeedRenderers(seed);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (allowPbrProbeLightingForSeeds)
            {
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            }
            else
            {
                if (disableSeedLightProbes)
                {
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                }

                if (disableSeedReflectionProbes)
                {
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }

            if (renderer is MeshRenderer meshRenderer)
            {
                if (disableSeedShadowCasting)
                {
                    meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                if (disableSeedReceiveShadows)
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

    public bool FillSlotWithColor(int slotIndex, SeedColor fillColor)
    {
        if (slotIndex < 0 || slotIndex >= FixedRowCount)
        {
            return false;
        }

        SyncSeedReferencesWithHierarchy();
        EnsureSeedSlotsInitialized();

        if (spawnedSeeds[slotIndex] != null)
        {
            return true;
        }

        GameObject seed = SpawnSeedAtSlot(slotIndex, fillColor);
        if (seed == null)
        {
            return false;
        }

        spawnedSeeds[slotIndex] = seed;
        // Cáº­p nháº­t mÃ u cá»§a row Ä‘á»ƒ GetCurrentColor() tráº£ vá» Ä‘Ãºng khi shooter check IsTargetValid
        color = fillColor;
        return true;
    }

    public void SetSeedPrefab(GameObject newPrefab)
    {
        seedPrefab = newPrefab;
    }

    public void SetColor(SeedColor newColor)
    {
        color = newColor;

        if (spawnedSeeds.Count > 0 && useUniformColor)
        {
            foreach (GameObject seed in spawnedSeeds)
            {
                if (seed != null)
                {
                    SetSeedColor(seed, color);
                }
            }
        }

        LogVerboseRuntime($"Color set to {color}");
    }

    public SeedColor GetColor()
    {
        return color;
    }

    public SeedColor GetCurrentColor()
    {
        return color;
    }

    public void SetUseUniformColor(bool uniform)
    {
        useUniformColor = uniform;

        if (spawnedSeeds.Count > 0)
        {
            RespawnSeeds();
        }
    }

    public List<GameObject> GetSpawnedSeeds()
    {
        if (spawnedSeeds == null)
        {
            spawnedSeeds = new List<GameObject>();
        }
        return new List<GameObject>(spawnedSeeds);
    }

    public GameObject GetSeed(int index)
    {
        if (spawnedSeeds == null)
        {
            return null;
        }

        SyncSeedReferencesWithHierarchy();

        if (index >= 0 && index < spawnedSeeds.Count)
        {
            return spawnedSeeds[index];
        }
        return null;
    }

    public Transform GetSlotTransform(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= FixedRowCount)
        {
            return null;
        }

        if (childTransforms == null || childTransforms.Count == 0)
        {
            FindChildTransforms();
        }

        if (childTransforms == null || slotIndex >= childTransforms.Count)
        {
            return null;
        }

        return childTransforms[slotIndex];
    }

    public bool DetachSeedAtSlot(int slotIndex, out GameObject seed, bool keepWorldTransform = true)
    {
        seed = null;

        if (slotIndex < 0 || slotIndex >= FixedRowCount)
        {
            return false;
        }

        SyncSeedReferencesWithHierarchy();
        EnsureSeedSlotsInitialized();

        if (slotIndex >= spawnedSeeds.Count)
        {
            return false;
        }

        seed = spawnedSeeds[slotIndex];
        if (seed == null)
        {
            return false;
        }

        seed.transform.DOKill();
        seed.transform.SetParent(null, keepWorldTransform);
        spawnedSeeds[slotIndex] = null;
        return true;
    }

    public bool AttachSeedToSlot(int slotIndex, GameObject seed, bool keepWorldTransform = true)
    {
        if (seed == null || slotIndex < 0 || slotIndex >= FixedRowCount)
        {
            return false;
        }

        SyncSeedReferencesWithHierarchy();
        EnsureSeedSlotsInitialized();

        Transform slotTransform = GetSlotTransform(slotIndex);
        if (slotTransform == null)
        {
            return false;
        }

        if (slotIndex < spawnedSeeds.Count && spawnedSeeds[slotIndex] != null && spawnedSeeds[slotIndex] != seed)
        {
            return false;
        }

        seed.transform.DOKill();
        seed.transform.SetParent(slotTransform, keepWorldTransform);
        seed.transform.localPosition = Vector3.zero;
        seed.transform.localRotation = Quaternion.identity;

        if (seed.transform.localScale.sqrMagnitude <= 0.0001f)
        {
            seed.transform.localScale = Vector3.one;
        }

        if (slotIndex >= spawnedSeeds.Count)
        {
            while (spawnedSeeds.Count <= slotIndex)
            {
                spawnedSeeds.Add(null);
            }
        }

        spawnedSeeds[slotIndex] = seed;

        SeedInfo seedInfo = GetCachedSeedInfo(seed);
        if (seedInfo != null)
        {
            SeedColor seedColor = seedInfo.GetSeedColor();
            seedInfo.SetSeedData(seedColor, slotIndex, gameObject.GetInstanceID());
            color = seedColor;
        }

        ConfigureSeedRenderersForBatching(seed);
        ApplySeedVisualState(seed);
        seedReferencesDirty = false;
        return true;
    }

    public void DestroySeed(int index)
    {
        if (spawnedSeeds == null)
        {
            return;
        }

        SyncSeedReferencesWithHierarchy();

        if (index >= 0 && index < spawnedSeeds.Count && spawnedSeeds[index] != null)
        {
            GameObject seedToRelease = spawnedSeeds[index];

            // Mark as destroyed in SeedInfo
            SeedInfo seedInfo = GetCachedSeedInfo(seedToRelease);
            if (seedInfo != null)
            {
                seedInfo.MarkAsDestroyed();
            }

            ReleaseSeedObject(seedToRelease);

            spawnedSeeds[index] = null;
        }
    }

    public void DestroySeed(GameObject seed)
    {
        int index = spawnedSeeds.IndexOf(seed);
        if (index >= 0)
        {
            DestroySeed(index);
        }
    }

    public void ClearAllSeedsOnly(bool destroySeedObjects = true)
    {
        CancelPendingSeedDestroySequence();

        // Force rescan to catch seeds that were temporarily removed from spawnedSeeds list during targeting.
        seedReferencesDirty = true;
        SyncSeedReferencesWithHierarchy();

        int aliveSeedCount = 0;
        for (int i = 0; i < FixedRowCount; i++)
        {
            if (i < spawnedSeeds.Count && spawnedSeeds[i] != null)
            {
                aliveSeedCount++;
            }
        }

        // Only keep pooling when row still has a complete group.
        bool canReturnToPoolAsGroup = aliveSeedCount == FixedRowCount;

        for (int i = 0; i < FixedRowCount; i++)
        {
            if (i >= spawnedSeeds.Count)
            {
                continue;
            }

            GameObject seed = spawnedSeeds[i];
            if (seed == null)
            {
                continue;
            }

            SeedInfo seedInfo = GetCachedSeedInfo(seed);
            if (seedInfo != null)
            {
                seedInfo.MarkAsDestroyed();
            }

            if (destroySeedObjects)
            {
                ReleaseSeedObject(seed, canReturnToPoolAsGroup);
            }

            spawnedSeeds[i] = null;
        }
    }

    public bool TryPeekFirstSeedColor(out SeedColor seedColor)
    {
        seedColor = color;
        SyncSeedReferencesWithHierarchy();

        for (int i = 0; i < FixedRowCount; i++)
        {
            if (i >= spawnedSeeds.Count || spawnedSeeds[i] == null)
            {
                continue;
            }

            SeedInfo seedInfo = GetCachedSeedInfo(spawnedSeeds[i]);
            if (seedInfo != null)
            {
                seedColor = seedInfo.GetSeedColor();
                return true;
            }

            seedColor = color;
            return true;
        }

        return false;
    }

    public bool TryConsumeFirstSeed(out SeedColor seedColor, bool destroySeedObject = true)
    {
        Vector3 ignoredPosition;
        return TryConsumeFirstSeed(out seedColor, out ignoredPosition, destroySeedObject);
    }

    public bool TryConsumeFirstSeed(out SeedColor seedColor, out Vector3 consumedWorldPosition, bool destroySeedObject = true)
    {
        seedColor = color;
        consumedWorldPosition = Vector3.zero;
        SyncSeedReferencesWithHierarchy();

        for (int i = 0; i < FixedRowCount; i++)
        {
            if (i >= spawnedSeeds.Count || spawnedSeeds[i] == null)
            {
                continue;
            }

            GameObject seed = spawnedSeeds[i];
            consumedWorldPosition = seed.transform.position;
            SeedInfo seedInfo = GetCachedSeedInfo(seed);
            if (seedInfo != null)
            {
                seedColor = seedInfo.GetSeedColor();
                seedInfo.MarkAsDestroyed();
            }
            else
            {
                seedColor = color;
            }

            if (destroySeedObject)
            {
                ReleaseSeedObject(seed);
            }

            spawnedSeeds[i] = null;
            return true;
        }

        return false;
    }

    public int GetSeedCount()
    {
        if (spawnedSeeds == null)
        {
            return 0;
        }

        SyncSeedReferencesWithHierarchy();

        int count = 0;
        foreach (GameObject seed in spawnedSeeds)
        {
            if (seed != null) count++;
        }
        return count;
    }

    public int GetMaxSeedCount()
    {
        return FixedRowCount;
    }
    public GameObject GetSeedAtIndex(int index)
    {
        return spawnedSeeds[index];
    }



    // Debug methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogColorInfo()
    {
        ;
        ;
        ;
        ;
        ;

        // Log seed details
        for (int i = 0; i < spawnedSeeds.Count; i++)
        {
            if (spawnedSeeds[i] != null)
            {
                SeedInfo info = GetCachedSeedInfo(spawnedSeeds[i]);
                if (info != null)
                {
                    ;
                }
            }
        }
    }

    // Gizmos Ä‘á»ƒ visualize spawn points
    void OnDrawGizmosSelected()
    {
        if (childTransforms != null && childTransforms.Count > 0)
        {
            Gizmos.color = Color.green;
            foreach (Transform child in childTransforms)
            {
                if (child != null)
                {
                    Gizmos.DrawWireSphere(child.position, 0.1f);
                }
            }
        }
        else
        {
            // Draw fallback positions
            Gizmos.color = Color.yellow;
            if (fallbackPositions == null) return;

            for (int i = 0; i < fallbackPositions.Length && i < FixedRowCount; i++)
            {
                Vector3 worldPos = transform.TransformPoint(fallbackPositions[i]);
                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.1f);
            }
        }
    }

    void OnValidate()
    {
        refillCheckInterval = Mathf.Max(0.05f, refillCheckInterval);

        if (childNames == null || childNames.Length != FixedRowCount)
        {
            childNames = new string[FixedRowCount] { "Row_0", "Row_1", "Row_2", "Row_3", "Row_4" };
        }

        if (fallbackPositions == null || fallbackPositions.Length < FixedRowCount)
        {
            fallbackPositions = new Vector3[FixedRowCount]
            {
                new Vector3(0, 0, 0),
                new Vector3(0.35f, 0, 0),
                new Vector3(-0.35f, 0, 0),
                new Vector3(0.7f, 0, 0),
                new Vector3(-0.7f, 0, 0)
            };
        }
    }
}

