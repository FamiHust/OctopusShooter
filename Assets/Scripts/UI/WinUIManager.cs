using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Serialization;

public class WinUIManager : MonoBehaviour
{
    [Header("Level Progression")]
    [SerializeField, Min(1)] private int maxPlayableLevel = 40;

    [SerializeField] private Button x2CoinBtn;
    [SerializeField] private Button nextBtn;
    [SerializeField] private GameObject starLevel;
    [SerializeField] private Transform starIcon;
    [SerializeField] private List<GameObject> newFeatureObjects = new List<GameObject>();
    [SerializeField] private WinFeatureUnlockConfigSO winFeatureUnlockConfig;
    [SerializeField] private float unlockedFeaturePunchStrength = 0.08f;
    [SerializeField] private float unlockedFeaturePunchDuration = 0.35f;
    [SerializeField] private float unlockedFeaturePunchInterval = 0.6f;
    [SerializeField] private Text levelText;
    [SerializeField] private Transform congratsText;
    

    [Header("Coin Fly Animation")]
    [SerializeField] private Text coinText;
    [SerializeField] private Transform coinIcon;
    [SerializeField] private Transform coinStartPos;
    [SerializeField] private Transform coinEndPos;
    [SerializeField] private GameObject coinVisualPrefab;
    [SerializeField] private Transform coinVisualParent;
    [SerializeField] private int coinVisualCount = 12;
    [SerializeField] private int coinRewardAmount = 120;
    [SerializeField] private int nextButtonCoinReward = 40;
    [SerializeField] private float spawnInterval = 0.06f;
    [SerializeField] private float burstDuration = 0.14f;
    [SerializeField] private float spawnBounceHeight = 24f;
    [SerializeField] private float spawnBounceDuration = 0.2f;
    [SerializeField] private float spawnDropOffset = 10f;
    [SerializeField] private float spawnPopScale = 1.15f;
    [SerializeField] private float spawnImpactScale = 0.9f;
    [SerializeField] private float spawnSettleHeight = 6f;
    [SerializeField] private float spawnSettleDuration = 0.1f;
    [SerializeField] private float preFlyDelay = 0.15f;
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float startScatterRadius = 36f;
    [SerializeField] private float startScale = 0.55f;
    [SerializeField] private float endScale = 0.95f;
    [SerializeField] private float coinCountAnimDuration = 0.45f;
    [SerializeField] private int coinVisualSortingOrder = 500;
    [SerializeField] private float delayBeforeShowLoadingAfterCoin = 0.5f;

    [Header("Coin SFX")]
    [SerializeField] private float goldFrameSfxCooldown = 0.08f;
    [SerializeField] private float goldEarnSfxCooldown = 0.06f;

    [Header("Firework")]
    [FormerlySerializedAs("fireworkPrefab")]
    [SerializeField] private GameObject fireworkSeedPrefab;
    [FormerlySerializedAs("fireworkVfxPrefab")]
    [SerializeField] private GameObject fireworkExplosionVfxPrefab;
    [SerializeField] private List<Transform> fireworkTargets; // nÃªn cÃ³ 4 target
    [SerializeField] private List<Transform> fireworkSpawnPoints; // 2 pháº§n tá»­: [0]=trÃ¡i, [1]=pháº£i
    [SerializeField] private float fireworkFlyDuration = 0.6f;
    [SerializeField] private float fireworkSpawnDelay = 0.1f;
    [SerializeField] private float fireworkArriveDistanceThreshold = 0.03f;
    [SerializeField] private float fireworkExplosionVfxLifetime = 1.5f;
    [SerializeField] private int fireworkExplosionPrewarmCount = 4;

    [Header("Performance")]
    [SerializeField] private bool enableLowEndLiteMode = true;
    [SerializeField] private int lowEndSystemMemoryMb = 3000;
    [SerializeField] private int lowEndProcessorCount = 4;
    [SerializeField] private int maxCoinVisualCountOnLowEnd = 6;
    [SerializeField] private bool simplifyCoinFlightOnLowEnd = true;
    [SerializeField] private bool disableCoinCountTweenOnLowEnd = true;
    [SerializeField] private bool skipCoinIconHitAnimOnLowEnd = true;
    [SerializeField] private float coinIconHitCooldownOnLowEnd = 0.08f;
    [SerializeField] private bool skipFireworkOnLowEnd = true;
    [SerializeField] private int maxFireworkCountOnLowEnd = 2;
    [SerializeField] private bool disableFireworkScaleTweenOnLowEnd = true;


    private readonly List<Tween> fireworkTweens = new List<Tween>();
    private readonly List<Tween> fireworkCleanupTweens = new List<Tween>();
    private readonly List<GameObject> activeFireworkPooledObjects = new List<GameObject>();
    private readonly List<GameObject> activeCoinPooledObjects = new List<GameObject>();
    private readonly List<Tween> coinFlowTweens = new List<Tween>();
    private int completedVisualCoins;
    private int pendingRewardAmount;
    private Tween coinCountTween;
    private Tween coinIconTween;
    private int activeCoinAnimations;
    private System.Action onCoinCollectCompleted;
    private bool isNextFlowRunning;
    private bool hasPrewarmedFireworkExplosionPool;
    private Tween unlockedFeaturePunchLoopTween;
    private Tween starIconLoopTween;
    private Tween x2CoinPunchLoopTween;
    private int pendingCoinPersistDelta;
    private int displayedCoinBalance;
    private bool useLiteMode;
    private float nextCoinIconHitRealtime;
    private readonly HashSet<int> preparedFireworkVfxIds = new HashSet<int>();
    private readonly Dictionary<int, ParticleSystem[]> fireworkParticleCache = new Dictionary<int, ParticleSystem[]>();
    private bool isFinalLevelWin;

