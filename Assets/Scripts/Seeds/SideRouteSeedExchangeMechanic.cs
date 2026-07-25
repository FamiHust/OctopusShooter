using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Serialization;

/// <summary>
/// Level mechanic: swaps all active seed rows between two side routes.
/// Works only when the level has exactly two side routes.
/// Attach this component to the level prefab and assign routes in inspector.
/// </summary>
public class SideRouteSeedExchangeMechanic : MonoBehaviour
{
    private static int activeExchangeMechanicCount;

    public static bool IsAnyExchangeInProgress => activeExchangeMechanicCount > 0;

    private sealed class BlackHoleVfxRuntimeState
    {
        public bool hasCachedDefaults;
        public readonly List<BlackHoleParticleAlphaState> particleAlphaStates = new List<BlackHoleParticleAlphaState>(8);
        public Vector3 originalScale = Vector3.one;
        public Vector3 originalLocalPosition = Vector3.zero;
        public float currentAlpha = 1f;
        public Tween fadeTween;
        public Tween scaleTween;
        public Tween moveTween;
    }

    private sealed class BlackHoleParticleAlphaState
    {
        public ParticleSystem particleSystem;
        public ParticleSystem.MinMaxGradient startColor;
        public ParticleSystem.Particle[] particlesBuffer;
        public ParticleSystemGradientMode colorMode;
        public Color cachedColor;
        public Color cachedColorMin;
        public Color cachedColorMax;
        public Gradient gradient;
        public Gradient gradientMin;
        public Gradient gradientMax;
        public GradientColorKey[] gradientColorKeys;
        public GradientColorKey[] gradientMinColorKeys;
        public GradientColorKey[] gradientMaxColorKeys;
        public GradientAlphaKey[] gradientAlphaKeys;
        public GradientAlphaKey[] gradientMinAlphaKeys;
        public GradientAlphaKey[] gradientMaxAlphaKeys;
    }

    private sealed class SeedFlightItem
    {
        public BlockRowSeedSpawner sourceSpawner;
        public int slotIndex;
        public GameObject seed;
        public Vector3 originalScale;
        public int rowOrder;
    }

    [Header("Route References (from level prefab)")]
    [SerializeField] private SplineRoute sideRouteA;
    [SerializeField] private SplineRoute sideRouteB;
    [SerializeField] private Transform blackHoleCenter;
    [SerializeField] private ParticleSystem portalVfx;
    [FormerlySerializedAs("blackHoleVfx")]
    [SerializeField] private ParticleSystem stoneVfx;

    [Header("Behavior")]
    [SerializeField] private bool triggerByPortalShooterEvent = true;
    [SerializeField] private bool requireExactlyTwoSideRoutesInLevel = true;
    [SerializeField, Min(0.05f)] private float referenceRefreshInterval = 0.5f;

    [Header("Black Hole Animation")]
    [SerializeField, Min(0.05f)] private float inwardDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float outwardDuration = 0.3f;
    [SerializeField, Min(0f)] private float inwardStaggerByIndex = 0.015f;
    [SerializeField, Min(0f)] private float outwardStaggerByIndex = 0.012f;
    [SerializeField, Min(0f)] private float inwardJumpPower = 0.55f;
    [SerializeField, Min(0f)] private float outwardJumpPower = 0.45f;
    [SerializeField, Min(1)] private int inwardJumpCount = 1;
    [SerializeField, Min(1)] private int outwardJumpCount = 1;
    [SerializeField, Range(0.01f, 1f)] private float minimumScaleRatioAtCenter = 0.15f;
    [SerializeField] private Ease inwardMoveEase = Ease.InBack;
    [SerializeField] private Ease outwardMoveEase = Ease.OutCubic;
    [SerializeField] private Ease inwardScaleEase = Ease.InQuad;
    [SerializeField] private Ease outwardScaleEase = Ease.OutBack;

    [Header("Black Hole VFX Intro")]
    [SerializeField, Min(0.01f)] private float blackHoleVfxIntroDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float blackHoleVfxOutroDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float blackHoleVfxIntroFadeDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float blackHoleVfxOutroFadeDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float blackHoleVfxMaxAlpha = 1f;
    [SerializeField] private Ease blackHoleVfxScaleEase = Ease.OutBack;
    [SerializeField] private Ease blackHoleVfxFadeEase = Ease.OutQuad;
    [SerializeField] private Ease blackHoleVfxOutroScaleEase = Ease.InBack;
    [SerializeField] private Ease blackHoleVfxOutroFadeEase = Ease.InQuad;

    [Header("Black Hole Row SFX")]
    [SerializeField, Min(0f)] private float absorbBlockSfxCooldown = 0.12f;
    [SerializeField, Min(0f)] private float releaseBlockSfxCooldown = 0.12f;

