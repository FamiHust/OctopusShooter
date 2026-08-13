using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Solo.MOST_IN_ONE;
using DG.Tweening;

public class GamePlayController : MonoBehaviour
{
    public static GamePlayController Instance { get; private set; }

    [SerializeField] private LevelDataBase levelData;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LevelCameraConfigurator levelCameraConfigurator;
    private GameObject levelGO;
    private SlotBar slotBar;
    private SplineRoute mainRoute;
    private int totalShooters;
    private int destroyedShooters;
    private bool gameEnded;
    private Coroutine pendingLoseRoutine;
    private Coroutine pendingLoseTriggerRoutine;
    private Coroutine pendingWinRoutine;
    private Coroutine pendingIntroRoutine;
    private Coroutine pendingWinFlowRoutine;
    private Coroutine pendingMagicStoneClearRoutine;
    private InGameUIManager cachedInGameUIManager;
    private InputManager cachedInputManager;
    private const int magicStoneClearCost = 3;
    private bool isMagicStoneClearInProgress;
    private bool isGamePausedBySettingPopup;
    private int pendingMagicStoneCoinReward;
    private readonly List<BaseShooter> magicStoneDeckShooterBuffer = new List<BaseShooter>(16);
    private readonly List<BaseShooter> magicStoneAllShooterBuffer = new List<BaseShooter>(128);
    private readonly HashSet<BaseShooter> magicStoneDeckShooterSet = new HashSet<BaseShooter>();
    private readonly List<BlockRowSeedSpawner> magicStoneMainRouteSeederBuffer = new List<BlockRowSeedSpawner>(32);

    [Header("Guaranteed Win / Auto-Finish")]
    [SerializeField, Min(0.05f)] private float autoFinishInterval = 0.2f;

    private Coroutine pendingAutoFinishRoutine;
    private bool isAutoFinishRunning;
    private readonly List<BaseShooter> autoFinishShooterBuffer = new List<BaseShooter>(64);

    [Header("Magic Stone - Beam Timing")]
    [SerializeField, Min(0.02f)] private float magicStoneWaveRowInterval = 0.08f;

    [Header("Magic Stone - Cast Position")]
    [SerializeField] private GameObject magicStoneCastDropPrefab;
    [SerializeField] private float magicStoneCastFixedX = 0f;
    [SerializeField] private float magicStoneCastFixedZ = 0f;
    [SerializeField] private float magicStoneCastOffsetX = 0f;
    [SerializeField] private float magicStoneCastOffsetZ = 0f;

    [Header("Magic Stone - Intro/Outro Motion")]
    [SerializeField, Min(0f)] private float magicStoneCastDropHeight = 2.2f;
    [FormerlySerializedAs("magicStoneCastDropDuration")]
    [SerializeField, Min(0.02f)] private float magicStoneCastIntroDuration = 0.22f;
    [SerializeField] private Ease magicStoneCastIntroEase = Ease.OutCubic;
    [FormerlySerializedAs("magicStoneCastSettleDelay")]
    [SerializeField, Min(0f)] private float magicStoneCastBeamStartDelay = 0.04f;
    [SerializeField, Min(0f)] private float magicStoneCastOutroRiseHeight = 1.3f;
    [SerializeField, Min(0.02f)] private float magicStoneCastOutroDuration = 0.22f;
    [SerializeField] private Ease magicStoneCastOutroEase = Ease.InSine;
    [SerializeField, Min(0f)] private float magicStoneCastImpactYOffset = 0.25f;

    [Header("Magic Stone - Beam Visual")]
    [SerializeField] private Material magicStoneBeamMaterial;
    [SerializeField] private GameObject magicStoneBeamLinePrefab;
    [SerializeField] private Color magicStoneBeamColor = new Color(0.62f, 1f, 0.95f, 0.92f);
    [SerializeField, Min(0.005f)] private float magicStoneBeamWidth = 0.05f;
    [SerializeField, Min(0f)] private float magicStoneBeamTargetYOffset = 0.22f;
    [SerializeField] private Vector3 magicStoneBeamSourceOffset = Vector3.zero;

    [Header("Magic Stone - Beam Blocking")]
    [SerializeField] private LayerMask magicStoneBeamObstacleMask = ~0;
    [SerializeField, Min(0f)] private float magicStoneBeamSourcePadding = 0.06f;
    [SerializeField, Min(0f)] private float magicStoneBeamObstaclePullback = 0.02f;
    [SerializeField] private bool magicStoneBeamHitTriggers = false;

    [SerializeField, Min(0f)] private float magicStoneBeamRevealInterval = 0.04f;
    [SerializeField, Min(0.02f)] private float magicStoneBeamRevealDuration = 0.09f;
    [SerializeField, Min(0f)] private float magicStoneBeamHoldDuration = 0.06f;
    [SerializeField, Min(0f)] private float magicStoneBeamFadeDuration = 0.08f;

    [Header("Magic Stone - Mobile Lite")]
    [SerializeField] private bool enableMagicStoneLiteModeOnLowEnd = true;
    [SerializeField, Min(512)] private int magicStoneLiteLowEndSystemMemoryMb = 3000;
    [SerializeField, Min(1)] private int magicStoneLiteLowEndProcessorCount = 4;
    [SerializeField] private bool magicStoneLiteHideSecondaryBeamVisual = true;
    [SerializeField] private bool magicStoneLiteSkipBeamObstacleCheck = true;
    [SerializeField, Min(8)] private int magicStoneBeamNonAllocHitBufferSize = 24;

    [Header("Magic Stone - Safety & VFX")]
    [SerializeField, Min(0f)] private float magicStoneRefillWaitTimeout = 0f;
    [SerializeField] private GameObject magicStoneSeedExplodeVfxPrefab;
    [SerializeField, Min(0.1f)] private float magicStoneSeedExplodeVfxLifetime = 1.1f;

    [Header("Magic Stone - Row Coin Reward")]
    [SerializeField, Min(0f)] private float magicStoneRowCoinSpawnYOffset = 0.06f;
    [SerializeField, Min(0f)] private float magicStoneRowCoinSpawnSpread = 0.12f;
    [SerializeField, Min(1)] private int magicStoneRowCoinRewardValue = 1;

    [Header("Magic Stone - Shooter Sync")]
    [SerializeField, Min(0f)] private float magicStoneShooterSettleTimeout = 2f;

    [Header("Magic Stone - Audio")]
    [SerializeField, Min(0f)] private float magicStoneAppearSfxVolume = 1f;
    [SerializeField, Min(0f)] private float magicStoneLaserBeamSfxVolume = 1f;
    [SerializeField, Min(0f)] private float magicStoneLaserBeamLoopStartTime = 1f;
    [FormerlySerializedAs("magicStoneLaserBeamAudibleDuration")]
    [SerializeField, Min(0f)] private float magicStoneLaserBeamLoopEndTime = 4f;
    [SerializeField, Min(0f)] private float magicStoneLaserBeamSfxFadeOutDuration = 0.38f;

    private Sequence activeMagicStoneWaveSequence;
    private readonly List<MagicStoneWaveRowData> magicStoneWaveRowBuffer = new List<MagicStoneWaveRowData>(32);
    private readonly List<MagicStoneWaveRowData> magicStoneWaveRowDataPool = new List<MagicStoneWaveRowData>(32);
    private readonly List<LineRenderer> activeMagicStoneBeamLines = new List<LineRenderer>(32);
    private readonly HashSet<int> magicStoneRewardedRowIds = new HashSet<int>();
    private GameObject activeMagicStoneCasterVfx;
    private GameObject magicStoneBeamRuntimeFallbackPrefab;
    private InGameUIManager cachedMagicStoneRewardUI;
    private Camera cachedMagicStoneRewardCamera;
    private bool isMagicStoneLaserSfxPlaying;
    private bool useMagicStoneLiteMode;
    private RaycastHit[] magicStoneBeamHitBuffer;
    private Tween activeMagicStoneBeamCleanupTween;
    private readonly Dictionary<int, Vector3> magicStoneCasterLocalCenterCache = new Dictionary<int, Vector3>(8);
    private readonly HashSet<int> magicStoneCasterNoCenterCache = new HashSet<int>();
    private readonly Dictionary<SeedColor, List<BaseShooter>> magicStoneDeckShootersByColor = new Dictionary<SeedColor, List<BaseShooter>>(8);
    private readonly Dictionary<SeedColor, List<BaseShooter>> magicStoneOtherShootersByColor = new Dictionary<SeedColor, List<BaseShooter>>(8);
    private bool hasMagicStoneAmmoConsumerCache;

    private sealed class MagicStoneWaveRowData
    {
        public BlockRowSeedSpawner seeder;
        public SeedColor color;
        public int seedCount;
        public bool isCleared;
        public readonly List<GameObject> seeds = new List<GameObject>(5);

        public void Reset()
        {
            seeder = null;
            color = SeedColor.Red;
            seedCount = 0;
            isCleared = false;
            seeds.Clear();
        }
    }