    void OnEnable()
    {
        isNextFlowRunning = false;

        if (nextBtn != null)
        {
            nextBtn.gameObject.SetActive(true);
            nextBtn.interactable = true;
        }

        if (x2CoinBtn != null)
        {
            x2CoinBtn.gameObject.SetActive(true);
            x2CoinBtn.interactable = true;
        }

        if (starLevel != null)
        {
            starLevel.SetActive(true);
        }

        useLiteMode = ShouldUseLiteMode();
        nextCoinIconHitRealtime = 0f;
        EnsureFireworkExplosionPoolPrewarmed();
        int completedLevel = GetClampedCurrentLevel();
        isFinalLevelWin = completedLevel >= Mathf.Max(1, maxPlayableLevel);
        UpdateLevelText(completedLevel);
        UpdateMechanicUnlockVisual(completedLevel);
        IncreaseLevel();
        SetButton();
        UpdateCoinTextInstant(GetCurrentCoin());
        AnimateElementsOnWin();
        PlayFirework();
    }
    
    private void OnDisable()
    {
        if (nextBtn != null)
        {
            nextBtn.onClick.RemoveAllListeners();
        }

        if (coinCountTween != null && coinCountTween.IsActive())
        {
            coinCountTween.Kill();
        }

        if (coinIconTween != null && coinIconTween.IsActive())
        {
            coinIconTween.Kill();
        }

        KillPersistentUITweens();
        KillUnlockedFeaturePunchLoop();

        KillCoinFlowTweens();
        KillFireworkTweens();
        CommitPendingCoinRewardIfAny();

        preparedFireworkVfxIds.Clear();
        fireworkParticleCache.Clear();
    }

    private void KillPersistentUITweens()
    {
        if (starIconLoopTween != null && starIconLoopTween.IsActive())
        {
            starIconLoopTween.Kill();
        }

        if (x2CoinPunchLoopTween != null && x2CoinPunchLoopTween.IsActive())
        {
            x2CoinPunchLoopTween.Kill();
        }

        starIconLoopTween = null;
        x2CoinPunchLoopTween = null;

        if (starIcon != null)
        {
            starIcon.DOKill();
        }

        if (x2CoinBtn != null)
        {
            x2CoinBtn.transform.DOKill();
        }

        if (congratsText != null)
        {
            congratsText.DOKill();
        }
    }

    private void EnsureFireworkExplosionPoolPrewarmed()
    {
        if (hasPrewarmedFireworkExplosionPool || fireworkExplosionVfxPrefab == null)
        {
            return;
        }

        int prewarmCount = Mathf.Max(0, fireworkExplosionPrewarmCount);
        for (int i = 0; i < prewarmCount; i++)
        {
            GameObject pooledVfx = ObjectPoolManager.SpawnObject(
                fireworkExplosionVfxPrefab,
                transform,
                ObjectPoolManager.PoolType.Particle
            );

            ObjectPoolManager.ReturnObject(pooledVfx, ObjectPoolManager.PoolType.Particle);
        }

        hasPrewarmedFireworkExplosionPool = true;
    }

    void PlayFirework()
    {
        if (fireworkSeedPrefab == null || fireworkExplosionVfxPrefab == null
            || fireworkTargets == null || fireworkTargets.Count < 4
            || fireworkSpawnPoints == null || fireworkSpawnPoints.Count < 2)
            return;

        if (useLiteMode && skipFireworkOnLowEnd)
        {
            return;
        }

        int fireworkCount = 4;
        if (useLiteMode)
        {
            fireworkCount = Mathf.Clamp(maxFireworkCountOnLowEnd, 1, fireworkCount);
        }

        for (int i = 0; i < fireworkCount; i++)
        {
            int index = i;

            Tween delayedSpawn = DOVirtual.DelayedCall(i * fireworkSpawnDelay, () =>
            {
                SpawnAndFlyFirework(index);
            }, true);
            fireworkTweens.Add(delayedSpawn);
        }
    }