    private SplineController splineController;
    private GameEventHub eventHub;
    private bool isListeningPortalSwapEvent;
    private bool hasPendingExchangeRequest;
    private bool isExchangeInProgress;
    private bool routesPausedByMechanic;
    private bool hasRegisteredAsActiveExchange;
    private Coroutine activeExchangeRoutine;
    private Tween activeExchangeSpeedScaledTween;
    private float nextReferenceRefreshTime;
    private readonly List<SeedFlightItem> flightItemsBuffer = new List<SeedFlightItem>(512);
    private readonly Stack<SeedFlightItem> flightItemPool = new Stack<SeedFlightItem>(512);
    private readonly List<SplineRoute> pauseRoutesBuffer = new List<SplineRoute>(4);
    private readonly BlackHoleVfxRuntimeState portalVfxState = new BlackHoleVfxRuntimeState();

    private void OnEnable()
    {
        ResolveSplineControllerReference();
        ResolveEventHubAndBind();
        RefreshSideRouteReferencesFromController();
        nextReferenceRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, referenceRefreshInterval);
    }

    private void OnDisable()
    {
        AbortExchangeForLevelCleanup();

        splineController = null;
        UnbindEventHub();
    }

    private void Update()
    {
        bool shouldRefreshReferences = hasPendingExchangeRequest ||
                                      splineController == null ||
                                      eventHub == null ||
                                      (triggerByPortalShooterEvent && !isListeningPortalSwapEvent) ||
                                      Time.unscaledTime >= nextReferenceRefreshTime;

        if (shouldRefreshReferences)
        {
            ResolveSplineControllerReference();
            ResolveEventHubAndBind();
            RefreshSideRouteReferencesFromController();
            nextReferenceRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, referenceRefreshInterval);
        }

        if (hasPendingExchangeRequest)
        {
            TryProcessPendingExchange();
        }

        ApplyExchangeAnimationSpeedScale();
    }

    public bool TryExecuteExchange()
    {
        if (!IsSetupValid())
        {
            return false;
        }

        if (isExchangeInProgress)
        {
            hasPendingExchangeRequest = true;
            return false;
        }

        if (IsRefillBusy())
        {
            hasPendingExchangeRequest = true;
            return false;
        }

        hasPendingExchangeRequest = true;
        TryProcessPendingExchange();
        return true;
    }

    [ContextMenu("Exchange Side Route Seeds Now")]
    private void ExchangeNowFromContextMenu()
    {
        hasPendingExchangeRequest = true;
        TryProcessPendingExchange();
    }

    private bool ExecuteExchangeNow()
    {
        if (!IsSetupValid())
        {
            return false;
        }

        if (isExchangeInProgress)
        {
            return false;
        }

        activeExchangeRoutine = StartCoroutine(ExecuteBlackHoleExchangeRoutine());
        return true;
    }

    public void AbortExchangeForLevelCleanup()
    {
        hasPendingExchangeRequest = false;

        if (activeExchangeRoutine != null)
        {
            StopCoroutine(activeExchangeRoutine);
            activeExchangeRoutine = null;
        }

        KillActiveExchangeSpeedScaledTween();

        KillBlackHoleVfxTweens(portalVfxState);

        RestoreDetachedSeedsOrReleaseToPool();

        if (routesPausedByMechanic)
        {
            SetAllRoutesMechanicPaused(false);
        }

        isExchangeInProgress = false;
        UnregisterActiveExchangeGlobal();
    }

    private void TryProcessPendingExchange()
    {
        if (!hasPendingExchangeRequest)
        {
            return;
        }

        if (isExchangeInProgress)
        {
            return;
        }

        if (!IsSetupValid())
        {
            hasPendingExchangeRequest = false;
            return;
        }

        if (IsRefillBusy())
        {
            return;
        }

        ExecuteExchangeNow();
    }

    private void OnPortalShooterSwapRequested(object data)
    {
        if (!triggerByPortalShooterEvent)
        {
            return;
        }

        if (!(data is BaseShooter))
        {
            return;
        }

        hasPendingExchangeRequest = true;
        TryProcessPendingExchange();
    }

    private IEnumerator ExecuteBlackHoleExchangeRoutine()
    {
        if (!IsSetupValid())
        {
            hasPendingExchangeRequest = false;
            activeExchangeRoutine = null;
            yield break;
        }

        isExchangeInProgress = true;
        RegisterActiveExchangeGlobal();
        Transform centerTransform = blackHoleCenter != null ? blackHoleCenter : transform;
        bool pausedRoutes = false;
        bool vfxIntroPlayed = false;

        try
        {
            SetAllRoutesMechanicPaused(true);
            pausedRoutes = true;

            CaptureAndDetachSeedsFromRoute(sideRouteA, flightItemsBuffer);
            CaptureAndDetachSeedsFromRoute(sideRouteB, flightItemsBuffer);

            if (flightItemsBuffer.Count > 0)
            {
                if (HasAnyBlackHoleVfxAssigned())
                {
                    StartCoroutine(PlayBlackHoleVfxIntro());
                    vfxIntroPlayed = true;
                }

                yield return PlayInwardAnimation(centerTransform.position);
            }

            bool swapped = sideRouteA.TrySwapAllRowsWith(sideRouteB);
            if (!swapped)
            {
                ReattachAllSeedsImmediately();

                if (vfxIntroPlayed)
                {
                    yield return PlayBlackHoleVfxOutro();
                }

                hasPendingExchangeRequest = false;
                yield break;
            }

            if (flightItemsBuffer.Count > 0)
            {
                yield return PlayOutwardAnimation();
            }

            ReattachAllSeedsImmediately();

            if (vfxIntroPlayed)
            {
                yield return PlayBlackHoleVfxOutro();
            }

            hasPendingExchangeRequest = false;
        }
        finally
        {
            if (pausedRoutes)
            {
                SetAllRoutesMechanicPaused(false);
            }

            hasPendingExchangeRequest = false;
            isExchangeInProgress = false;
            activeExchangeRoutine = null;
            UnregisterActiveExchangeGlobal();
        }
    }

    private void RegisterActiveExchangeGlobal()
    {
        if (hasRegisteredAsActiveExchange)
        {
            return;
        }

        hasRegisteredAsActiveExchange = true;
        activeExchangeMechanicCount++;
    }

    private void UnregisterActiveExchangeGlobal()
    {
        if (!hasRegisteredAsActiveExchange)
        {
            return;
        }

        hasRegisteredAsActiveExchange = false;
        activeExchangeMechanicCount = Mathf.Max(0, activeExchangeMechanicCount - 1);
    }

    private void CaptureAndDetachSeedsFromRoute(SplineRoute route, List<SeedFlightItem> output)
    {
        if (route == null || output == null)
        {
            return;
        }

        List<GameObject> rows = route.GetActiveBlockRows();
        if (rows == null || rows.Count == 0)
        {
            return;
        }

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            GameObject row = rows[rowIndex];
            if (row == null)
            {
                continue;
            }

            BlockRowSeedSpawner seeder = row.GetComponent<BlockRowSeedSpawner>();
            if (seeder == null)
            {
                continue;
            }

            for (int slotIndex = 0; slotIndex < 5; slotIndex++)
            {
                if (!seeder.DetachSeedAtSlot(slotIndex, out GameObject seed, true) || seed == null)
                {
                    continue;
                }

                seed.transform.DOKill();

                SeedFlightItem item = RentSeedFlightItem();
                item.sourceSpawner = seeder;
                item.slotIndex = slotIndex;
                item.seed = seed;
                item.originalScale = seed.transform.localScale;
                item.rowOrder = rowIndex;
                output.Add(item);
            }
        }
    }

    private IEnumerator PlayInwardAnimation(Vector3 centerWorldPosition)
    {
        Sequence master = DOTween.Sequence().SetUpdate(true);
        SetActiveExchangeSpeedScaledTween(master);
        TryPlayBlackHoleRowSfxWithCooldown(Const.absorbBlockSFX, absorbBlockSfxCooldown);

        float safeDuration = Mathf.Max(0.05f, inwardDuration);
        float safeMinScale = Mathf.Clamp01(minimumScaleRatioAtCenter);
        float safeStagger = Mathf.Max(0f, inwardStaggerByIndex);
        float safeJumpPower = Mathf.Max(0f, inwardJumpPower);
        int safeJumpCount = Mathf.Max(1, inwardJumpCount);

        for (int i = 0; i < flightItemsBuffer.Count; i++)
        {
            SeedFlightItem item = flightItemsBuffer[i];
            if (item == null || item.seed == null)
            {
                continue;
            }

            Transform seedTransform = item.seed.transform;
            Vector3 targetScale = Vector3.Max(item.originalScale * safeMinScale, Vector3.one * 0.01f);
            float delay = safeStagger * Mathf.Max(0, item.rowOrder);

            Tween moveTween = seedTransform.DOJump(centerWorldPosition, safeJumpPower, safeJumpCount, safeDuration)
                .SetEase(inwardMoveEase)
                .SetUpdate(true);
            Tween scaleTween = seedTransform.DOScale(targetScale, safeDuration)
                .SetEase(inwardScaleEase)
                .SetUpdate(true);

            master.Insert(delay, moveTween);
            master.Insert(delay, scaleTween);
        }

        if (!master.IsActive())
        {
            ClearActiveExchangeSpeedScaledTween(master);
            yield break;
        }

        yield return master.WaitForCompletion();
        ClearActiveExchangeSpeedScaledTween(master);
    }

    private IEnumerator PlayOutwardAnimation()
    {
        Sequence master = DOTween.Sequence().SetUpdate(true);
        SetActiveExchangeSpeedScaledTween(master);
        TryPlayBlackHoleRowSfxWithCooldown(Const.releaseBlockSFX, releaseBlockSfxCooldown);
        float safeDuration = Mathf.Max(0.05f, outwardDuration);
        float safeStagger = Mathf.Max(0f, outwardStaggerByIndex);
        float safeJumpPower = Mathf.Max(0f, outwardJumpPower);
        int safeJumpCount = Mathf.Max(1, outwardJumpCount);

        for (int i = 0; i < flightItemsBuffer.Count; i++)
        {
            SeedFlightItem item = flightItemsBuffer[i];
            if (item == null || item.seed == null || item.sourceSpawner == null)
            {
                continue;
            }

            Transform destinationSlot = item.sourceSpawner.GetSlotTransform(item.slotIndex);
            if (destinationSlot == null)
            {
                continue;
            }

            Transform seedTransform = item.seed.transform;
            Transform destinationParent = destinationSlot.parent != null ? destinationSlot.parent : destinationSlot;
            seedTransform.SetParent(destinationParent, true);

            Quaternion targetLocalRotation = destinationParent == destinationSlot ? Quaternion.identity : destinationSlot.localRotation;
            seedTransform.localRotation = targetLocalRotation;

            Vector3 destinationLocalPos = destinationSlot.localPosition;

            float delay = safeStagger * Mathf.Max(0, item.rowOrder);

            Tween moveTween = seedTransform.DOLocalJump(destinationLocalPos, safeJumpPower, safeJumpCount, safeDuration)
                .SetEase(outwardMoveEase)
                .SetUpdate(true);
            Tween scaleTween = seedTransform.DOScale(item.originalScale, safeDuration)
                .SetEase(outwardScaleEase)
                .SetUpdate(true);

            master.Insert(delay, moveTween);
            master.Insert(delay, scaleTween);
        }

        if (!master.IsActive())
        {
            ClearActiveExchangeSpeedScaledTween(master);
            yield break;
        }

        yield return master.WaitForCompletion();
        ClearActiveExchangeSpeedScaledTween(master);
    }

    private static void TryPlayBlackHoleRowSfxWithCooldown(string sfxKey, float cooldown)
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null || string.IsNullOrEmpty(sfxKey))
        {
            return;
        }

        audioManager.TryPlaySFXWithCooldown(sfxKey, Mathf.Max(0f, cooldown));
    }

    private void ReattachAllSeedsImmediately()
    {
        RestoreDetachedSeedsOrReleaseToPool();
    }

    private void RestoreDetachedSeedsOrReleaseToPool()
    {
        for (int i = 0; i < flightItemsBuffer.Count; i++)
        {
            SeedFlightItem item = flightItemsBuffer[i];
            if (item == null)
            {
                continue;
            }

            if (item.seed == null)
            {
                ReleaseSeedFlightItem(item);
                continue;
            }

            item.seed.transform.DOKill();
            item.seed.transform.localScale = item.originalScale;

            bool attached = false;
            if (item.sourceSpawner != null)
            {
                attached = item.sourceSpawner.AttachSeedToSlot(item.slotIndex, item.seed, true);
            }

            if (!attached)
            {
                ObjectPoolManager.ReturnObject(item.seed, ObjectPoolManager.PoolType.Seed);
            }

            ReleaseSeedFlightItem(item);
        }

        flightItemsBuffer.Clear();
    }

    private SeedFlightItem RentSeedFlightItem()
    {
        if (flightItemPool.Count > 0)
        {
            return flightItemPool.Pop();
        }

        return new SeedFlightItem();
    }

    private void ReleaseSeedFlightItem(SeedFlightItem item)
    {
        if (item == null)
        {
            return;
        }

        item.sourceSpawner = null;
        item.slotIndex = 0;
        item.seed = null;
        item.originalScale = Vector3.one;
        item.rowOrder = 0;
        flightItemPool.Push(item);
    }

    private void SetAllRoutesMechanicPaused(bool paused)
    {
        pauseRoutesBuffer.Clear();

        if (splineController == null)
        {
            return;
        }

        SplineRoute mainRoute = splineController.GetMainRoute();
        if (mainRoute != null)
        {
            pauseRoutesBuffer.Add(mainRoute);
        }

        SplineRoute[] sideRoutes = splineController.GetSideRoutes();
        if (sideRoutes != null)
        {
            for (int i = 0; i < sideRoutes.Length; i++)
            {
                SplineRoute sideRoute = sideRoutes[i];
                if (sideRoute == null || pauseRoutesBuffer.Contains(sideRoute))
                {
                    continue;
                }

                pauseRoutesBuffer.Add(sideRoute);
            }
        }

        for (int i = 0; i < pauseRoutesBuffer.Count; i++)
        {
            pauseRoutesBuffer[i].SetMechanicPaused(paused);
        }

        routesPausedByMechanic = paused;
    }

    private IEnumerator PlayBlackHoleVfxIntro()
    {
        AudioManager.Instance?.PlaySFX(Const.blackHoleSFX);
        yield return PlaySingleBlackHoleVfxIntro(GetPrimaryBlackHoleVfx(), portalVfxState);
    }

    private IEnumerator PlayBlackHoleVfxOutro()
    {
        AudioManager.Instance?.PlaySFX(Const.blackHoleSFX);
        yield return PlaySingleBlackHoleVfxOutro(GetPrimaryBlackHoleVfx(), portalVfxState);
    }

    private bool HasAnyBlackHoleVfxAssigned()
    {
        return GetPrimaryBlackHoleVfx() != null;
    }

    private ParticleSystem GetPrimaryBlackHoleVfx()
    {
        if (portalVfx != null)
        {
            return portalVfx;
        }

        // Backward compatibility for older prefabs that only had blackHoleVfx.
        return stoneVfx;
    }

    private IEnumerator PlaySingleBlackHoleVfxIntro(ParticleSystem targetVfx, BlackHoleVfxRuntimeState runtimeState)
    {
        if (targetVfx == null || runtimeState == null)
        {
            yield break;
        }

        CacheBlackHoleVfxDefaults(targetVfx, runtimeState);

        GameObject vfxObject = targetVfx.gameObject;
        if (vfxObject != null && !vfxObject.activeSelf)
        {
            vfxObject.SetActive(true);
        }

        Transform vfxTransform = targetVfx.transform;
        if (vfxTransform == null)
        {
            yield break;
        }

        KillBlackHoleVfxTweens(runtimeState);

        vfxTransform.DOKill();
        vfxTransform.localScale = Vector3.zero;
        SetBlackHoleVfxAlpha(targetVfx, runtimeState, 0f);

        targetVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        targetVfx.Play(true);

        float duration = Mathf.Max(0.01f, blackHoleVfxIntroDuration);
        float fadeDuration = Mathf.Max(duration, blackHoleVfxIntroFadeDuration);
        float maxAlpha = Mathf.Clamp01(blackHoleVfxMaxAlpha);

        runtimeState.scaleTween = vfxTransform.DOScale(runtimeState.originalScale, duration)
            .SetEase(blackHoleVfxScaleEase)
            .SetUpdate(true);

        runtimeState.fadeTween = DOVirtual.Float(0f, maxAlpha, fadeDuration, alpha => SetBlackHoleVfxAlpha(targetVfx, runtimeState, alpha))
            .SetEase(blackHoleVfxFadeEase)
            .SetUpdate(true);

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        SetActiveExchangeSpeedScaledTween(seq);
        seq.Join(runtimeState.scaleTween);
        seq.Join(runtimeState.fadeTween);
        if (runtimeState.moveTween != null)
        {
            seq.Join(runtimeState.moveTween);
        }

        yield return seq.WaitForCompletion();
        ClearActiveExchangeSpeedScaledTween(seq);

        SetBlackHoleVfxAlpha(targetVfx, runtimeState, maxAlpha);
    }

    private IEnumerator PlaySingleBlackHoleVfxOutro(ParticleSystem targetVfx, BlackHoleVfxRuntimeState runtimeState)
    {
        if (targetVfx == null || runtimeState == null)
        {
            yield break;
        }

        Transform vfxTransform = targetVfx.transform;
        if (vfxTransform == null)
        {
            yield break;
        }

        KillBlackHoleVfxTweens(runtimeState);

        float duration = Mathf.Max(0.01f, blackHoleVfxOutroDuration);
        float fadeDuration = Mathf.Max(duration, blackHoleVfxOutroFadeDuration);
        float scaleDuration = Mathf.Max(duration, fadeDuration);

        runtimeState.scaleTween = vfxTransform.DOScale(Vector3.zero, scaleDuration)
            .SetEase(blackHoleVfxOutroScaleEase)
            .SetUpdate(true);

        runtimeState.fadeTween = DOVirtual.Float(runtimeState.currentAlpha, 0f, fadeDuration, alpha => SetBlackHoleVfxAlpha(targetVfx, runtimeState, alpha))
            .SetEase(blackHoleVfxOutroFadeEase)
            .SetUpdate(true);

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        SetActiveExchangeSpeedScaledTween(seq);
        seq.Join(runtimeState.scaleTween);
        seq.Join(runtimeState.fadeTween);
        if (runtimeState.moveTween != null)
        {
            seq.Join(runtimeState.moveTween);
        }

        yield return seq.WaitForCompletion();
        ClearActiveExchangeSpeedScaledTween(seq);

        targetVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SetActiveExchangeSpeedScaledTween(Tween tween)
    {
        activeExchangeSpeedScaledTween = tween;
        ApplyExchangeAnimationSpeedScale();
    }

    private void ClearActiveExchangeSpeedScaledTween(Tween tween)
    {
        if (!ReferenceEquals(activeExchangeSpeedScaledTween, tween))
        {
            return;
        }

        activeExchangeSpeedScaledTween = null;
    }

    private void KillActiveExchangeSpeedScaledTween()
    {
        if (activeExchangeSpeedScaledTween != null && activeExchangeSpeedScaledTween.IsActive())
        {
            activeExchangeSpeedScaledTween.Kill(false);
        }

        activeExchangeSpeedScaledTween = null;
    }

    private void ApplyExchangeAnimationSpeedScale()
    {
        if (activeExchangeSpeedScaledTween == null || !activeExchangeSpeedScaledTween.IsActive())
        {
            return;
        }

        float speedScale = Mathf.Max(0.1f, SpeedMultiplierManager.GetBaseMultiplier());
        if (Mathf.Abs(activeExchangeSpeedScaledTween.timeScale - speedScale) <= 0.001f)
        {
            return;
        }

        activeExchangeSpeedScaledTween.timeScale = speedScale;
    }

    private void CacheBlackHoleVfxDefaults(ParticleSystem targetVfx, BlackHoleVfxRuntimeState runtimeState)
    {
        if (targetVfx == null || runtimeState == null || runtimeState.hasCachedDefaults)
        {
            return;
        }

        runtimeState.particleAlphaStates.Clear();
        ParticleSystem[] particleSystems = targetVfx.GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems != null && particleSystems.Length > 0)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                {
                    continue;
                }

                runtimeState.particleAlphaStates.Add(new BlackHoleParticleAlphaState
                {
                    particleSystem = ps,
                    startColor = ps.main.startColor
                });

                InitializeParticleAlphaState(runtimeState.particleAlphaStates[runtimeState.particleAlphaStates.Count - 1]);
            }
        }

        if (runtimeState.particleAlphaStates.Count == 0)
        {
            runtimeState.particleAlphaStates.Add(new BlackHoleParticleAlphaState
            {
                particleSystem = targetVfx,
                startColor = targetVfx.main.startColor
            });

            InitializeParticleAlphaState(runtimeState.particleAlphaStates[runtimeState.particleAlphaStates.Count - 1]);
        }

        Transform vfxTransform = targetVfx.transform;
        runtimeState.originalScale = vfxTransform != null ? vfxTransform.localScale : Vector3.one;
        runtimeState.originalLocalPosition = vfxTransform != null ? vfxTransform.localPosition : Vector3.zero;
        runtimeState.hasCachedDefaults = true;
    }

    private void SetBlackHoleVfxAlpha(ParticleSystem targetVfx, BlackHoleVfxRuntimeState runtimeState, float alpha01)
    {
        if (targetVfx == null || runtimeState == null)
        {
            return;
        }

        CacheBlackHoleVfxDefaults(targetVfx, runtimeState);

        float alpha = Mathf.Clamp01(alpha01);
        runtimeState.currentAlpha = alpha;

        for (int i = 0; i < runtimeState.particleAlphaStates.Count; i++)
        {
            BlackHoleParticleAlphaState state = runtimeState.particleAlphaStates[i];
            if (state == null || state.particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule mainModule = state.particleSystem.main;
            mainModule.startColor = BuildGradientWithAlpha(state, alpha);
        }

        SyncBlackHoleLiveParticlesAlpha(runtimeState, alpha);
    }

    private void KillBlackHoleVfxTweens(BlackHoleVfxRuntimeState runtimeState)
    {
        if (runtimeState == null)
        {
            return;
        }

        runtimeState.fadeTween?.Kill(false);
        runtimeState.scaleTween?.Kill(false);
        runtimeState.moveTween?.Kill(false);
        runtimeState.fadeTween = null;
        runtimeState.scaleTween = null;
        runtimeState.moveTween = null;
    }

    private ParticleSystem.MinMaxGradient BuildGradientWithAlpha(BlackHoleParticleAlphaState state, float alpha)
    {
        if (state == null)
        {
            Color fallback = Color.white;
            fallback.a = alpha;
            return new ParticleSystem.MinMaxGradient(fallback);
        }

        switch (state.colorMode)
        {
            case ParticleSystemGradientMode.Color:
            {
                Color c = state.cachedColor;
                c.a = alpha;
                return new ParticleSystem.MinMaxGradient(c);
            }
            case ParticleSystemGradientMode.TwoColors:
            {
                Color min = state.cachedColorMin;
                Color max = state.cachedColorMax;
                min.a = alpha;
                max.a = alpha;
                return new ParticleSystem.MinMaxGradient(min, max);
            }
            case ParticleSystemGradientMode.Gradient:
            {
                ApplyUniformAlphaToGradientKeys(state.gradientAlphaKeys, alpha);
                if (state.gradient == null)
                {
                    state.gradient = new Gradient();
                }

                state.gradient.SetKeys(state.gradientColorKeys, state.gradientAlphaKeys);
                return new ParticleSystem.MinMaxGradient(state.gradient);
            }
            case ParticleSystemGradientMode.TwoGradients:
            {
                ApplyUniformAlphaToGradientKeys(state.gradientMinAlphaKeys, alpha);
                ApplyUniformAlphaToGradientKeys(state.gradientMaxAlphaKeys, alpha);

                if (state.gradientMin == null)
                {
                    state.gradientMin = new Gradient();
                }

                if (state.gradientMax == null)
                {
                    state.gradientMax = new Gradient();
                }

                state.gradientMin.SetKeys(state.gradientMinColorKeys, state.gradientMinAlphaKeys);
                state.gradientMax.SetKeys(state.gradientMaxColorKeys, state.gradientMaxAlphaKeys);
                return new ParticleSystem.MinMaxGradient(state.gradientMin, state.gradientMax);
            }
            case ParticleSystemGradientMode.RandomColor:
            {
                ApplyUniformAlphaToGradientKeys(state.gradientAlphaKeys, alpha);
                if (state.gradient == null)
                {
                    state.gradient = new Gradient();
                }

                state.gradient.SetKeys(state.gradientColorKeys, state.gradientAlphaKeys);
                return new ParticleSystem.MinMaxGradient(state.gradient);
            }
            default:
            {
                Color c = Color.white;
                c.a = alpha;
                return new ParticleSystem.MinMaxGradient(c);
            }
        }
    }

    private void InitializeParticleAlphaState(BlackHoleParticleAlphaState state)
    {
        if (state == null)
        {
            return;
        }

        state.colorMode = state.startColor.mode;
        state.cachedColor = state.startColor.color;
        state.cachedColorMin = state.startColor.colorMin;
        state.cachedColorMax = state.startColor.colorMax;

        if (state.colorMode == ParticleSystemGradientMode.Gradient || state.colorMode == ParticleSystemGradientMode.RandomColor)
        {
            BuildGradientRuntimeCache(state.startColor.gradient, out state.gradient, out state.gradientColorKeys, out state.gradientAlphaKeys);
            return;
        }

        if (state.colorMode == ParticleSystemGradientMode.TwoGradients)
        {
            BuildGradientRuntimeCache(state.startColor.gradientMin, out state.gradientMin, out state.gradientMinColorKeys, out state.gradientMinAlphaKeys);
            BuildGradientRuntimeCache(state.startColor.gradientMax, out state.gradientMax, out state.gradientMaxColorKeys, out state.gradientMaxAlphaKeys);
        }
    }

    private void BuildGradientRuntimeCache(
        Gradient source,
        out Gradient runtimeGradient,
        out GradientColorKey[] colorKeys,
        out GradientAlphaKey[] alphaKeys)
    {
        runtimeGradient = new Gradient();

        if (source == null)
        {
            colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };

            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            };

            runtimeGradient.SetKeys(colorKeys, alphaKeys);
            return;
        }

        colorKeys = source.colorKeys;
        if (colorKeys == null || colorKeys.Length == 0)
        {
            colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };
        }

        alphaKeys = source.alphaKeys;
        if (alphaKeys == null || alphaKeys.Length == 0)
        {
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            };
        }

        runtimeGradient.SetKeys(colorKeys, alphaKeys);
    }

    private void ApplyUniformAlphaToGradientKeys(GradientAlphaKey[] alphaKeys, float alpha)
    {
        if (alphaKeys == null || alphaKeys.Length == 0)
        {
            return;
        }

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha = alpha;
        }
    }

    private void SyncBlackHoleLiveParticlesAlpha(BlackHoleVfxRuntimeState runtimeState, float alpha01)
    {
        if (runtimeState == null)
        {
            return;
        }

        byte alphaByte = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(alpha01) * 255f), 0, 255);

        for (int i = 0; i < runtimeState.particleAlphaStates.Count; i++)
        {
            BlackHoleParticleAlphaState state = runtimeState.particleAlphaStates[i];
            if (state == null || state.particleSystem == null)
            {
                continue;
            }

            ParticleSystem particleSystem = state.particleSystem;
            int particleCount = particleSystem.particleCount;
            if (particleCount <= 0)
            {
                continue;
            }

            if (state.particlesBuffer == null || state.particlesBuffer.Length < particleCount)
            {
                state.particlesBuffer = new ParticleSystem.Particle[particleCount];
            }

            int aliveCount = particleSystem.GetParticles(state.particlesBuffer);
            for (int p = 0; p < aliveCount; p++)
            {
                Color32 color = state.particlesBuffer[p].startColor;
                color.a = alphaByte;
                state.particlesBuffer[p].startColor = color;
            }

            particleSystem.SetParticles(state.particlesBuffer, aliveCount);
        }
    }

    private bool IsRefillBusy()
    {
        if (splineController == null)
        {
            return false;
        }

        return splineController.IsAnyRefillInProgress();
    }

    private void ResolveSplineControllerReference()
    {
        Transform searchRoot = transform.root != null ? transform.root : transform;
        splineController = searchRoot.GetComponentInChildren<SplineController>(true);
    }

    private void ResolveEventHubAndBind()
    {
        GameEventHub candidate = GameEventHub.Instance;
        if (!ReferenceEquals(candidate, eventHub))
        {
            if (isListeningPortalSwapEvent && eventHub != null)
            {
                eventHub.RemoveListener(GameEventType.OnPortalShooterSwapRequest, OnPortalShooterSwapRequested);
            }

            eventHub = candidate;
            isListeningPortalSwapEvent = false;
        }

        if (eventHub == null || !triggerByPortalShooterEvent)
        {
            return;
        }

        if (isListeningPortalSwapEvent)
        {
            return;
        }

        eventHub.AddListener(GameEventType.OnPortalShooterSwapRequest, OnPortalShooterSwapRequested);
        isListeningPortalSwapEvent = true;
    }

    private void UnbindEventHub()
    {
        if (isListeningPortalSwapEvent && eventHub != null)
        {
            eventHub.RemoveListener(GameEventType.OnPortalShooterSwapRequest, OnPortalShooterSwapRequested);
        }

        isListeningPortalSwapEvent = false;
        eventHub = null;
    }

    private void RefreshSideRouteReferencesFromController()
    {
        if (splineController == null)
        {
            return;
        }

        bool shouldRefreshA = sideRouteA == null || sideRouteA.GetRouteMode() != SplineRoute.RouteMode.Side;
        bool shouldRefreshB = sideRouteB == null || sideRouteB.GetRouteMode() != SplineRoute.RouteMode.Side || sideRouteB == sideRouteA;
        if (!shouldRefreshA && !shouldRefreshB)
        {
            return;
        }

        SplineRoute[] sideRoutes = splineController.GetSideRoutes();
        if (sideRoutes == null || sideRoutes.Length == 0)
        {
            return;
        }

        SplineRoute first = null;
        SplineRoute second = null;

        for (int i = 0; i < sideRoutes.Length; i++)
        {
            SplineRoute route = sideRoutes[i];
            if (route == null || route.GetRouteMode() != SplineRoute.RouteMode.Side)
            {
                continue;
            }

            if (first == null)
            {
                first = route;
                continue;
            }

            if (second == null)
            {
                second = route;
                break;
            }
        }

        if (shouldRefreshA)
        {
            sideRouteA = first;
        }

        if (shouldRefreshB)
        {
            sideRouteB = second;
        }
    }

    private bool IsSetupValid()
    {
        if (sideRouteA == null || sideRouteB == null || sideRouteA == sideRouteB)
        {
            return false;
        }

        if (sideRouteA.GetRouteMode() != SplineRoute.RouteMode.Side ||
            sideRouteB.GetRouteMode() != SplineRoute.RouteMode.Side)
        {
            return false;
        }

        if (!requireExactlyTwoSideRoutesInLevel)
        {
            return true;
        }

        SplineRoute[] allRoutes = null;
        if (splineController != null)
        {
            allRoutes = splineController.GetSideRoutes();
        }

        if (allRoutes == null || allRoutes.Length == 0)
        {
            allRoutes = GetComponentsInChildren<SplineRoute>(true);
        }

        int sideRouteCount = 0;
        bool hasA = false;
        bool hasB = false;

        for (int i = 0; i < allRoutes.Length; i++)
        {
            SplineRoute route = allRoutes[i];
            if (route == null)
            {
                continue;
            }

            if (route.GetRouteMode() == SplineRoute.RouteMode.Side)
            {
                sideRouteCount++;
                if (route == sideRouteA)
                {
                    hasA = true;
                }

                if (route == sideRouteB)
                {
                    hasB = true;
                }
            }
        }

        return sideRouteCount == 2 && hasA && hasB;
    }

    private void OnValidate()
    {
        if (sideRouteA != null && sideRouteB != null)
        {
            return;
        }

        Transform searchRoot = transform.root != null ? transform.root : transform;
        SplineRoute[] allRoutes = searchRoot.GetComponentsInChildren<SplineRoute>(true);

        SplineRoute first = null;
        SplineRoute second = null;

        for (int i = 0; i < allRoutes.Length; i++)
        {
            SplineRoute route = allRoutes[i];
            if (route == null || route.GetRouteMode() != SplineRoute.RouteMode.Side)
            {
                continue;
            }

            if (first == null)
            {
                first = route;
                continue;
            }

            if (second == null)
            {
                second = route;
                break;
            }
        }

        if (sideRouteA == null)
        {
            sideRouteA = first;
        }

        if (sideRouteB == null)
        {
            sideRouteB = second;
        }
    }
}