    void Awake()
    {
        Instance = this;
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarInit, OnSlotBarInit);
        GameEventHub.Instance.AddListener(GameEventType.OnShooterDisappear, OnShooterDisappear);
        GameEventHub.Instance.AddListener(GameEventType.OnShooterAddedToSlot, OnShooterAddedToSlot);
        GameEventHub.Instance.AddListener(GameEventType.OnGamePause, OnGamePauseChanged);
        cachedInputManager = InputManager.Instance;
        RefreshMagicStoneMobileRuntimeProfile();
    }

    private void Update()
    {
        ApplyMagicStoneWaveSequenceSpeedScale();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (GameEventHub.Instance == null) return;
        GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarInit, OnSlotBarInit);
        GameEventHub.Instance.RemoveListener(GameEventType.OnShooterDisappear, OnShooterDisappear);
        GameEventHub.Instance.RemoveListener(GameEventType.OnShooterAddedToSlot, OnShooterAddedToSlot);
        GameEventHub.Instance.RemoveListener(GameEventType.OnGamePause, OnGamePauseChanged);

        if (magicStoneBeamRuntimeFallbackPrefab != null)
        {
            Destroy(magicStoneBeamRuntimeFallbackPrefab);
            magicStoneBeamRuntimeFallbackPrefab = null;
        }

        KillActiveMagicStoneBeamCleanupTween();

        magicStoneBeamHitBuffer = null;
        magicStoneCasterLocalCenterCache.Clear();
        magicStoneCasterNoCenterCache.Clear();
        ClearMagicStoneAmmoConsumerCache();
    }



    private void OnSlotBarInit(object data)
    {
        slotBar = data as SlotBar;
    }

    private void OnGamePauseChanged(object data)
    {
        bool shouldPause = true;
        if (data is bool boolData)
        {
            shouldPause = boolData;
        }

        isGamePausedBySettingPopup = shouldPause;

        if (activeMagicStoneWaveSequence != null && activeMagicStoneWaveSequence.IsActive())
        {
            if (shouldPause)
            {
                activeMagicStoneWaveSequence.Pause();
                if (isMagicStoneLaserSfxPlaying)
                {
                    AudioManager.Instance?.PauseLongSFX();
                }
            }
            else
            {
                activeMagicStoneWaveSequence.Play();
                ApplyMagicStoneWaveSequenceSpeedScale();
                if (isMagicStoneLaserSfxPlaying)
                {
                    AudioManager.Instance?.ResumeLongSFX();
                }
            }
        }
    }

    private void OnShooterDisappear(object data)
    {
        if (gameEnded) return;

        if (isMagicStoneClearInProgress)
        {
            ClearMagicStoneAmmoConsumerCache();
        }

        int remainingBeforeDisappear = GetRemainingShooterCountIncludingInactive();
        if (remainingBeforeDisappear <= 4)
        {
            InGameUIManager inGameUIManager = GetInGameUIManagerCached();
            inGameUIManager?.ShowLastFourShooterPraise(remainingBeforeDisappear);

            // Phát GoldEarn SFX với pitch và volume tăng dần theo combo: 4=Great(1.0, 1.0), 3=Nice(1.1, 1.15), 2=Awesome(1.2, 1.3), 1=Perfect(1.3, 1.45)
            int comboStep = 4 - Mathf.Clamp(remainingBeforeDisappear, 1, 4);
            float comboPitch = 1f + comboStep * 0.15f;
            float comboVolume = 1f + comboStep * 0.15f;
            AudioManager.Instance?.PlaySFX(Const.goldEarnSFX, comboPitch, comboVolume);
        }

        CancelPendingLoseTrigger();
        destroyedShooters++;
        ;

        // Delay 1 frame Ä‘á»ƒ shooter Ä‘Ã£ Destroy xong khá»i hierarchy trÆ°á»›c khi check win.
        if (pendingWinRoutine == null)
        {
            pendingWinRoutine = StartCoroutine(CheckWinAfterDestroyFrame());
        }
    }

    private int GetRemainingShooterCountIncludingInactive()
    {
        if (totalShooters > 0)
        {
            return Mathf.Max(0, totalShooters - destroyedShooters);
        }

        if (levelGO == null)
        {
            return Mathf.Max(0, totalShooters - destroyedShooters);
        }

        BaseShooter[] shootersInLevel = levelGO.GetComponentsInChildren<BaseShooter>(true);
        if (shootersInLevel == null || shootersInLevel.Length == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < shootersInLevel.Length; i++)
        {
            BaseShooter shooter = shootersInLevel[i];
            if (shooter == null)
            {
                continue;
            }

            if (shooter.gameObject == null)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private IEnumerator CheckWinAfterDestroyFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        pendingWinRoutine = null;

        if (gameEnded)
            yield break;

        if (CheckWinCondition())
        {
            TriggerWin();
            yield break;
        }

        TryTriggerGuaranteedWin();
    }

    private void OnShooterAddedToSlot(object data)
    {
        if (gameEnded) return;

        if (isMagicStoneClearInProgress)
        {
            ClearMagicStoneAmmoConsumerCache();
        }

        CancelPendingLoseTrigger();
        // Kiá»ƒm tra lose khi thÃªm shooter vÃ o slot
        CallLoseCheckDelayed();
    }

    /// <summary>
    /// Gá»i lose check sau 0.5 giÃ¢y (cho phÃ­ lÃ  Ä‘á»ƒ animation hoÃ n toÃ n)
    /// </summary>
    private void CallLoseCheckDelayed()
    {
        if (gameEnded) return;
        if (pendingLoseRoutine != null) return;
        pendingLoseRoutine = StartCoroutine(CheckLoseDelayed());
    }

    private IEnumerator CheckLoseDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        pendingLoseRoutine = null;

        if (gameEnded)
            yield break;

        if (CheckWinCondition())
        {
            TriggerWin();
            yield break;
        }

        if (TryTriggerGuaranteedWin())
        {
            yield break;
        }

        if (!gameEnded && CheckLoseCondition())
        {
            if (pendingLoseTriggerRoutine == null)
            {
                pendingLoseTriggerRoutine = StartCoroutine(TriggerLoseWithDelay());
            }
        }
        else
        {
            CancelPendingLoseTrigger();
        }
    }

    /// <summary>
    /// Delay lose event 1 giÃ¢y trÆ°á»›c khi gá»i event
    /// </summary>
    private IEnumerator TriggerLoseWithDelay()
    {
        ;
        yield return new WaitForSeconds(1f);
        pendingLoseTriggerRoutine = null;

        if (gameEnded)
        {
            yield break;
        }

        if (CheckWinCondition())
        {
            TriggerWin();
            yield break;
        }

        // Re-check Ä‘á»ƒ trÃ¡nh lose giáº£ khi state Ä‘Ã£ Ä‘á»•i trong thá»i gian delay
        // (vÃ­ dá»¥ refill vá»«a xong, shooter vá»«a báº¯n Ä‘Æ°á»£c, main route cÃ²n slot trá»‘ng).
        if (!CheckLoseCondition())
        {
            yield break;
        }

        TriggerLose();
    }

    private bool CheckLoseCondition()
    {
        // Äiá»u kiá»‡n 1: SlotBar Ä‘áº§y
        if (slotBar == null || !slotBar.IsFull()) return false;

        // Äiá»u kiá»‡n 2: Main route Ä‘ang cÃ³ rows (khÃ´ng rá»—ng)
        if (mainRoute == null) return false;
        List<GameObject> mainRows = mainRoute.GetActiveBlockRows();
        if (mainRows == null || mainRows.Count == 0) return false;

        // Äiá»u kiá»‡n 3: KhÃ´ng cÃ³ mÃ u shooter nÃ o khá»›p vá»›i mÃ u seed thá»±c táº¿ trÃªn main route
        var shooters = slotBar.GetAllShooters();
        if (shooters == null || shooters.Count == 0) return false;

        var mainSeedColors = new HashSet<SeedColor>();
        foreach (var rowGO in mainRows)
        {
            if (rowGO == null) continue;
            var seeder = rowGO.GetComponent<BlockRowSeedSpawner>();
            if (seeder == null)
            {
                continue;
            }

            // Main route cÃ²n báº¥t ká»³ slot trá»‘ng nÃ o thÃ¬ chÆ°a thá»ƒ káº¿t luáº­n báº¿ táº¯c mÃ u.
            if (seeder.HasEmptySlot())
            {
                return false;
            }

            if (seeder.GetSeedCount() <= 0)
            {
                return false;
            }

            for (int i = 0; i < seeder.GetMaxSeedCount(); i++)
            {
                GameObject seed = seeder.GetSeed(i);
                if (seed == null)
                {
                    continue;
                }

                SeedInfo seedInfo = seed.GetComponent<SeedInfo>();
                if (seedInfo != null)
                {
                    mainSeedColors.Add(seedInfo.GetSeedColor());
                }
                else
                {
                    // Fallback náº¿u seed thiáº¿u SeedInfo.
                    mainSeedColors.Add(seeder.GetCurrentColor());
                }
            }
        }

        // Náº¿u main route khÃ´ng cÃ²n seed nÃ o â†’ khÃ´ng pháº£i lose do báº¿ táº¯c mÃ u.
        if (mainSeedColors.Count == 0) return false;

        foreach (var shooter in shooters)
        {
            if (shooter == null) continue;
            if (mainSeedColors.Contains(shooter.GetTargetColor())) return false;
        }
        return true;
    }

    private bool CheckWinCondition()
    {
        return IsShooterClearConditionMet();
    }

    private bool IsShooterClearConditionMet()
    {
        // Äiá»u kiá»‡n win gá»‘c: khÃ´ng cÃ²n shooter nÃ o tá»“n táº¡i trong level.
        if (levelGO == null)
            return false;

        if (totalShooters > 0)
        {
            return destroyedShooters >= totalShooters;
        }

        BaseShooter[] shootersInLevel = levelGO.GetComponentsInChildren<BaseShooter>(true);
        for (int i = 0; i < shootersInLevel.Length; i++)
        {
            if (shootersInLevel[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    private void TriggerWin()
    {
        gameEnded = true;
        StopPendingMagicStoneClearProcess();
        if (pendingAutoFinishRoutine != null)
        {
            StopCoroutine(pendingAutoFinishRoutine);
            pendingAutoFinishRoutine = null;
        }
        isAutoFinishRunning = false;
        if (pendingWinRoutine != null)
        {
            StopCoroutine(pendingWinRoutine);
            pendingWinRoutine = null;
        }
        if (pendingLoseRoutine != null)
        {
            StopCoroutine(pendingLoseRoutine);
            pendingLoseRoutine = null;
        }
        CancelPendingLoseTrigger();
        if (pendingIntroRoutine != null)
        {
            StopCoroutine(pendingIntroRoutine);
            pendingIntroRoutine = null;
        }
        if (pendingWinFlowRoutine != null)
        {
            StopCoroutine(pendingWinFlowRoutine);
            pendingWinFlowRoutine = null;
        }

        ;

        pendingWinFlowRoutine = StartCoroutine(TriggerWinFlow());
    }

    private IEnumerator TriggerWinFlow()
    {
        ConveyorArrowSystem arrowSystem = levelGO != null ? levelGO.GetComponentInChildren<ConveyorArrowSystem>(true) : null;
        if (arrowSystem != null)
        {
            arrowSystem.DisableArrowsForOutro();
        }

        InGameUIManager inGameUIManager = GetInGameUIManagerCached();
        inGameUIManager?.PlayHUDOutroImmediatelyOnWin();

        LevelElementAnimator animator = levelGO != null ? levelGO.GetComponentInChildren<LevelElementAnimator>(true) : null;
        if (animator != null)
        {
            animator.PlayOutroAnimation();
            float outroDuration = Mathf.Max(0f, animator.GetOutroDuration());
            if (outroDuration > 0f)
            {
                yield return new WaitForSeconds(outroDuration);
            }
        }

        pendingWinFlowRoutine = null;

        AudioManager.Instance?.PlaySFX(Const.winSFX);

        CommitPendingMagicStoneCoinReward();

    // Magic stone is level-local only, clear value when the level ends.
    BaseShooter.ResetMagicStoneForCurrentLevel();

        // Trigger win event sau khi outro hoÃ n táº¥t.
        GameEventHub.Instance.Invoke(GameEventType.OnGameWin);

        // Gá»i GameManager Ä‘á»ƒ update level/coins vÃ  show win UI ngay sau outro.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelWon(0f);
        }
    }

    private void TriggerLose()
    {
        if (gameEnded) return;

        gameEnded = true;
        StopPendingMagicStoneClearProcess();
        if (pendingAutoFinishRoutine != null)
        {
            StopCoroutine(pendingAutoFinishRoutine);
            pendingAutoFinishRoutine = null;
        }
        isAutoFinishRunning = false;
        mainRoute?.SetTutorialPaused(true);
        if (pendingWinRoutine != null)
        {
            StopCoroutine(pendingWinRoutine);
            pendingWinRoutine = null;
        }
        if (pendingLoseRoutine != null)
        {
            StopCoroutine(pendingLoseRoutine);
            pendingLoseRoutine = null;
        }
        CancelPendingLoseTrigger();
        ;

        AudioManager.Instance?.PlaySFX(Const.loseSFX);
        TryPlayLoseHaptic();

        // Magic stone is level-local only, clear value when the level ends.
        BaseShooter.ResetMagicStoneForCurrentLevel();
        ResetPendingMagicStoneCoinReward();
        
        // Trigger lose event (UI/popup sáº½ listen vÃ o event nÃ y)
        GameEventHub.Instance.Invoke(GameEventType.OnGameLose);
        
        // Sau Ä‘Ã³ gá»i GameManager Ä‘á»ƒ subtract health (lÃ m sau popup)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelLost();
        }
    }

    private static void TryPlayLoseHaptic()
    {
        int defaultEnabledValue = MOST_HapticFeedback.HapticsEnabled ? 1 : 0;
        bool isEnabled = PlayerPrefs.GetInt(Const.player_vibration_key, defaultEnabledValue) == 1;
        if (MOST_HapticFeedback.HapticsEnabled != isEnabled)
        {
            MOST_HapticFeedback.HapticsEnabled = isEnabled;
        }

        if (!isEnabled)
        {
            return;
        }

        MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.Failure, 0.2f);
    }

    /// <summary>
    /// Dá»n dáº¹p level hiá»‡n táº¡i: há»§y levelGO, reset state, dá»«ng coroutine.
    /// DÃ¹ng khi vá» menu hoáº·c trÆ°á»›c khi load level má»›i.
    /// </summary>
    public void CleanupLevel()
    {
        SpeedMultiplierManager.ResetSpeedStatic();
        StopPendingMagicStoneClearProcess();
        if (pendingAutoFinishRoutine != null)
        {
            StopCoroutine(pendingAutoFinishRoutine);
            pendingAutoFinishRoutine = null;
        }
        isAutoFinishRunning = false;
        BaseShooter.ResetMagicStoneForCurrentLevel();
        ResetPendingMagicStoneCoinReward();
        cachedMagicStoneRewardUI = null;
        cachedMagicStoneRewardCamera = null;
        magicStoneCasterLocalCenterCache.Clear();
        magicStoneCasterNoCenterCache.Clear();
        ClearMagicStoneAmmoConsumerCache();

        gameEnded = false;
        if (pendingLoseRoutine != null)
        {
            StopCoroutine(pendingLoseRoutine);
            pendingLoseRoutine = null;
        }
        CancelPendingLoseTrigger();
        if (pendingWinRoutine != null)
        {
            StopCoroutine(pendingWinRoutine);
            pendingWinRoutine = null;
        }
        if (pendingIntroRoutine != null)
        {
            StopCoroutine(pendingIntroRoutine);
            pendingIntroRoutine = null;
        }
        if (pendingWinFlowRoutine != null)
        {
            StopCoroutine(pendingWinFlowRoutine);
            pendingWinFlowRoutine = null;
        }
        totalShooters = 0;
        mainRoute = null;
        destroyedShooters = 0;
        cachedInGameUIManager = null;
        if (levelGO != null)
        {
            SideRouteSeedExchangeMechanic[] exchangeMechanics = levelGO.GetComponentsInChildren<SideRouteSeedExchangeMechanic>(true);
            if (exchangeMechanics != null)
            {
                for (int i = 0; i < exchangeMechanics.Length; i++)
                {
                    SideRouteSeedExchangeMechanic exchangeMechanic = exchangeMechanics[i];
                    exchangeMechanic?.AbortExchangeForLevelCleanup();
                }
            }

            // Release pooled block rows before Destroy(levelGO), so next InitLevel can reuse in the same frame.
            SplineRoute[] routes = levelGO.GetComponentsInChildren<SplineRoute>(true);
            if (routes != null)
            {
                for (int i = 0; i < routes.Length; i++)
                {
                    SplineRoute route = routes[i];
                    route?.ReleaseAllRowsToPoolNow();
                }
            }

            // Return pooled seeds immediately so the next level can reuse them in the same frame.
            BlockRowSeedSpawner[] seedSpawners = levelGO.GetComponentsInChildren<BlockRowSeedSpawner>(true);
            if (seedSpawners != null)
            {
                for (int i = 0; i < seedSpawners.Length; i++)
                {
                    BlockRowSeedSpawner seedSpawner = seedSpawners[i];
                    if (seedSpawner != null)
                    {
                        seedSpawner.ClearAllSeedsOnly(true);
                    }
                }
            }

            // Mobile-friendly cleanup rule: keep inactive seeds per color in multiples of 5.
            ObjectPoolManager.NormalizeInactiveSeedPoolByColor(5);

            // Long session maintenance: trim heavy inactive pools to avoid progressive FPS decay.
            ObjectPoolManager.TrimPoolsForLongSession();

            Destroy(levelGO);
            levelGO = null;
        }
    }

    public void InitLevel(int level)
    {
        // Luon bat dau van moi o toc do x1.
        CleanupLevel();

        LevelCameraConfigurator cameraConfigurator = levelCameraConfigurator != null
            ? levelCameraConfigurator
            : LevelCameraConfigurator.Instance;
        cameraConfigurator?.ApplyForLevel(level);

        SetInputLocked(true);

        GameObject levelPrefab = levelData.GetLevelPrefab(level);
        if (levelPrefab == null)
        {
            ;
            return;
        }

        levelGO = Instantiate(levelPrefab, this.transform);
        LevelElementAnimator animator = levelGO.GetComponentInChildren<LevelElementAnimator>(true);
        if (animator != null)
        {
            animator.PrepareInitialPose();
        }

        LevelController levelController = levelGO.GetComponent<LevelController>();
        if (levelController != null)
        {
            levelController.InitLevel(mainCamera);
            totalShooters = levelController.GetShooterCount();
            mainRoute = levelController.GetMainRoute();
            ;
        }

        if (level == 1)
        {
            TutorialManager.Instance?.PrewarmForLevelRuntime();
        }

        if (animator != null)
        {
            pendingIntroRoutine = StartCoroutine(PlayIntroNextFrame(animator, level));
        }
        else
        {
            InGameUIManager inGameUIManager = GetInGameUIManagerCached();
            inGameUIManager?.PlayHardLevelAfterLevelIntro();
            SetInputLocked(false);
            TutorialManager.Instance?.CheckAndStartTutorial(level);
        }
    }

    public int GetMagicStoneClearCost()
    {
        return magicStoneClearCost;
    }

    public bool IsMagicStoneClearRunning()
    {
        return isMagicStoneClearInProgress;
    }

    public bool CanActivateMagicStoneClear()
    {
        if (gameEnded || isMagicStoneClearInProgress)
        {
            return false;
        }

        if (mainRoute == null)
        {
            return false;
        }

        if (BaseShooter.GetCollectedMagicStoneForCurrentLevel() < GetMagicStoneClearCost())
        {
            return false;
        }

        return HasClearableSeedOnMainRoute();
    }

    public bool TryActivateMagicStoneClearFromUI()
    {
        if (!CanActivateMagicStoneClear())
        {
            return false;
        }

        if (!BaseShooter.TryConsumeMagicStoneForCurrentLevel(GetMagicStoneClearCost()))
        {
            return false;
        }

        StopPendingMagicStoneClearProcess();
        pendingMagicStoneClearRoutine = StartCoroutine(ClearMainRouteSeedsByMagicStoneRoutine());
        return true;
    }

    public bool CanActivateMagicStoneClearDebugBypassCost()
    {
        if (gameEnded || isMagicStoneClearInProgress)
        {
            return false;
        }

        if (mainRoute == null)
        {
            return false;
        }

        return HasClearableSeedOnMainRoute();
    }

    public bool TryActivateMagicStoneClearDebugFromUI()
    {
        if (!CanActivateMagicStoneClearDebugBypassCost())
        {
            return false;
        }

        StopPendingMagicStoneClearProcess();
        pendingMagicStoneClearRoutine = StartCoroutine(ClearMainRouteSeedsByMagicStoneRoutine());
        return true;
    }

    public void AddPendingMagicStoneCoinReward(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            return;
        }

        pendingMagicStoneCoinReward += safeAmount;
    }

    public int GetPendingMagicStoneCoinReward()
    {
        return Mathf.Max(0, pendingMagicStoneCoinReward);
    }

    private void CommitPendingMagicStoneCoinReward()
    {
        int reward = Mathf.Max(0, pendingMagicStoneCoinReward);
        if (reward <= 0)
        {
            return;
        }

        int currentCoins = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_coins_key, 0));
        PlayerPrefs.SetInt(Const.player_coins_key, currentCoins + reward);
        PlayerPrefs.Save();
        pendingMagicStoneCoinReward = 0;
    }

    private void ResetPendingMagicStoneCoinReward()
    {
        pendingMagicStoneCoinReward = 0;
    }

    private bool HasClearableSeedOnMainRoute()
    {
        if (mainRoute == null)
        {
            return false;
        }

        if (mainRoute.FillActiveBlockRowSeeders(magicStoneMainRouteSeederBuffer) <= 0)
        {
            return false;
        }

        for (int i = 0; i < magicStoneMainRouteSeederBuffer.Count; i++)
        {
            BlockRowSeedSpawner seeder = magicStoneMainRouteSeederBuffer[i];
            if (seeder == null)
            {
                continue;
            }

            if (seeder.GetSeedCount() > 0)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator ClearMainRouteSeedsByMagicStoneRoutine()
    {
        yield return WaitForSettingPauseReleased();
        SetMagicStoneClearInProgress(true);
        bool routesPaused = false;

        yield return WaitForRefillCompleteBeforeMagicPause();
        if (gameEnded)
        {
            pendingMagicStoneClearRoutine = null;
            SetMagicStoneClearInProgress(false);
            yield break;
        }

        // Let shooters finish only the row they are already destroying before MagicStone snapshots rows.
        yield return WaitForShootersFinishCurrentRowsBeforeMagicStone();

        SetAllRoutesMechanicPaused(true);
        routesPaused = true;

        // Build target rows only after refill settled and routes are paused,
        // so newly refilled rows are included and no new movement starts mid-cast.
        yield return WaitForSettingPauseReleased();
        yield return WaitOneFrameForMagicStoneRowSnapshot();

        yield return WaitForSettingPauseReleased();
        yield return PlayMagicStoneMainRouteWaveClearAnimation();

        if (routesPaused)
        {
            SetAllRoutesMechanicPaused(false);
        }

        pendingMagicStoneClearRoutine = null;
        SetMagicStoneClearInProgress(false);

        if (!gameEnded)
        {
            CancelPendingLoseTrigger();
            CallLoseCheckDelayed();
        }
    }

    private void StopPendingMagicStoneClearProcess()
    {
        if (pendingMagicStoneClearRoutine != null)
        {
            StopCoroutine(pendingMagicStoneClearRoutine);
            pendingMagicStoneClearRoutine = null;
        }

        if (activeMagicStoneWaveSequence != null && activeMagicStoneWaveSequence.IsActive())
        {
            activeMagicStoneWaveSequence.Kill(false);
        }

        activeMagicStoneWaveSequence = null;
        magicStoneWaveRowBuffer.Clear();
        ClearMagicStoneAmmoConsumerCache();
        KillActiveMagicStoneBeamCleanupTween();
        CleanupMagicStoneVisualsImmediate();
        StopMagicStoneLaserBeamSfxImmediate();

        SetMagicStoneClearInProgress(false);
        SetAllRoutesMechanicPaused(false);
    }

    private void SetMagicStoneClearInProgress(bool inProgress)
    {
        if (isMagicStoneClearInProgress == inProgress)
        {
            return;
        }

        isMagicStoneClearInProgress = inProgress;
        GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
    }

    private IEnumerator WaitForShootersFinishCurrentRowsBeforeMagicStone()
    {
        float timeout = Mathf.Max(0f, magicStoneShooterSettleTimeout);
        float elapsed = 0f;

        while (!gameEnded && IsAnyShooterFinishingCurrentRowForMagicStone())
        {
            if (isGamePausedBySettingPopup)
            {
                yield return null;
                continue;
            }

            if (timeout > 0f && elapsed >= timeout)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool IsAnyShooterFinishingCurrentRowForMagicStone()
    {
        magicStoneAllShooterBuffer.Clear();
        BaseShooter.FillRegisteredShooterBuffer(magicStoneAllShooterBuffer, false);

        for (int i = 0; i < magicStoneAllShooterBuffer.Count; i++)
        {
            BaseShooter shooter = magicStoneAllShooterBuffer[i];
            if (shooter != null && shooter.IsFinishingCurrentRowForMagicStoneGate())
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator WaitForRefillCompleteBeforeMagicPause()
    {
        SplineController splineController = SplineController.Instance;
        if (splineController == null)
        {
            yield break;
        }

        float timeout = Mathf.Max(0f, magicStoneRefillWaitTimeout);
        float elapsed = 0f;

        while (!gameEnded && splineController.IsAnyRefillInProgress())
        {
            if (isGamePausedBySettingPopup)
            {
                yield return null;
                continue;
            }

            if (timeout > 0f && elapsed >= timeout)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Let the refill sequence settle one frame before pausing all routes.
        if (!gameEnded)
        {
            yield return null;
        }
    }

    private IEnumerator WaitOneFrameForMagicStoneRowSnapshot()
    {
        if (isGamePausedBySettingPopup)
        {
            yield return WaitForSettingPauseReleased();
        }

        if (gameEnded)
        {
            yield break;
        }

        yield return null;
    }

    private void SetAllRoutesMechanicPaused(bool paused)
    {
        SplineController splineController = SplineController.Instance;
        if (splineController != null)
        {
            SplineRoute controllerMainRoute = splineController.GetMainRoute();
            controllerMainRoute?.SetMechanicPaused(paused);

            SplineRoute[] sideRoutes = splineController.GetSideRoutes();
            if (sideRoutes != null)
            {
                for (int i = 0; i < sideRoutes.Length; i++)
                {
                    sideRoutes[i]?.SetMechanicPaused(paused);
                }
            }
        }

        if (mainRoute != null)
        {
            mainRoute.SetMechanicPaused(paused);
        }
    }

    private IEnumerator PlayMagicStoneMainRouteWaveClearAnimation()
    {
        if (isGamePausedBySettingPopup)
        {
            yield return WaitForSettingPauseReleased();
        }

        magicStoneRewardedRowIds.Clear();

        int rowCount = BuildMagicStoneWaveRowsByIndex();
        if (rowCount <= 0)
        {
            ClearMagicStoneAmmoConsumerCache();
            CleanupMagicStoneVisualsImmediate();
            yield break;
        }

        BuildMagicStoneAmmoConsumerCache();

        float introDuration = Mathf.Max(0.02f, magicStoneCastIntroDuration);
        float beamStartDelay = Mathf.Max(0f, magicStoneCastBeamStartDelay);
        float outroDuration = Mathf.Max(0.02f, magicStoneCastOutroDuration);
        float beamRevealInterval = Mathf.Max(0f, magicStoneBeamRevealInterval);
        float beamRevealDuration = Mathf.Max(0.02f, magicStoneBeamRevealDuration);
        float beamHoldDuration = Mathf.Max(0f, magicStoneBeamHoldDuration);
        float beamFadeDuration = Mathf.Max(0f, magicStoneBeamFadeDuration);
        float rowStepInterval = Mathf.Max(0.01f, magicStoneWaveRowInterval);
        float beamSweepDuration = Mathf.Max(beamRevealDuration, rowStepInterval);
        const float seedClearDelayPadding = 0.02f;

        Vector3 impactPosition = GetMagicStoneCastImpactPosition();
        Vector3 castStartPosition = impactPosition + Vector3.up * Mathf.Max(0f, magicStoneCastDropHeight);
        SpawnMagicStoneCasterVisual(castStartPosition);

        Sequence castSequence = DOTween.Sequence().SetUpdate(true);
        activeMagicStoneWaveSequence = castSequence;
        ApplyMagicStoneWaveSequenceSpeedScale();

        if (isGamePausedBySettingPopup)
        {
            castSequence.Pause();
        }

        if (activeMagicStoneCasterVfx != null)
        {
            castSequence.AppendCallback(PlayMagicStoneAppearSfx);
            castSequence.Append(activeMagicStoneCasterVfx.transform.DOMove(impactPosition, introDuration).SetEase(magicStoneCastIntroEase).SetUpdate(true));
        }
        else
        {
            castSequence.AppendInterval(introDuration);
        }

        if (beamStartDelay > 0f)
        {
            castSequence.AppendInterval(beamStartDelay);
        }

        float beamStageStart = castSequence.Duration(false);
        castSequence.InsertCallback(beamStageStart, StartMagicStoneLaserBeamSfx);
        Vector3 beamStart = GetMagicStoneBeamSourcePosition(impactPosition);
        bool useLiteMode = useMagicStoneLiteMode;
        bool skipObstacleCheck = useLiteMode && magicStoneLiteSkipBeamObstacleCheck;

        LineRenderer primaryBeam = CreateMagicStoneBeamLine(beamStart, beamStart);
        int secondaryBeamCount = Mathf.Max(0, (rowCount - 1) / 2);
        bool showSecondaryBeamVisual = !useLiteMode || !magicStoneLiteHideSecondaryBeamVisual;
        LineRenderer secondaryBeam = (secondaryBeamCount > 0 && showSecondaryBeamVisual)
            ? CreateMagicStoneBeamLine(beamStart, beamStart)
            : null;

        int primaryBeamLowerBound = secondaryBeamCount;
        int totalSteps = Mathf.Max(rowCount - primaryBeamLowerBound, secondaryBeamCount);
        float lastHitTime = beamStageStart;
        Vector3 primaryCurrentTarget = beamStart;
        Vector3 secondaryCurrentTarget = beamStart;

        for (int step = 0; step < totalSteps; step++)
        {
            float revealStartTime = beamStageStart + (step * rowStepInterval);

            int primaryIndex = rowCount - 1 - step;
            if (primaryBeam != null && primaryIndex >= primaryBeamLowerBound)
            {
                MagicStoneWaveRowData primaryRowData = magicStoneWaveRowBuffer[primaryIndex];
                if (primaryRowData != null && primaryRowData.seeder != null && primaryRowData.seedCount > 0)
                {
                    LineRenderer capturedBeam = primaryBeam;
                    Vector3 capturedStart = beamStart;
                    Vector3 capturedFromTarget = primaryCurrentTarget;
                    Vector3 primaryDesiredTarget = primaryRowData.seeder.transform.position + Vector3.up * Mathf.Max(0f, magicStoneBeamTargetYOffset);
                    float beamWidthForObstacle = capturedBeam != null
                        ? Mathf.Max(capturedBeam.startWidth, capturedBeam.endWidth)
                        : Mathf.Max(0.005f, magicStoneBeamWidth);
                    Vector3 capturedTarget = skipObstacleCheck
                        ? primaryDesiredTarget
                        : ResolveMagicStoneBeamTarget(capturedStart, primaryDesiredTarget, beamWidthForObstacle);
                    MagicStoneWaveRowData capturedRowData = primaryRowData;

                    if (capturedBeam != null)
                    {
                        castSequence.InsertCallback(revealStartTime, () =>
                        {
                            RevealMagicStoneBeam(capturedBeam, capturedStart, capturedFromTarget, capturedTarget, beamSweepDuration);
                        });
                    }

                    float hitTime = revealStartTime + beamSweepDuration;
                    castSequence.InsertCallback(hitTime, () =>
                    {
                        TriggerMagicStoneRowClear(capturedRowData);
                    });

                    if (hitTime > lastHitTime)
                    {
                        lastHitTime = hitTime;
                    }

                    primaryCurrentTarget = capturedTarget;
                }
            }

            int secondaryIndex = step;
            if (secondaryIndex >= 0 && secondaryIndex < secondaryBeamCount)
            {
                MagicStoneWaveRowData secondaryRowData = magicStoneWaveRowBuffer[secondaryIndex];
                if (secondaryRowData != null && secondaryRowData.seeder != null && secondaryRowData.seedCount > 0)
                {
                    LineRenderer capturedBeam = secondaryBeam;
                    Vector3 capturedStart = beamStart;
                    Vector3 capturedFromTarget = secondaryCurrentTarget;
                    Vector3 secondaryDesiredTarget = secondaryRowData.seeder.transform.position + Vector3.up * Mathf.Max(0f, magicStoneBeamTargetYOffset);
                    float beamWidthForObstacle = capturedBeam != null
                        ? Mathf.Max(capturedBeam.startWidth, capturedBeam.endWidth)
                        : Mathf.Max(0.005f, magicStoneBeamWidth);
                    Vector3 capturedTarget = skipObstacleCheck
                        ? secondaryDesiredTarget
                        : ResolveMagicStoneBeamTarget(capturedStart, secondaryDesiredTarget, beamWidthForObstacle);
                    MagicStoneWaveRowData capturedRowData = secondaryRowData;

                    if (capturedBeam != null)
                    {
                        castSequence.InsertCallback(revealStartTime, () =>
                        {
                            RevealMagicStoneBeam(capturedBeam, capturedStart, capturedFromTarget, capturedTarget, beamSweepDuration);
                        });
                    }

                    float hitTime = revealStartTime + beamSweepDuration;
                    castSequence.InsertCallback(hitTime, () =>
                    {
                        TriggerMagicStoneRowClear(capturedRowData);
                    });

                    if (hitTime > lastHitTime)
                    {
                        lastHitTime = hitTime;
                    }

                    secondaryCurrentTarget = capturedTarget;
                }
            }
        }

        float fadeStartTime = lastHitTime + beamHoldDuration;
        castSequence.InsertCallback(fadeStartTime, () =>
        {
            FadeOutMagicStoneBeams(beamFadeDuration);
            StopMagicStoneLaserBeamSfxSmooth();
        });

        float beamFadeEndTime = fadeStartTime + beamFadeDuration;
        float outroStartTime = beamFadeEndTime;
        float outroEndTime = outroStartTime;

        if (activeMagicStoneCasterVfx != null)
        {
            Vector3 outroTarget = impactPosition + Vector3.up * Mathf.Max(0f, magicStoneCastOutroRiseHeight);
            castSequence.InsertCallback(outroStartTime, PlayMagicStoneAppearSfx);
            castSequence.Insert(outroStartTime,
                activeMagicStoneCasterVfx.transform.DOMove(outroTarget, outroDuration)
                    .SetEase(magicStoneCastOutroEase)
                    .SetUpdate(true));

            outroEndTime = outroStartTime + outroDuration;
        }

        float clearCompletionTime = lastHitTime + seedClearDelayPadding;
        float totalDuration = Mathf.Max(Mathf.Max(clearCompletionTime, beamFadeEndTime), outroEndTime) + 0.03f;
        float currentDuration = castSequence.Duration(false);
        if (currentDuration < totalDuration)
        {
            castSequence.AppendInterval(totalDuration - currentDuration);
        }

        yield return castSequence.WaitForCompletion();

        activeMagicStoneWaveSequence = null;
        magicStoneWaveRowBuffer.Clear();
        magicStoneRewardedRowIds.Clear();
        ClearMagicStoneAmmoConsumerCache();
        CleanupMagicStoneVisualsImmediate();
    }

    private IEnumerator WaitForSettingPauseReleased()
    {
        while (!gameEnded && isGamePausedBySettingPopup)
        {
            yield return null;
        }
    }

    private void ApplyMagicStoneWaveSequenceSpeedScale()
    {
        if (activeMagicStoneWaveSequence == null || !activeMagicStoneWaveSequence.IsActive())
        {
            return;
        }

        float speedScale = Mathf.Max(0.1f, SpeedMultiplierManager.GetBaseMultiplier());
        if (Mathf.Abs(activeMagicStoneWaveSequence.timeScale - speedScale) <= 0.001f)
        {
            return;
        }

        activeMagicStoneWaveSequence.timeScale = speedScale;
    }

    private void PlayMagicStoneAppearSfx()
    {
        AudioManager.Instance?.PlaySFX(Const.magicStoneAppearSFX, 1f, Mathf.Max(0f, magicStoneAppearSfxVolume));
    }

    private void StartMagicStoneLaserBeamSfx()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            return;
        }

        audioManager.PlayLongSFXLoopSegment(
            Const.laserBeamSFX,
            Mathf.Max(0f, magicStoneLaserBeamSfxVolume),
            Mathf.Max(0f, magicStoneLaserBeamLoopStartTime),
            Mathf.Max(0f, magicStoneLaserBeamLoopEndTime)
        );
        isMagicStoneLaserSfxPlaying = true;
    }

    private void StopMagicStoneLaserBeamSfxSmooth()
    {
        if (!isMagicStoneLaserSfxPlaying)
        {
            return;
        }

        isMagicStoneLaserSfxPlaying = false;
        AudioManager.Instance?.StopLongSFXSmooth(Mathf.Max(0f, magicStoneLaserBeamSfxFadeOutDuration));
    }

    private void StopMagicStoneLaserBeamSfxImmediate()
    {
        if (!isMagicStoneLaserSfxPlaying)
        {
            return;
        }

        isMagicStoneLaserSfxPlaying = false;
        AudioManager.Instance?.StopLongSFX();
    }

    private void TriggerMagicStoneRowClear(MagicStoneWaveRowData rowData)
    {
        if (rowData == null || rowData.seeder == null || rowData.seedCount <= 0 || rowData.isCleared)
        {
            return;
        }

        rowData.isCleared = true;
        BlockRowSeedSpawner rowSeeder = rowData.seeder;
        int rowSeedCount = rowData.seedCount;
        Vector3 rowCoinSpawnPosition = rowSeeder.transform.position;

        ConsumeAmmoByMagicStoneClear(rowData.color, rowSeedCount);

        for (int seedIndex = 0; seedIndex < rowData.seeds.Count; seedIndex++)
        {
            GameObject seed = rowData.seeds[seedIndex];
            if (seed == null)
            {
                continue;
            }

            SpawnMagicStoneSeedExplodeVfx(seed.transform.position);
            rowSeeder.DestroySpecificSeed(seed);
            GameEventHub.Instance?.Invoke(GameEventType.OnSeedDestroyed, 1);
        }

        GameEventHub.Instance?.Invoke(GameEventType.OnSeedRowDestroyed, rowSeedCount);

        int rowId = rowSeeder.GetInstanceID();
        if (magicStoneRewardedRowIds.Add(rowId))
        {
            SpawnMagicStoneRowCoinFromClearedRow(rowCoinSpawnPosition);
        }
    }

    private Vector3 GetMagicStoneCastImpactPosition()
    {
        float baseY = mainRoute != null ? mainRoute.transform.position.y : transform.position.y;
        return new Vector3(
            magicStoneCastFixedX + magicStoneCastOffsetX,
            baseY + Mathf.Max(0f, magicStoneCastImpactYOffset),
            magicStoneCastFixedZ + magicStoneCastOffsetZ
        );
    }

    private Vector3 GetMagicStoneBeamSourcePosition(Vector3 fallbackPosition)
    {
        Vector3 source = fallbackPosition;

        if (activeMagicStoneCasterVfx != null)
        {
            if (TryGetVisualCenter(activeMagicStoneCasterVfx, out Vector3 center))
            {
                source = center;
            }
            else
            {
                source = activeMagicStoneCasterVfx.transform.position;
            }
        }

        return source + magicStoneBeamSourceOffset;
    }

    private bool TryGetVisualCenter(GameObject target, out Vector3 center)
    {
        center = Vector3.zero;
        if (target == null)
        {
            return false;
        }

        int targetId = target.GetInstanceID();
        if (magicStoneCasterLocalCenterCache.TryGetValue(targetId, out Vector3 cachedLocalCenter))
        {
            center = target.transform.TransformPoint(cachedLocalCenter);
            return true;
        }

        if (magicStoneCasterNoCenterCache.Contains(targetId))
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            magicStoneCasterNoCenterCache.Add(targetId);
            return false;
        }

        bool hasBounds = false;
        Bounds merged = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                merged = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                merged.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            magicStoneCasterNoCenterCache.Add(targetId);
            return false;
        }

        center = merged.center;
        magicStoneCasterLocalCenterCache[targetId] = target.transform.InverseTransformPoint(center);
        magicStoneCasterNoCenterCache.Remove(targetId);
        return true;
    }

    private Vector3 ResolveMagicStoneBeamTarget(Vector3 start, Vector3 desiredTarget, float beamWidth)
    {
        EnsureMagicStoneBeamHitBufferCapacity();

        Vector3 delta = desiredTarget - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            return desiredTarget;
        }

        Vector3 direction = delta / distance;
        float sourcePadding = Mathf.Clamp(magicStoneBeamSourcePadding, 0f, distance);
        Vector3 castOrigin = start + direction * sourcePadding;
        float castDistance = distance - sourcePadding;
        if (castDistance <= 0.0001f)
        {
            return desiredTarget;
        }

        float radius = Mathf.Max(0.005f, beamWidth * 0.5f);
        QueryTriggerInteraction triggerMode = magicStoneBeamHitTriggers
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            radius,
            direction,
            magicStoneBeamHitBuffer,
            castDistance,
            magicStoneBeamObstacleMask,
            triggerMode
        );

        if (hitCount <= 0)
        {
            return desiredTarget;
        }

        float nearestDistance = float.MaxValue;
        bool hasValidHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = magicStoneBeamHitBuffer[i].collider;
            if (collider == null)
            {
                continue;
            }

            if (activeMagicStoneCasterVfx != null && collider.transform.IsChildOf(activeMagicStoneCasterVfx.transform))
            {
                continue;
            }

            float hitDistance = magicStoneBeamHitBuffer[i].distance;
            if (hitDistance < nearestDistance)
            {
                nearestDistance = hitDistance;
                hasValidHit = true;
            }
        }

        if (!hasValidHit)
        {
            return desiredTarget;
        }

        float pullback = Mathf.Max(0f, magicStoneBeamObstaclePullback);
        float clampedDistance = Mathf.Max(0f, nearestDistance - pullback);
        return castOrigin + direction * clampedDistance;
    }

    private void RefreshMagicStoneMobileRuntimeProfile()
    {
        useMagicStoneLiteMode = ShouldUseMagicStoneLiteMode();
        EnsureMagicStoneBeamHitBufferCapacity();
    }

    private bool ShouldUseMagicStoneLiteMode()
    {
        if (!enableMagicStoneLiteModeOnLowEnd)
        {
            return false;
        }

        int memoryMb = SystemInfo.systemMemorySize;
        if (memoryMb > 0 && memoryMb <= Mathf.Max(512, magicStoneLiteLowEndSystemMemoryMb))
        {
            return true;
        }

        return SystemInfo.processorCount <= Mathf.Max(1, magicStoneLiteLowEndProcessorCount);
    }

    private void EnsureMagicStoneBeamHitBufferCapacity()
    {
        int bufferSize = Mathf.Max(8, magicStoneBeamNonAllocHitBufferSize);
        if (magicStoneBeamHitBuffer != null && magicStoneBeamHitBuffer.Length == bufferSize)
        {
            return;
        }

        magicStoneBeamHitBuffer = new RaycastHit[bufferSize];
    }

    private void SpawnMagicStoneCasterVisual(Vector3 worldPosition)
    {
        if (activeMagicStoneCasterVfx != null)
        {
            ObjectPoolManager.ReturnObject(activeMagicStoneCasterVfx, ObjectPoolManager.PoolType.Particle);
            activeMagicStoneCasterVfx = null;
        }

        if (magicStoneCastDropPrefab == null)
        {
            return;
        }

        activeMagicStoneCasterVfx = ObjectPoolManager.SpawnObject(
            magicStoneCastDropPrefab,
            worldPosition,
            Quaternion.identity,
            ObjectPoolManager.PoolType.Particle
        );
    }

    private LineRenderer CreateMagicStoneBeamLine(Vector3 start, Vector3 end)
    {
        GameObject beamPrefab = GetMagicStoneBeamLinePrefab();
        if (beamPrefab == null)
        {
            return null;
        }

        GameObject lineObject = ObjectPoolManager.SpawnObject(
            beamPrefab,
            start,
            Quaternion.identity,
            ObjectPoolManager.PoolType.Particle
        );

        if (lineObject == null)
        {
            return null;
        }

        PrepareMagicStoneBeamObject(lineObject);

        lineObject.transform.SetParent(transform, true);

        LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = lineObject.AddComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;
        float width = Mathf.Max(0.005f, magicStoneBeamWidth);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        if (magicStoneBeamMaterial != null)
        {
            lineRenderer.sharedMaterial = magicStoneBeamMaterial;
        }

        lineRenderer.startColor = magicStoneBeamColor;
        lineRenderer.endColor = magicStoneBeamColor;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        // Hide beam until first reveal callback to avoid a stray line during intro drop.
        lineRenderer.enabled = false;
        activeMagicStoneBeamLines.Add(lineRenderer);
        return lineRenderer;
    }

    private static void PrepareMagicStoneBeamObject(GameObject lineObject)
    {
        if (lineObject == null)
        {
            return;
        }

        lineObject.SetActive(true);

        OnParticleDestroy[] autoReturners = lineObject.GetComponentsInChildren<OnParticleDestroy>(true);
        for (int i = 0; i < autoReturners.Length; i++)
        {
            if (autoReturners[i] != null)
            {
                autoReturners[i].enabled = false;
            }
        }
    }

    private GameObject GetMagicStoneBeamLinePrefab()
    {
        if (magicStoneBeamLinePrefab != null)
        {
            return magicStoneBeamLinePrefab;
        }

        if (magicStoneBeamRuntimeFallbackPrefab == null)
        {
            magicStoneBeamRuntimeFallbackPrefab = new GameObject("MagicStoneBeamLineFallbackPrefab");
            magicStoneBeamRuntimeFallbackPrefab.SetActive(false);
            magicStoneBeamRuntimeFallbackPrefab.AddComponent<LineRenderer>();
        }

        return magicStoneBeamRuntimeFallbackPrefab;
    }

    private void RevealMagicStoneBeam(LineRenderer lineRenderer, Vector3 start, Vector3 fromTarget, Vector3 target, float duration)
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (lineRenderer.gameObject != null && !lineRenderer.gameObject.activeSelf)
        {
            lineRenderer.gameObject.SetActive(true);
        }

        lineRenderer.enabled = true;
        if (lineRenderer.positionCount < 2)
        {
            lineRenderer.positionCount = 2;
        }

        DOTween.Kill(lineRenderer);

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, fromTarget);

        float safeDuration = Mathf.Max(0.01f, duration);
        DOVirtual.Float(0f, 1f, safeDuration, t =>
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, Vector3.LerpUnclamped(fromTarget, target, t));
        }).SetEase(Ease.OutSine).SetUpdate(true).SetTarget(lineRenderer);
    }

    private void FadeOutMagicStoneBeams(float duration)
    {
        if (activeMagicStoneBeamLines.Count <= 0)
        {
            return;
        }

        float safeDuration = Mathf.Max(0f, duration);
        if (safeDuration <= 0f)
        {
            CleanupMagicStoneBeamLinesOnly();
            return;
        }

        for (int i = 0; i < activeMagicStoneBeamLines.Count; i++)
        {
            LineRenderer lineRenderer = activeMagicStoneBeamLines[i];
            if (lineRenderer == null)
            {
                continue;
            }

            Color baseStart = lineRenderer.startColor;
            Color baseEnd = lineRenderer.endColor;
            DOVirtual.Float(1f, 0f, safeDuration, alpha =>
            {
                if (lineRenderer == null)
                {
                    return;
                }

                Color startColor = baseStart;
                Color endColor = baseEnd;
                startColor.a = baseStart.a * alpha;
                endColor.a = baseEnd.a * alpha;
                lineRenderer.startColor = startColor;
                lineRenderer.endColor = endColor;
            }).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(lineRenderer);
        }

        KillActiveMagicStoneBeamCleanupTween();
        activeMagicStoneBeamCleanupTween = DOVirtual.DelayedCall(safeDuration + 0.02f, CleanupMagicStoneBeamLinesOnly, true)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private void CleanupMagicStoneBeamLinesOnly()
    {
        KillActiveMagicStoneBeamCleanupTween();

        for (int i = 0; i < activeMagicStoneBeamLines.Count; i++)
        {
            LineRenderer lineRenderer = activeMagicStoneBeamLines[i];
            if (lineRenderer != null)
            {
                DOTween.Kill(lineRenderer);
                ObjectPoolManager.ReturnObject(lineRenderer.gameObject, ObjectPoolManager.PoolType.Particle);
            }
        }

        activeMagicStoneBeamLines.Clear();
    }

    private void KillActiveMagicStoneBeamCleanupTween()
    {
        if (activeMagicStoneBeamCleanupTween != null && activeMagicStoneBeamCleanupTween.IsActive())
        {
            activeMagicStoneBeamCleanupTween.Kill(false);
        }

        activeMagicStoneBeamCleanupTween = null;
    }

    private void CleanupMagicStoneVisualsImmediate()
    {
        CleanupMagicStoneBeamLinesOnly();

        if (activeMagicStoneCasterVfx != null)
        {
            ObjectPoolManager.ReturnObject(activeMagicStoneCasterVfx, ObjectPoolManager.PoolType.Particle);
            activeMagicStoneCasterVfx = null;
        }
    }

    private int BuildMagicStoneWaveRowsByIndex()
    {
        magicStoneWaveRowBuffer.Clear();

        if (mainRoute == null)
        {
            return 0;
        }

        if (mainRoute.FillActiveBlockRowSeeders(magicStoneMainRouteSeederBuffer) <= 0)
        {
            return 0;
        }

        for (int i = 0; i < magicStoneMainRouteSeederBuffer.Count; i++)
        {
            BlockRowSeedSpawner seeder = magicStoneMainRouteSeederBuffer[i];
            if (seeder == null)
            {
                continue;
            }

            int reuseIndex = magicStoneWaveRowBuffer.Count;
            MagicStoneWaveRowData rowData;
            if (reuseIndex < magicStoneWaveRowDataPool.Count)
            {
                rowData = magicStoneWaveRowDataPool[reuseIndex];
            }
            else
            {
                rowData = new MagicStoneWaveRowData();
                magicStoneWaveRowDataPool.Add(rowData);
            }

            rowData.Reset();
            rowData.seeder = seeder;
            rowData.color = seeder.GetCurrentColor();

            int maxSeeds = Mathf.Max(0, seeder.GetMaxSeedCount());
            for (int seedIndex = 0; seedIndex < maxSeeds; seedIndex++)
            {
                GameObject seed = seeder.GetSeed(seedIndex);
                if (seed == null)
                {
                    continue;
                }

                rowData.seeds.Add(seed);
            }

            rowData.seedCount = rowData.seeds.Count;
            if (rowData.seedCount <= 0)
            {
                continue;
            }

            magicStoneWaveRowBuffer.Add(rowData);
        }

        return magicStoneWaveRowBuffer.Count;
    }

    private void SpawnMagicStoneSeedExplodeVfx(Vector3 worldPosition)
    {
        if (magicStoneSeedExplodeVfxPrefab == null)
        {
            return;
        }

        GameObject vfx = ObjectPoolManager.SpawnObject(
            magicStoneSeedExplodeVfxPrefab,
            worldPosition,
            Quaternion.identity,
            ObjectPoolManager.PoolType.Particle
        );

        if (vfx == null)
        {
            return;
        }

        float lifetime = Mathf.Max(0.1f, magicStoneSeedExplodeVfxLifetime);
        DOVirtual.DelayedCall(lifetime, () =>
        {
            if (vfx != null)
            {
                ObjectPoolManager.ReturnObject(vfx, ObjectPoolManager.PoolType.Particle);
            }
        }, true);
    }

    private void SpawnMagicStoneRowCoinFromClearedRow(Vector3 rowWorldPosition)
    {
        InGameUIManager inGameUIManager = GetMagicStoneRewardUICached();
        if (inGameUIManager == null)
        {
            return;
        }

        Vector2 randomOffset = Random.insideUnitCircle * Mathf.Max(0f, magicStoneRowCoinSpawnSpread);
        Vector3 spawnPosition = rowWorldPosition + new Vector3(
            randomOffset.x,
            Mathf.Max(0f, magicStoneRowCoinSpawnYOffset),
            randomOffset.y
        );

        Camera rewardCamera = GetMagicStoneRewardCameraCached();
        int rewardValue = Mathf.Max(1, magicStoneRowCoinRewardValue);
        inGameUIManager.PlayGameplayCoinFlyFromWorld(spawnPosition, rewardValue, rewardCamera);
    }

    private InGameUIManager GetMagicStoneRewardUICached()
    {
        if (cachedMagicStoneRewardUI == null)
        {
            cachedMagicStoneRewardUI = GetInGameUIManagerCached();
        }

        return cachedMagicStoneRewardUI;
    }

    private Camera GetMagicStoneRewardCameraCached()
    {
        if (cachedMagicStoneRewardCamera == null)
        {
            cachedMagicStoneRewardCamera = mainCamera != null ? mainCamera : Camera.main;
        }

        return cachedMagicStoneRewardCamera;
    }

    private void ConsumeAmmoByMagicStoneClear(SeedColor targetColor, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
        {
            return;
        }

        if (!hasMagicStoneAmmoConsumerCache)
        {
            BuildMagicStoneAmmoConsumerCache();
        }

        remaining -= ConsumeAmmoFromCachedColorMap(magicStoneDeckShootersByColor, targetColor, remaining);
        if (remaining > 0)
        {
            remaining -= ConsumeAmmoFromCachedColorMap(magicStoneOtherShootersByColor, targetColor, remaining);
        }

        // Keep legacy scan as a safety net so runtime edge-cases still match prior behavior.
        if (remaining > 0)
        {
            ConsumeAmmoByMagicStoneClearFallback(targetColor, remaining);
        }
    }

    private void BuildMagicStoneAmmoConsumerCache()
    {
        ClearShooterColorMap(magicStoneDeckShootersByColor);
        ClearShooterColorMap(magicStoneOtherShootersByColor);
        magicStoneDeckShooterSet.Clear();

        if (slotBar != null)
        {
            List<BaseShooter> deckShooters = slotBar.GetAllShooters();
            for (int i = 0; i < deckShooters.Count; i++)
            {
                BaseShooter shooter = deckShooters[i];
                if (shooter == null)
                {
                    continue;
                }

                magicStoneDeckShooterSet.Add(shooter);
                AddShooterToColorMap(magicStoneDeckShootersByColor, shooter);
            }
        }

        magicStoneAllShooterBuffer.Clear();
        BaseShooter.FillRegisteredShooterBuffer(magicStoneAllShooterBuffer, false);

        for (int i = 0; i < magicStoneAllShooterBuffer.Count; i++)
        {
            BaseShooter shooter = magicStoneAllShooterBuffer[i];
            if (shooter == null || magicStoneDeckShooterSet.Contains(shooter))
            {
                continue;
            }

            AddShooterToColorMap(magicStoneOtherShootersByColor, shooter);
        }

        hasMagicStoneAmmoConsumerCache = true;
    }

    private void ClearMagicStoneAmmoConsumerCache()
    {
        ClearShooterColorMap(magicStoneDeckShootersByColor);
        ClearShooterColorMap(magicStoneOtherShootersByColor);
        magicStoneDeckShooterSet.Clear();
        hasMagicStoneAmmoConsumerCache = false;
    }

    private static void ClearShooterColorMap(Dictionary<SeedColor, List<BaseShooter>> colorMap)
    {
        if (colorMap == null || colorMap.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<SeedColor, List<BaseShooter>> pair in colorMap)
        {
            pair.Value?.Clear();
        }
    }

    private static void AddShooterToColorMap(Dictionary<SeedColor, List<BaseShooter>> colorMap, BaseShooter shooter)
    {
        if (colorMap == null || shooter == null)
        {
            return;
        }

        if (shooter.GetCurrentState() == ShooterState.Disappear)
        {
            return;
        }

        if (shooter.GetBulletCount() <= 0)
        {
            return;
        }

        SeedColor color = shooter.GetTargetColor();
        if (!colorMap.TryGetValue(color, out List<BaseShooter> shooters))
        {
            shooters = new List<BaseShooter>(8);
            colorMap[color] = shooters;
        }

        shooters.Add(shooter);
    }

    private static int ConsumeAmmoFromCachedColorMap(Dictionary<SeedColor, List<BaseShooter>> colorMap, SeedColor targetColor, int amount)
    {
        if (colorMap == null || !colorMap.TryGetValue(targetColor, out List<BaseShooter> shooters))
        {
            return 0;
        }

        return ConsumeAmmoFromCandidateShooterList(shooters, targetColor, amount);
    }

    private static int ConsumeAmmoFromCandidateShooterList(List<BaseShooter> shooters, SeedColor targetColor, int amount)
    {
        if (shooters == null || shooters.Count == 0)
        {
            return 0;
        }

        int remaining = Mathf.Max(0, amount);
        int consumedTotal = 0;

        for (int i = 0; i < shooters.Count && remaining > 0; i++)
        {
            BaseShooter shooter = shooters[i];
            if (!CanConsumeMagicStoneAmmoFromShooter(shooter, targetColor))
            {
                continue;
            }

            int consumed = shooter.ConsumeAmmoExternally(remaining);
            if (consumed <= 0)
            {
                continue;
            }

            consumedTotal += consumed;
            remaining -= consumed;
        }

        return consumedTotal;
    }

    private void ConsumeAmmoByMagicStoneClearFallback(SeedColor targetColor, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
        {
            return;
        }

        magicStoneDeckShooterBuffer.Clear();
        magicStoneDeckShooterSet.Clear();

        if (slotBar != null)
        {
            List<BaseShooter> deckShooters = slotBar.GetAllShooters();
            for (int i = 0; i < deckShooters.Count; i++)
            {
                BaseShooter shooter = deckShooters[i];
                if (!CanConsumeMagicStoneAmmoFromShooter(shooter, targetColor))
                {
                    continue;
                }

                magicStoneDeckShooterBuffer.Add(shooter);
                magicStoneDeckShooterSet.Add(shooter);
            }

            remaining -= ConsumeAmmoFromShooterList(magicStoneDeckShooterBuffer, remaining);
        }

        if (remaining <= 0)
        {
            return;
        }

        magicStoneAllShooterBuffer.Clear();
        BaseShooter.FillRegisteredShooterBuffer(magicStoneAllShooterBuffer, false);

        for (int i = 0; i < magicStoneAllShooterBuffer.Count && remaining > 0; i++)
        {
            BaseShooter shooter = magicStoneAllShooterBuffer[i];
            if (shooter == null || magicStoneDeckShooterSet.Contains(shooter))
            {
                continue;
            }

            if (!CanConsumeMagicStoneAmmoFromShooter(shooter, targetColor))
            {
                continue;
            }

            remaining -= shooter.ConsumeAmmoExternally(remaining);
        }
    }

    private static int ConsumeAmmoFromShooterList(List<BaseShooter> shooters, int amount)
    {
        if (shooters == null || shooters.Count == 0)
        {
            return 0;
        }

        int remaining = Mathf.Max(0, amount);
        int consumedTotal = 0;

        for (int i = 0; i < shooters.Count && remaining > 0; i++)
        {
            BaseShooter shooter = shooters[i];
            if (shooter == null)
            {
                continue;
            }

            int consumed = shooter.ConsumeAmmoExternally(remaining);
            if (consumed <= 0)
            {
                continue;
            }

            consumedTotal += consumed;
            remaining -= consumed;
        }

        return consumedTotal;
    }

    private static bool CanConsumeMagicStoneAmmoFromShooter(BaseShooter shooter, SeedColor targetColor)
    {
        if (shooter == null)
        {
            return false;
        }

        if (shooter.GetCurrentState() == ShooterState.Disappear)
        {
            return false;
        }

        if (shooter.GetTargetColor() != targetColor)
        {
            return false;
        }

        return shooter.GetBulletCount() > 0;
    }

    private IEnumerator PlayIntroNextFrame(LevelElementAnimator animator, int level)
    {
        try
        {
            yield return null;

            if (ShouldAbortIntroCoroutine(animator))
            {
                SetInputLocked(false);
                yield break;
            }

            InGameUIManager inGameUIManager = GetInGameUIManagerCached();
            if (inGameUIManager != null)
            {
                float extraDelay = Mathf.Max(0f, inGameUIManager.GetLevelElementAnimatorDelayFromHUDStart());
                if (extraDelay > 0f)
                {
                    yield return new WaitForSeconds(extraDelay);

                    if (ShouldAbortIntroCoroutine(animator))
                    {
                        SetInputLocked(false);
                        yield break;
                    }
                }
            }

            animator.PlayIntroAnimation();
            float introDuration = Mathf.Max(0f, animator.GetIntroDuration());
            if (introDuration > 0f)
            {
                yield return new WaitForSeconds(introDuration);

                if (ShouldAbortIntroCoroutine(animator))
                {
                    SetInputLocked(false);
                    yield break;
                }
            }

            animator.ApplyPostIntroRenderOptimization();

            inGameUIManager?.PlayHardLevelAfterLevelIntro();

            SetInputLocked(false);
            TutorialManager.Instance?.CheckAndStartTutorial(level);
        }
        finally
        {
            pendingIntroRoutine = null;
        }
    }

    private bool ShouldAbortIntroCoroutine(LevelElementAnimator animator)
    {
        if (gameEnded || animator == null)
        {
            return true;
        }

        if (levelGO == null)
        {
            return true;
        }

        if (!animator.gameObject.scene.IsValid())
        {
            return true;
        }

        return false;
    }

    private InGameUIManager GetInGameUIManagerCached()
    {
        if (cachedInGameUIManager == null)
        {
            cachedInGameUIManager = InGameUIManager.Instance;
        }

        return cachedInGameUIManager;
    }

    private void SetInputLocked(bool shouldLock)
    {
        InputManager inputManager = cachedInputManager;
        if (inputManager == null)
        {
            inputManager = InputManager.Instance;
            cachedInputManager = inputManager;
        }

        if (inputManager == null)
        {
            return;
        }

        inputManager.SetInputActive(!shouldLock);
    }
    // Booster condition gatekeeper
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Kiá»ƒm tra lose khi refill hoÃ n táº¥t (gá»i tá»« SplineController)
    /// </summary>
    public void CheckLoseAfterRefill()
    {
        if (!gameEnded)
        {
            CancelPendingLoseTrigger();
            CallLoseCheckDelayed();
        }
    }

    private void CancelPendingLoseTrigger()
    {
        if (pendingLoseTriggerRoutine != null)
        {
            StopCoroutine(pendingLoseTriggerRoutine);
            pendingLoseTriggerRoutine = null;
        }
    }

    public void ContinueAfterKeepPlaying()
    {
        if (!gameEnded)
        {
            return;
        }

        gameEnded = false;
        mainRoute?.SetTutorialPaused(false);

        if (pendingLoseRoutine != null)
        {
            StopCoroutine(pendingLoseRoutine);
            pendingLoseRoutine = null;
        }

        CancelPendingLoseTrigger();
        SetInputLocked(false);
        CallLoseCheckDelayed();
    }

    /// <summary>
    /// Kiá»ƒm tra Ä‘iá»u kiá»‡n sá»­ dá»¥ng booster â€” InGameUIManager há»i hÃ m nÃ y Ä‘á»ƒ update button UI.
    /// Thá»±c táº¿ delegate sang BoosterManager â†’ strategy.CanUse().
    /// </summary>
    public bool CanUseBooster(string boosterId)
    {
        if (isAutoFinishRunning || gameEnded) return false;
        if (BoosterManager.Instance == null) return false;
        return BoosterManager.Instance.GetCanUse(boosterId);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GUARANTEED WIN / AUTO-FINISH
    // ─────────────────────────────────────────────────────────────────────────────

    public bool IsAutoFinishRunning()
    {
        return isAutoFinishRunning;
    }

    /// <summary>
    /// Kiểm tra xem có đủ điều kiện Guaranteed Win không:
    /// Số slot trống trong hàng chờ >= tổng số shooter còn lại bên dưới (grid + tunnel).
    /// </summary>
    public bool IsGuaranteedWinPossible()
    {
        if (gameEnded || isAutoFinishRunning || isMagicStoneClearInProgress)
        {
            return false;
        }

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            return false;
        }

        if (slotBar == null)
        {
            return false;
        }

        int totalSlotCount = slotBar.GetTotalSlotCount();
        int initialTotalShooters = totalShooters > 0 ? totalShooters : (destroyedShooters + GetRemainingShooterCountBelow());
        if (initialTotalShooters <= totalSlotCount)
        {
            return false;
        }

        int emptySlotCount = slotBar.GetEmptySlotCount();
        if (emptySlotCount <= 0)
        {
            return false;
        }

        int remainingBelow = GetRemainingShooterCountBelow();
        if (remainingBelow <= 0)
        {
            return false;
        }

        return emptySlotCount >= remainingBelow;
    }

    /// <summary>
    /// Lấy tổng số shooter còn lại bên dưới (trên grid hoặc trong tunnel, chưa vào SlotBar).
    /// </summary>
    public int GetRemainingShooterCountBelow()
    {
        if (slotBar == null)
        {
            return 0;
        }

        int occupiedInSlotBar = slotBar.GetShooterCount();
        int totalRemaining = GetRemainingShooterCountIncludingInactive();
        return Mathf.Max(0, totalRemaining - occupiedInSlotBar);
    }

    /// <summary>
    /// Thử kích hoạt cơ chế Guaranteed Win nếu điều kiện thỏa mãn.
    /// </summary>
    public bool TryTriggerGuaranteedWin()
    {
        if (!IsGuaranteedWinPossible())
        {
            return false;
        }

        StartAutoFinish();
        return true;
    }

    public void StartAutoFinish()
    {
        if (isAutoFinishRunning || gameEnded)
        {
            return;
        }

        if (pendingAutoFinishRoutine != null)
        {
            StopCoroutine(pendingAutoFinishRoutine);
            pendingAutoFinishRoutine = null;
        }

        isAutoFinishRunning = true;
        SetInputLocked(true);
        pendingAutoFinishRoutine = StartCoroutine(PerformAutoFinishRoutine());
    }

    private IEnumerator PerformAutoFinishRoutine()
    {
        yield return new WaitForSeconds(0.1f);

        float timeoutTime = Time.time + 30f;

        while (!gameEnded && isAutoFinishRunning && GetRemainingShooterCountBelow() > 0 && Time.time < timeoutTime)
        {
            if (slotBar == null || slotBar.IsFull())
            {
                break;
            }

            BaseShooter pickableShooter = FindNextPickableShooterForAutoFinish();
            if (pickableShooter != null)
            {
                TriggerAutoPickShooter(pickableShooter);
                yield return new WaitForSeconds(autoFinishInterval);
            }
            else
            {
                yield return new WaitForSeconds(0.08f);
            }
        }

        pendingAutoFinishRoutine = null;
        isAutoFinishRunning = false;
    }

    private BaseShooter FindNextPickableShooterForAutoFinish()
    {
        BaseShooter.FillRegisteredShooterBuffer(autoFinishShooterBuffer, true);

        // Ưu tiên shooter có màu trùng với màu seed đang có ở main route để bắn ngay
        if (mainRoute != null)
        {
            List<GameObject> activeRows = mainRoute.GetActiveBlockRows();
            if (activeRows != null && activeRows.Count > 0)
            {
                for (int r = 0; r < activeRows.Count; r++)
                {
                    GameObject rowGO = activeRows[r];
                    if (rowGO == null) continue;
                    BlockRowSeedSpawner seeder = rowGO.GetComponent<BlockRowSeedSpawner>();
                    if (seeder == null || seeder.GetSeedCount() <= 0) continue;

                    SeedColor rowColor = seeder.GetCurrentColor();
                    for (int i = 0; i < autoFinishShooterBuffer.Count; i++)
                    {
                        BaseShooter shooter = autoFinishShooterBuffer[i];
                        if (shooter != null && shooter.isActiveAndEnabled && shooter.GetCurrentState() == ShooterState.IdleGrid)
                        {
                            if (shooter.GetTargetColor() == rowColor)
                            {
                                return shooter;
                            }
                        }
                    }
                }
            }
        }

        // Nếu không có shooter cùng màu hoặc không xác định được, lấy shooter IdleGrid đầu tiên
        for (int i = 0; i < autoFinishShooterBuffer.Count; i++)
        {
            BaseShooter shooter = autoFinishShooterBuffer[i];
            if (shooter != null && shooter.isActiveAndEnabled && shooter.GetCurrentState() == ShooterState.IdleGrid)
            {
                return shooter;
            }
        }

        return null;
    }

    private void TriggerAutoPickShooter(BaseShooter shooter)
    {
        if (shooter == null || slotBar == null)
        {
            return;
        }

        if (!slotBar.AddShooter(shooter))
        {
            return;
        }

        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        if (IsLastPickableShooterOnGrid(shooter) &&
            SpeedMultiplierManager.Instance != null &&
            !SpeedMultiplierManager.IsSpeedUpActive())
        {
            SpeedMultiplierManager.Instance.ToggleSpeedUp();
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
        }

        GameEventHub.Instance.Invoke(GameEventType.OnShooterJumpStart, shooter);
        GameEventHub.Instance.Invoke(GameEventType.OnShooterSelected, shooter);
        GameEventHub.Instance.Invoke(GameEventType.OnShooterAddedToSlot, null);
    }

    private bool IsLastPickableShooterOnGrid(BaseShooter selectedShooter)
    {
        if (selectedShooter == null)
        {
            return false;
        }

        BaseShooter.FillRegisteredShooterBuffer(autoFinishShooterBuffer, true);
        for (int i = 0; i < autoFinishShooterBuffer.Count; i++)
        {
            BaseShooter shooter = autoFinishShooterBuffer[i];
            if (shooter == null || shooter == selectedShooter)
            {
                continue;
            }

            ShooterState state = shooter.GetCurrentState();
            if (state == ShooterState.IdleGrid ||
                state == ShooterState.Lock ||
                state == ShooterState.Frozen)
            {
                return false;
            }
        }

        return true;
    }

    public void DebugForceWin()
    {
        if (gameEnded)
        {
            return;
        }

        TriggerWin();
    }

    public void DebugForceLose()
    {
        if (gameEnded)
        {
            return;
        }

        TriggerLose();
    }
}