    void SpawnAndFlyFirework(int index)
    {
        if (index < 0 || index >= fireworkTargets.Count)
        {
            return;
        }

        GameObject fireworkSeed = ObjectPoolManager.SpawnObject(
            fireworkSeedPrefab,
            transform,
            ObjectPoolManager.PoolType.Particle
        );
        RegisterActiveFireworkObject(fireworkSeed);

        AudioManager.Instance?.PlaySFX(Const.fireworkWhistleSFX);

        // Thá»© tá»±: trÃ¡i, pháº£i, trÃ¡i, pháº£i.
        int spawnIndex = index % 2;

        Vector3 spawnPos = fireworkSpawnPoints[spawnIndex].position;
        Vector3 targetPos = fireworkTargets[index].position;

        fireworkSeed.transform.position = spawnPos;
        fireworkSeed.transform.localScale = Vector3.one * 0.7f;

        Tween scaleTween = null;
        Tween flyTween = null;
        bool finalized = false;
        System.Action finalizeAtTarget = () =>
        {
            if (finalized || fireworkSeed == null)
            {
                return;
            }

            finalized = true;

            if (flyTween != null && flyTween.IsActive())
            {
                flyTween.Kill(false);
            }

            if (scaleTween != null && scaleTween.IsActive())
            {
                scaleTween.Kill(false);
            }

            ReturnActiveFireworkObject(fireworkSeed);
            SpawnFireworkExplosionVfx(targetPos);
        };

        flyTween = fireworkSeed.transform.DOMove(targetPos, Mathf.Max(0.01f, fireworkFlyDuration))
            .SetUpdate(true)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                finalizeAtTarget();
            });
        fireworkTweens.Add(flyTween);

        if (!(useLiteMode && disableFireworkScaleTweenOnLowEnd))
        {
            scaleTween = fireworkSeed.transform.DOScale(1f, fireworkFlyDuration * 0.5f)
                .SetUpdate(true)
                .SetEase(Ease.OutBack);
            fireworkTweens.Add(scaleTween);
        }
    }

    private void SpawnFireworkExplosionVfx(Vector3 position)
    {
        if (fireworkExplosionVfxPrefab == null)
        {
            return;
        }

        GameObject vfx = ObjectPoolManager.SpawnObject(
            fireworkExplosionVfxPrefab,
            transform,
            ObjectPoolManager.PoolType.Particle
        );
        RegisterActiveFireworkObject(vfx);
        vfx.transform.position = position;
        vfx.transform.rotation = Quaternion.identity;
        vfx.transform.SetAsLastSibling();
        PrepareFireworkVfxForUI(vfx);
        AudioManager.Instance?.PlaySFX(Const.fireworkExplodeSFX);

        Tween cleanupTween = DOVirtual.DelayedCall(Mathf.Max(0.1f, fireworkExplosionVfxLifetime), () =>
        {
            ReturnActiveFireworkObject(vfx);
        }, true);
        fireworkCleanupTweens.Add(cleanupTween);
    }

    private void PrepareFireworkVfxForUI(GameObject vfx)
    {
        if (vfx == null)
        {
            return;
        }

        int instanceId = vfx.GetInstanceID();
        if (preparedFireworkVfxIds.Add(instanceId))
        {
            EnsureObjectLayerMatchesUI(vfx);

            OnParticleDestroy[] autoReturnHandlers = vfx.GetComponentsInChildren<OnParticleDestroy>(true);
            if (autoReturnHandlers != null)
            {
                for (int i = 0; i < autoReturnHandlers.Length; i++)
                {
                    if (autoReturnHandlers[i] == null)
                    {
                        continue;
                    }

                    autoReturnHandlers[i].enabled = false;
                }
            }

            if (vfx.transform.localScale.sqrMagnitude <= 0.0001f)
            {
                vfx.transform.localScale = Vector3.one;
            }

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            Canvas[] nestedCanvases = vfx.GetComponentsInChildren<Canvas>(true);
            if (parentCanvas != null && nestedCanvases != null && nestedCanvases.Length > 0)
            {
                int sortingLayerId = parentCanvas.sortingLayerID;
                int sortingOrder = parentCanvas.sortingOrder + 1;

                for (int i = 0; i < nestedCanvases.Length; i++)
                {
                    Canvas nestedCanvas = nestedCanvases[i];
                    if (nestedCanvas == null)
                    {
                        continue;
                    }

                    nestedCanvas.overrideSorting = true;
                    nestedCanvas.sortingLayerID = sortingLayerId;
                    nestedCanvas.sortingOrder = Mathf.Max(nestedCanvas.sortingOrder, sortingOrder);

                    if (nestedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        nestedCanvas.worldCamera = parentCanvas.worldCamera;
                    }
                }
            }

            ParticleSystem[] cachedParticleSystems = vfx.GetComponentsInChildren<ParticleSystem>(true);
            if (cachedParticleSystems != null && cachedParticleSystems.Length > 0)
            {
                for (int i = 0; i < cachedParticleSystems.Length; i++)
                {
                    ParticleSystem particleSystem = cachedParticleSystems[i];
                    if (particleSystem == null)
                    {
                        continue;
                    }

                    ParticleSystem.MainModule mainModule = particleSystem.main;
                    mainModule.useUnscaledTime = true;
                }

                fireworkParticleCache[instanceId] = cachedParticleSystems;
            }
        }

        if (!fireworkParticleCache.TryGetValue(instanceId, out ParticleSystem[] particleSystems) || particleSystems == null)
        {
            particleSystems = vfx.GetComponentsInChildren<ParticleSystem>(true);
            fireworkParticleCache[instanceId] = particleSystems;
        }

        if (particleSystems == null || particleSystems.Length == 0)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private void EnsureObjectLayerMatchesUI(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return;
        }

        int uiLayer = gameObject.layer;
        Transform[] children = rootObject.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == null)
            {
                continue;
            }

            children[i].gameObject.layer = uiLayer;
        }
    }

    private void AnimateElementsOnWin()
    {
        KillPersistentUITweens();

        if (starIcon != null && starIcon.gameObject.activeInHierarchy)
        {
            starIcon.localScale = Vector3.zero;

            // Pop in trÆ°á»›c
            starIcon.DOScale(1f, 0.3f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    // Breathing loop
                    starIconLoopTween = starIcon.DOScale(1.08f, 2f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
                });
        }
        if (congratsText != null)
        {
            congratsText.localScale = Vector3.zero;

            congratsText.DOScale(1f, 0.5f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    congratsText.DOPunchScale(Vector3.one * 0.1f, 0.4f, 1);
                });
        }
        PunchLoop();
    }

    private void UpdateMechanicUnlockVisual(int completedLevel)
    {
        KillUnlockedFeaturePunchLoop();
        SetAllNewFeatureObjectsActive(false);

        if (starIcon != null)
        {
            starIcon.gameObject.SetActive(true);
        }

        int unlockFeatureIndex = ResolveUnlockFeatureIndex(completedLevel);
        if (unlockFeatureIndex < 0)
        {
            return;
        }

        if (starIcon != null)
        {
            starIcon.gameObject.SetActive(false);
        }

        if (newFeatureObjects == null || unlockFeatureIndex >= newFeatureObjects.Count)
        {
            ;
            return;
        }

        GameObject unlockedFeatureObject = newFeatureObjects[unlockFeatureIndex];
        if (unlockedFeatureObject != null)
        {
            unlockedFeatureObject.SetActive(true);
            PlayUnlockedFeatureImagePunch(unlockedFeatureObject);
        }
    }

    private void PlayUnlockedFeatureImagePunch(GameObject unlockedFeatureObject)
    {
        if (unlockedFeatureObject == null)
        {
            return;
        }

        Transform featureRoot = unlockedFeatureObject.transform;
        if (featureRoot.childCount <= 0)
        {
            return;
        }

        Transform featureImage = featureRoot.GetChild(0);
        if (featureImage == null)
        {
            return;
        }

        featureImage.DOKill();
        featureImage.localScale = Vector3.one;
        Sequence punchLoop = DOTween.Sequence();
        punchLoop.Append(featureImage.DOPunchScale(
                Vector3.one * Mathf.Max(0f, unlockedFeaturePunchStrength),
                Mathf.Max(0.05f, unlockedFeaturePunchDuration),
                2,
                0.7f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true));
        punchLoop.AppendInterval(Mathf.Max(0f, unlockedFeaturePunchInterval));
        punchLoop.SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);

        unlockedFeaturePunchLoopTween = punchLoop;
    }

    private void KillUnlockedFeaturePunchLoop()
    {
        if (unlockedFeaturePunchLoopTween != null && unlockedFeaturePunchLoopTween.IsActive())
        {
            unlockedFeaturePunchLoopTween.Kill();
        }

        unlockedFeaturePunchLoopTween = null;
    }

    private int ResolveUnlockFeatureIndex(int completedLevel)
    {
        if (winFeatureUnlockConfig != null)
        {
            return winFeatureUnlockConfig.GetUnlockFeatureIndexForLevel(completedLevel);
        }

        if (completedLevel == 14)
        {
            return 0;
        }

        if (completedLevel == 24)
        {
            return 1;
        }

        if (completedLevel == 34)
        {
            return 2;
        }

        return -1;
    }

    private void SetAllNewFeatureObjectsActive(bool isActive)
    {
        if (newFeatureObjects == null)
        {
            return;
        }

        for (int i = 0; i < newFeatureObjects.Count; i++)
        {
            if (newFeatureObjects[i] != null)
            {
                newFeatureObjects[i].SetActive(isActive);
            }
        }
    }

    void PunchLoop()
    {
        if (x2CoinBtn == null) return;

        x2CoinBtn.transform.DOKill();
        Sequence punchLoop = DOTween.Sequence();
        punchLoop.AppendInterval(1f);
        punchLoop.Append(x2CoinBtn.transform.DOPunchScale(Vector3.one * 0.1f, 0.5f, 2).SetEase(Ease.Linear));
        punchLoop.AppendInterval(4f);
        punchLoop.SetLoops(-1, LoopType.Restart).SetUpdate(true);
        x2CoinPunchLoopTween = punchLoop;
    }   

    private void UpdateLevelText(int completedLevel)
    {
        if (levelText != null)
        {
            levelText.text = $"{completedLevel}";
        }
    }

    private void IncreaseLevel()
    {
        int maxLevel = Mathf.Max(1, maxPlayableLevel);
        int currentLevel = Mathf.Clamp(PlayerPrefs.GetInt(Const.player_level_key, 1), 1, maxLevel);
        int nextLevel = Mathf.Min(maxLevel, currentLevel + 1);
        PlayerPrefs.SetInt(Const.player_level_key, nextLevel);
        PlayerPrefs.Save();
    }

    private int GetClampedCurrentLevel()
    {
        int maxLevel = Mathf.Max(1, maxPlayableLevel);
        int currentLevel = Mathf.Clamp(PlayerPrefs.GetInt(Const.player_level_key, 1), 1, maxLevel);

        if (PlayerPrefs.GetInt(Const.player_level_key, 1) != currentLevel)
        {
            PlayerPrefs.SetInt(Const.player_level_key, currentLevel);
            PlayerPrefs.Save();
        }

        return currentLevel;
    }

    private void SetButton()
    {
        if (nextBtn == null)
            return;

        nextBtn.onClick.RemoveAllListeners();
        nextBtn.onClick.AddListener(() =>
        {
            if (isNextFlowRunning)
                return;

            isNextFlowRunning = true;

            if (x2CoinBtn != null)
                x2CoinBtn.gameObject.SetActive(false);
            if (starLevel != null)
                starLevel.SetActive(false);
            nextBtn.gameObject.SetActive(false);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideInGameUIImmediate();
            }

            PlayCoinCollectAnimation(nextButtonCoinReward, OnNextCoinFlowCompleted);
        });
    }

    public void PlayCoinCollectAnimation()
    {
        PlayCoinCollectAnimation(coinRewardAmount, null);
    }

    public void PlayCoinCollectAnimation(int rewardAmount)
    {
        PlayCoinCollectAnimation(rewardAmount, null);
    }

    public void PlayCoinCollectAnimation(int rewardAmount, System.Action onComplete)
    {
        KillCoinFlowTweens();
        completedVisualCoins = 0;
        pendingRewardAmount = Mathf.Max(0, rewardAmount);
        onCoinCollectCompleted = onComplete;
        activeCoinAnimations = 0;
        pendingCoinPersistDelta = 0;
        displayedCoinBalance = GetCurrentCoin();
        UpdateCoinTextInstant(displayedCoinBalance);

        if (coinVisualPrefab == null || coinStartPos == null || coinEndPos == null)
        {
            AddCoinRewardIncrement(pendingRewardAmount);
            CompleteCoinFlow();
            return;
        }

        int spawnCount = Mathf.Max(1, coinVisualCount);
        if (pendingRewardAmount > 0)
        {
            spawnCount = GetBestSpawnCountForEqualReward(pendingRewardAmount, spawnCount);
        }

        if (useLiteMode)
        {
            spawnCount = Mathf.Min(spawnCount, Mathf.Max(1, maxCoinVisualCountOnLowEnd));
        }

        int rewardPerCoin = pendingRewardAmount > 0
            ? (pendingRewardAmount / Mathf.Max(1, spawnCount))
            : 0;

        float delay = 0f;
        activeCoinAnimations = spawnCount;

        for (int i = 0; i < spawnCount; i++)
        {
            bool isFirstCoin = i == 0;
            Tween delayedSpawn = DOVirtual.DelayedCall(delay, () => SpawnAndFlyOneCoin(rewardPerCoin, isFirstCoin), true);
            coinFlowTweens.Add(delayedSpawn);
            delay += Mathf.Max(0f, spawnInterval);
        }
    }

    private void SpawnAndFlyOneCoin(int rewardForCoin, bool isFirstCoin)
    {
        if (coinVisualPrefab == null || coinStartPos == null || coinEndPos == null)
        {
            AddCoinRewardIncrement(rewardForCoin);
            return;
        }

        Transform parent = coinVisualParent != null ? coinVisualParent : transform;
        GameObject coin = ObjectPoolManager.SpawnObject(
            coinVisualPrefab,
            parent,
            ObjectPoolManager.PoolType.Coin
        );
        RegisterActiveCoinObject(coin);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TryPlaySFXWithCooldown(Const.goldFrameSFX, Mathf.Max(0f, goldFrameSfxCooldown));
        }
        coin.transform.SetAsLastSibling();

        Canvas coinCanvas = coin.GetComponent<Canvas>();
        if (coinCanvas != null)
        {
            coinCanvas.overrideSorting = true;
            coinCanvas.sortingOrder = coinVisualSortingOrder;
        }

        RectTransform coinRect = coin.GetComponent<RectTransform>();
        RectTransform parentRect = parent as RectTransform;

        Vector3 spawnPos;
        Vector3 landedPos;
        Vector3 endPos;
        Vector2 spawnAnchoredPos = Vector2.zero;
        Vector2 landedAnchoredPos = Vector2.zero;
        Vector2 endAnchoredPos = Vector2.zero;
        bool useAnchoredTween = coinRect != null && parentRect != null;

        if (useAnchoredTween)
        {
            spawnAnchoredPos = WorldToAnchoredPosition(coinStartPos.position, parentRect);
            endAnchoredPos = WorldToAnchoredPosition(coinEndPos.position, parentRect);

            if (!isFirstCoin)
            {
                Vector2 randomOffset = Random.insideUnitCircle * startScatterRadius;
                spawnAnchoredPos += randomOffset;
            }

            landedAnchoredPos = spawnAnchoredPos + Vector2.down * Mathf.Max(0f, spawnDropOffset);

            coinRect.anchoredPosition = spawnAnchoredPos;
            coinRect.localScale = Vector3.zero;

            spawnPos = spawnAnchoredPos;
            landedPos = landedAnchoredPos;
            endPos = endAnchoredPos;
        }
        else
        {
            spawnPos = coinStartPos.position;
            if (!isFirstCoin)
            {
                Vector3 randomOffset = Random.insideUnitCircle * startScatterRadius;
                spawnPos += randomOffset;
            }

            landedPos = spawnPos + Vector3.down * Mathf.Max(0f, spawnDropOffset);
            endPos = coinEndPos.position;

            coin.transform.position = spawnPos;
            coin.transform.localScale = Vector3.zero;
        }

        float safeBurstDuration = Mathf.Max(0.05f, burstDuration);
        float safeBounceDuration = Mathf.Max(0.05f, spawnBounceDuration);
        float safeSettleDuration = Mathf.Max(0.05f, spawnSettleDuration);
        float popScale = Mathf.Max(0.01f, spawnPopScale);
        float impactScale = Mathf.Clamp(spawnImpactScale, 0.5f, 1.5f);

        if (useLiteMode && simplifyCoinFlightOnLowEnd)
        {
            Sequence liteSeq = DOTween.Sequence();
            liteSeq.Append(coin.transform.DOScale(endScale, Mathf.Max(0.1f, flyDuration * 0.35f)).SetEase(Ease.OutBack));
            if (useAnchoredTween)
            {
                liteSeq.Join(coinRect.DOAnchorPos(endAnchoredPos, Mathf.Max(0.12f, flyDuration)).SetEase(Ease.InQuad));
            }
            else
            {
                liteSeq.Join(coin.transform.DOMove(endPos, Mathf.Max(0.12f, flyDuration)).SetEase(Ease.InQuad));
            }

            liteSeq.OnComplete(() =>
            {
                completedVisualCoins++;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.TryPlaySFXWithCooldown(Const.goldEarnSFX, Mathf.Max(0f, goldEarnSfxCooldown));
                }
                PlayCoinIconHitAnim();
                AddCoinRewardIncrement(rewardForCoin);
                ReturnActiveCoinObject(coin);

                activeCoinAnimations--;
                if (activeCoinAnimations <= 0)
                {
                    CompleteCoinFlow();
                }
            });

            coinFlowTweens.Add(liteSeq);
            return;
        }

        Sequence seq = DOTween.Sequence();
        // Pop out, fall lower than spawn point, then do a tiny settle bounce.
        seq.Append(coin.transform.DOScale(startScale * popScale, safeBurstDuration * 0.7f).SetEase(Ease.OutBack));
        if (useAnchoredTween)
        {
            seq.Join(coinRect.DOAnchorPosY(spawnAnchoredPos.y + Mathf.Max(0f, spawnBounceHeight), safeBounceDuration * 0.45f).SetEase(Ease.OutQuad));
        }
        else
        {
            seq.Join(coin.transform.DOMoveY(spawnPos.y + Mathf.Max(0f, spawnBounceHeight), safeBounceDuration * 0.45f).SetEase(Ease.OutQuad));
        }
        seq.Append(coin.transform.DOScale(startScale * impactScale, safeBurstDuration * 0.3f).SetEase(Ease.InQuad));
        if (useAnchoredTween)
        {
            seq.Join(coinRect.DOAnchorPosY(landedAnchoredPos.y, safeBounceDuration * 0.55f).SetEase(Ease.InQuad));
            seq.Append(coinRect.DOAnchorPosY(landedAnchoredPos.y + Mathf.Max(0f, spawnSettleHeight), safeSettleDuration * 0.45f).SetEase(Ease.OutQuad));
            seq.Append(coinRect.DOAnchorPosY(landedAnchoredPos.y, safeSettleDuration * 0.55f).SetEase(Ease.InQuad));
        }
        else
        {
            seq.Join(coin.transform.DOMoveY(landedPos.y, safeBounceDuration * 0.55f).SetEase(Ease.InQuad));
            seq.Append(coin.transform.DOMoveY(landedPos.y + Mathf.Max(0f, spawnSettleHeight), safeSettleDuration * 0.45f).SetEase(Ease.OutQuad));
            seq.Append(coin.transform.DOMoveY(landedPos.y, safeSettleDuration * 0.55f).SetEase(Ease.InQuad));
        }
        seq.Join(coin.transform.DOScale(startScale, safeSettleDuration).SetEase(Ease.OutQuad));
        seq.AppendInterval(Mathf.Max(0f, preFlyDelay));
        seq.Append(coin.transform.DOScale(endScale, flyDuration * 0.35f).SetEase(Ease.OutBack));
        if (useAnchoredTween)
        {
            seq.Join(coinRect.DOAnchorPos(endAnchoredPos, flyDuration).SetEase(Ease.InQuad));
        }
        else
        {
            seq.Join(coin.transform.DOMove(endPos, flyDuration).SetEase(Ease.InQuad));
        }
        seq.OnComplete(() =>
        {
            completedVisualCoins++;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.TryPlaySFXWithCooldown(Const.goldEarnSFX, Mathf.Max(0f, goldEarnSfxCooldown));
            }
            PlayCoinIconHitAnim();
            AddCoinRewardIncrement(rewardForCoin);
            ReturnActiveCoinObject(coin);

            activeCoinAnimations--;
            if (activeCoinAnimations <= 0)
            {
                CompleteCoinFlow();
            }
        });

        coinFlowTweens.Add(seq);
    }

    private Vector2 WorldToAnchoredPosition(Vector3 worldPosition, RectTransform parentRect)
    {
        if (parentRect == null)
        {
            return Vector2.zero;
        }

        Camera eventCamera = GetCanvasEventCamera(parentRect);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldPosition);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return localPoint;
        }

        Vector3 fallback = parentRect.InverseTransformPoint(worldPosition);
        return new Vector2(fallback.x, fallback.y);
    }

    private Camera GetCanvasEventCamera(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return null;
        }

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return Camera.main;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private void PlayCoinIconHitAnim()
    {
        if (coinIcon == null)
        {
            return;
        }

        if (useLiteMode)
        {
            if (skipCoinIconHitAnimOnLowEnd)
            {
                return;
            }

            if (Time.unscaledTime < nextCoinIconHitRealtime)
            {
                return;
            }

            nextCoinIconHitRealtime = Time.unscaledTime + Mathf.Max(0.01f, coinIconHitCooldownOnLowEnd);
        }

        if (coinIconTween != null && coinIconTween.IsActive())
        {
            coinIconTween.Kill();
        }
        coinIcon.localScale = Vector3.one;
        coinIconTween = coinIcon.DOScale(Vector3.one * 1.2f, 0.16f).SetEase(Ease.OutBack);
        coinIconTween.OnComplete(() =>
        {
            if (coinIcon != null)
            {
                coinIcon.DOScale(Vector3.one, 0).SetEase(Ease.InBack);
            }
        });
        
    }

    private int GetBestSpawnCountForEqualReward(int totalReward, int desiredMaxSpawn)
    {
        int safeReward = Mathf.Max(0, totalReward);
        int maxSpawn = Mathf.Max(1, desiredMaxSpawn);

        if (safeReward <= 0)
        {
            return 1;
        }

        maxSpawn = Mathf.Min(maxSpawn, safeReward);

        for (int count = maxSpawn; count >= 1; count--)
        {
            if (safeReward % count == 0)
            {
                return count;
            }
        }

        return 1;
    }

    private void AddCoinRewardIncrement(int reward)
    {
        int safeReward = Mathf.Max(0, reward);
        if (safeReward <= 0)
        {
            return;
        }

        int fromCoins = Mathf.Max(0, displayedCoinBalance);
        int toCoins = fromCoins + safeReward;
        displayedCoinBalance = toCoins;
        pendingCoinPersistDelta += safeReward;

        if (useLiteMode && disableCoinCountTweenOnLowEnd)
        {
            UpdateCoinTextInstant(toCoins);
            return;
        }

        AnimateCoinText(fromCoins, toCoins);
    }

    private int GetCurrentCoin()
    {
        int prefsCoin = PlayerPrefs.GetInt(Const.player_coins_key, 0);

        if (PlayerData.Instance == null)
        {
            return prefsCoin;
        }

        int dataCoin = PlayerData.Instance.GetCoinBalance();
        if (dataCoin < prefsCoin)
        {
            PlayerData.Instance.AddCoins(prefsCoin - dataCoin);
        }
        else if (dataCoin > prefsCoin)
        {
            PlayerData.Instance.SpendCoins(dataCoin - prefsCoin);
        }

        return prefsCoin;
    }

    private void UpdateCoinTextInstant(int value)
    {
        if (coinText == null)
        {
            return;
        }

        coinText.text = Mathf.Max(0, value).ToString();
    }

    private void AnimateCoinText(int fromValue, int toValue)
    {
        if (coinText == null)
        {
            return;
        }

        if (coinCountTween != null && coinCountTween.IsActive())
        {
            coinCountTween.Kill();
        }

        int safeFrom = Mathf.Max(0, fromValue);
        int safeTo = Mathf.Max(0, toValue);

        coinText.text = safeFrom.ToString();
        coinCountTween = DOVirtual.Int(safeFrom, safeTo, Mathf.Max(0.05f, coinCountAnimDuration), value =>
        {
            coinText.text = value.ToString();
        }).SetEase(Ease.OutCubic);
    }

    private void KillCoinFlowTweens()
    {
        for (int i = 0; i < coinFlowTweens.Count; i++)
        {
            if (coinFlowTweens[i] != null && coinFlowTweens[i].IsActive())
            {
                coinFlowTweens[i].Kill();
            }
        }

        coinFlowTweens.Clear();

        for (int i = activeCoinPooledObjects.Count - 1; i >= 0; i--)
        {
            ReturnActiveCoinObject(activeCoinPooledObjects[i]);
        }
        activeCoinPooledObjects.Clear();

        activeCoinAnimations = 0;
        onCoinCollectCompleted = null;
    }

    private void RegisterActiveCoinObject(GameObject pooledObject)
    {
        if (pooledObject == null)
        {
            return;
        }

        if (!activeCoinPooledObjects.Contains(pooledObject))
        {
            activeCoinPooledObjects.Add(pooledObject);
        }
    }

    private void ReturnActiveCoinObject(GameObject pooledObject)
    {
        if (pooledObject == null)
        {
            return;
        }

        activeCoinPooledObjects.Remove(pooledObject);
        if (pooledObject.activeInHierarchy)
        {
            pooledObject.transform.DOKill();
            ObjectPoolManager.ReturnObject(pooledObject, ObjectPoolManager.PoolType.Coin);
        }
    }

    private void KillFireworkTweens()
    {
        for (int i = 0; i < fireworkTweens.Count; i++)
        {
            if (fireworkTweens[i] != null && fireworkTweens[i].IsActive())
            {
                fireworkTweens[i].Kill();
            }
        }

        fireworkTweens.Clear();

        for (int i = 0; i < fireworkCleanupTweens.Count; i++)
        {
            if (fireworkCleanupTweens[i] != null && fireworkCleanupTweens[i].IsActive())
            {
                fireworkCleanupTweens[i].Kill();
            }
        }

        fireworkCleanupTweens.Clear();

        for (int i = activeFireworkPooledObjects.Count - 1; i >= 0; i--)
        {
            ReturnActiveFireworkObject(activeFireworkPooledObjects[i]);
        }
        activeFireworkPooledObjects.Clear();
    }

    private void RegisterActiveFireworkObject(GameObject pooledObject)
    {
        if (pooledObject == null)
        {
            return;
        }

        if (!activeFireworkPooledObjects.Contains(pooledObject))
        {
            activeFireworkPooledObjects.Add(pooledObject);
        }
    }

    private void ReturnActiveFireworkObject(GameObject pooledObject)
    {
        if (pooledObject == null)
        {
            return;
        }

        activeFireworkPooledObjects.Remove(pooledObject);
        if (pooledObject.activeInHierarchy)
        {
            ObjectPoolManager.ReturnObject(pooledObject, ObjectPoolManager.PoolType.Particle);
        }
    }

    private void SyncCoinData(int targetCoin)
    {
        int safeCoin = Mathf.Max(0, targetCoin);

        PlayerPrefs.SetInt(Const.player_coins_key, safeCoin);

        if (PlayerData.Instance != null)
        {
            int currentDataCoin = PlayerData.Instance.GetCoinBalance();
            if (currentDataCoin < safeCoin)
            {
                PlayerData.Instance.AddCoins(safeCoin - currentDataCoin);
            }
            else if (currentDataCoin > safeCoin)
            {
                PlayerData.Instance.SpendCoins(currentDataCoin - safeCoin);
            }
        }
    }

    private void CompleteCoinFlow()
    {
        CommitPendingCoinRewardIfAny();

        System.Action callback = onCoinCollectCompleted;
        onCoinCollectCompleted = null;
        callback?.Invoke();
    }

    private void CommitPendingCoinRewardIfAny()
    {
        if (pendingCoinPersistDelta <= 0)
        {
            return;
        }

        int baseCoin = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_coins_key, 0));
        int targetCoin = baseCoin + pendingCoinPersistDelta;
        SyncCoinData(targetCoin);
        PlayerPrefs.Save();

        pendingCoinPersistDelta = 0;
        displayedCoinBalance = targetCoin;
    }

    private void OnNextCoinFlowCompleted()
    {
        int nextLevel = GetClampedCurrentLevel();

        GamePlayController gamePlayController = GamePlayController.Instance;
        if (gamePlayController == null)
        {
            ;
            isNextFlowRunning = false;
            return;
        }

        InputManager inputManager = InputManager.Instance;
        inputManager?.SetInputActive(false);

        if (FireRangeDetector.Instance != null)
        {
            FireRangeDetector.Instance.targetsInRange.Clear();
        }

        if (isFinalLevelWin)
        {
            StartCoroutine(ReturnToMenuAfterCoinDelay(gamePlayController));
            return;
        }

        StartCoroutine(LoadNextLevelAfterCoinDelay(nextLevel, gamePlayController));
    }

    private IEnumerator ReturnToMenuAfterCoinDelay(GamePlayController gamePlayController)
    {
        float waitTime = Mathf.Max(0f, delayBeforeShowLoadingAfterCoin);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        UIManager uiManager = UIManager.Instance;
        if (uiManager != null)
        {
            uiManager.ShowLoadingAndRunNextFrame(() =>
            {
                gamePlayController.CleanupLevel();
                uiManager.HideAllUI();
                uiManager.LoadMenuUI();
                uiManager.ShowLoadingUI2();
            });
            yield break;
        }

        gamePlayController.CleanupLevel();
        isNextFlowRunning = false;
    }

    private IEnumerator LoadNextLevelAfterCoinDelay(int nextLevel, GamePlayController gamePlayController)
    {
        float waitTime = Mathf.Max(0f, delayBeforeShowLoadingAfterCoin);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLoadingAndRunNextFrame(() =>
            {
                UIManager.Instance.HideAllUI();
                UIManager.Instance.LoadInGameUI();
                UIManager.Instance.ShowLoadingUI2();
                gamePlayController.InitLevel(nextLevel);
            });
            yield break;
        }

        gamePlayController.InitLevel(nextLevel);
        isNextFlowRunning = false;
    }

    private bool ShouldUseLiteMode()
    {
        if (!enableLowEndLiteMode)
        {
            return false;
        }

        int memoryMb = SystemInfo.systemMemorySize;
        if (memoryMb > 0 && memoryMb <= Mathf.Max(512, lowEndSystemMemoryMb))
        {
            return true;
        }

        return SystemInfo.processorCount <= Mathf.Max(1, lowEndProcessorCount);
    }
}

