using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Manager quản lý tất cả boosters trong game.
/// Singleton — tồn tại suốt màn chơi.
/// 
/// Flow tổng quát:
///   1. InGameUIManager.OnUseBooster(id) → BoosterManager.TryActivate(strategy)
///   2. BoosterManager.TryActivate → kiểm tra CanUse, rồi Execute
///   3. Với "Pick Locked Shooter": chuyển sang mode PickLockedShooter
///      → InputManager phản ứng → chỉ cho chọn shooter Lock
///   4. Khi done → Deactivate → fire OnBoosterDeactivated
/// </summary>
public class BoosterManager : MonoBehaviour
{
    public static BoosterManager Instance { get; private set; }
    private const string BoosterCountKeyPrefix = "BoosterCount_";
    private const string BoosterInitGrantedKeyPrefix = "BoosterInitGranted_";
    private const string BoosterInitMigrationVersionKey = "BoosterInitMigrationVersion";
    private const int BoosterInitMigrationVersion = 1;

    // ─── Registered configs (gán trong Inspector) ─────────────────────
    [Header("Booster Configs")]
    [Tooltip("Kéo thả tất cả BoosterStrategyConfig assets vào đây")]
    [SerializeField] private List<BoosterStrategyConfig> boosterConfigs = new List<BoosterStrategyConfig>();

    // ─── Runtime state ─────────────────────────────────────────────────
    public enum ActiveBoosterMode { None, PickLockedShooter, HeroShooter }

    [Header("Runtime (read-only)")]
    [SerializeField] private ActiveBoosterMode currentMode = ActiveBoosterMode.None;

    private IBoosterStrategy activeStrategy;
    private System.Action    pendingOnComplete;

    // Lưu config của booster đang ở mode PickLockedShooter
    private PickLockedShooterBoosterConfig pickLockedCfg;

    // Lưu config của booster đang ở mode HeroShooter
    private HeroShooterBoosterConfig heroShooterCfg;

    // Config đang active (dùng để InGameUIManager đọc tên/icon cho instruction panel)
    public BoosterStrategyConfig ActiveConfig { get; private set; }

    // SlotBar reference để update glow VFX cho hero booster selection
    private SlotBar slotBar;

    // Strategy cache — build once in Awake để không CreateStrategy mỗi lần hỏi CanUse
    private Dictionary<string, IBoosterStrategy> strategyCache = new Dictionary<string, IBoosterStrategy>();
    private readonly Dictionary<string, int> boosterCounts = new Dictionary<string, int>();
    private readonly List<BaseShooter> shooterBuffer = new List<BaseShooter>(128);

    // ──────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildCache();
    }

    private void OnEnable()
    {
        if (GameEventHub.Instance == null) return;
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarInit, OnSlotBarInit);
    }

    private void OnDisable()
    {
        if (GameEventHub.Instance == null) return;
        GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarInit, OnSlotBarInit);
    }

    private void OnSlotBarInit(object data)
    {
        if (data is SlotBar slotBar)
            InjectSlotBar(slotBar);
    }

    private void InjectSlotBar(SlotBar slotBarRef)
    {
        slotBar = slotBarRef;
        foreach (var strategy in strategyCache.Values)
            strategy.SetSlotBar(slotBarRef);
    }

    private void OnDestroy()
    {
        SetMainRoutePausedForBooster(false);
        if (Instance == this) Instance = null;
    }

    private void SetMainRoutePausedForBooster(bool paused)
    {
        SplineController splineController = SplineController.Instance;
        if (splineController == null)
        {
            return;
        }

        SplineRoute mainRoute = splineController.GetMainRoute();
        if (mainRoute == null)
        {
            return;
        }

        mainRoute.SetBoosterFocusPaused(paused);
    }

    private void BuildCache()
    {
        strategyCache.Clear();
        boosterCounts.Clear();

        foreach (var cfg in boosterConfigs)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.boosterName)) continue;
            strategyCache[cfg.boosterName] = cfg.CreateStrategy();
            boosterCounts[cfg.boosterName] = LoadBoosterCount(cfg.boosterName);
        }

        SyncUnlockedBoosterInitialCount();
    }

    // ──────────────────────────────────────────────────────────────────
    // Query API — dùng bởi GamePlayController / InGameUIManager
    // ──────────────────────────────────────────────────────────────────

    public IBoosterStrategy GetStrategy(string boosterId)
        => strategyCache.TryGetValue(boosterId, out var s) ? s : null;

    public bool GetCanUse(string boosterId)
        => GetStrategy(boosterId)?.CanUse() ?? false;

    public int GetBoosterCount(string boosterId)
    {
        if (string.IsNullOrEmpty(boosterId)) return 0;
        return boosterCounts.TryGetValue(boosterId, out int count) ? Mathf.Max(0, count) : 0;
    }

    public bool HasBooster(string boosterId)
        => GetBoosterCount(boosterId) > 0;

    public bool TryConsumeBooster(string boosterId, int amount = 1)
    {
        if (string.IsNullOrEmpty(boosterId)) return false;
        int consumeAmount = Mathf.Max(1, amount);
        int current = GetBoosterCount(boosterId);
        if (current < consumeAmount) return false;

        int nextCount = current - consumeAmount;
        boosterCounts[boosterId] = nextCount;
        SaveBoosterCount(boosterId, nextCount);
        return true;
    }

    public bool AddBooster(string boosterId, int amount = 1)
    {
        if (string.IsNullOrEmpty(boosterId)) return false;

        int addAmount = Mathf.Max(1, amount);
        int current = GetBoosterCount(boosterId);
        int nextCount = current + addAmount;
        boosterCounts[boosterId] = nextCount;
        SaveBoosterCount(boosterId, nextCount);

        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, null);
        }

        return true;
    }

    public List<BoosterStrategyConfig> GetAllConfigs() => boosterConfigs;

    public BoosterStrategyConfig GetBoosterConfig(string boosterId)
    {
        if (string.IsNullOrEmpty(boosterId)) return null;
        for (int i = 0; i < boosterConfigs.Count; i++)
        {
            var cfg = boosterConfigs[i];
            if (cfg == null || string.IsNullOrEmpty(cfg.boosterName)) continue;
            if (cfg.boosterName == boosterId ||
               ((boosterId == Const.BOOSTER_UNLOCKSHOOTER || boosterId == "Spinner" || boosterId == "PickLockedShooter") &&
                (cfg.boosterName == Const.BOOSTER_UNLOCKSHOOTER || cfg.boosterName == "Spinner" || cfg.boosterName == "PickLockedShooter")))
            {
                return cfg;
            }
        }
        return null;
    }

    public int GetPurchaseAmount(string boosterId)
    {
        var cfg = GetBoosterConfig(boosterId);
        if (cfg != null && cfg.purchaseAmount > 0)
        {
            return cfg.purchaseAmount;
        }
        return 3;
    }

    public void SyncUnlockedBoosterInitialCount()
    {
        bool hasChanges = false;
        bool applyLegacyZeroCountMigration = PlayerPrefs.GetInt(BoosterInitMigrationVersionKey, 0) < BoosterInitMigrationVersion;

        for (int i = 0; i < boosterConfigs.Count; i++)
        {
            BoosterStrategyConfig cfg = boosterConfigs[i];
            if (cfg == null || string.IsNullOrEmpty(cfg.boosterName))
            {
                continue;
            }

            string boosterId = cfg.boosterName;
            int currentCount = GetBoosterCount(boosterId);
            bool isUnlocked = BoosterUnlockPrefs.IsBoosterUnlocked(boosterId);
            string initKey = GetBoosterInitGrantedKey(boosterId);
            bool isInitGranted = PlayerPrefs.GetInt(initKey, 0) == 1;

            if (isUnlocked && !isInitGranted)
            {
                currentCount = Mathf.Max(currentCount, Mathf.Max(0, cfg.initialCount));
                PlayerPrefs.SetInt(initKey, 1);
                hasChanges = true;
            }

            // Migration 1 lan de sua du lieu cu bi danh dau init nhung count bi luu sai = 0.
            if (applyLegacyZeroCountMigration && isUnlocked && isInitGranted && currentCount <= 0 && cfg.initialCount > 0)
            {
                currentCount = cfg.initialCount;
                hasChanges = true;
            }

            boosterCounts[boosterId] = currentCount;

            string countKey = GetBoosterCountKey(boosterId);
            if (!PlayerPrefs.HasKey(countKey) || PlayerPrefs.GetInt(countKey, -1) != currentCount)
            {
                PlayerPrefs.SetInt(countKey, currentCount);
                hasChanges = true;
            }
        }

        if (applyLegacyZeroCountMigration)
        {
            PlayerPrefs.SetInt(BoosterInitMigrationVersionKey, BoosterInitMigrationVersion);
            hasChanges = true;
        }

        if (hasChanges)
        {
            PlayerPrefs.Save();
            if (GameEventHub.Instance != null)
            {
                GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, null);
            }
        }
    }

    private static string GetBoosterCountKey(string boosterId)
    {
        return string.Concat(BoosterCountKeyPrefix, boosterId);
    }

    private static string GetBoosterInitGrantedKey(string boosterId)
    {
        return string.Concat(BoosterInitGrantedKeyPrefix, boosterId);
    }

    private static int LoadBoosterCount(string boosterId)
    {
        if (string.IsNullOrEmpty(boosterId))
        {
            return 0;
        }

        string countKey = GetBoosterCountKey(boosterId);
        if (!PlayerPrefs.HasKey(countKey))
        {
            return 0;
        }

        return Mathf.Max(0, PlayerPrefs.GetInt(countKey, 0));
    }

    private static void SaveBoosterCount(string boosterId, int count)
    {
        if (string.IsNullOrEmpty(boosterId))
        {
            return;
        }

        PlayerPrefs.SetInt(GetBoosterCountKey(boosterId), Mathf.Max(0, count));
        PlayerPrefs.Save();
    }

    // ──────────────────────────────────────────────────────────────────
    // Public API — gọi từ InGameUIManager
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thử kích hoạt booster.  
    /// - Nếu booster này đang active (2-step) → Cancel (toggle off).
    /// - Nếu mode khác đang active → Reset mode cũ trước.
    /// - Ngược lại → Execute nếu CanUse().
    /// </summary>
    public void TryActivate(IBoosterStrategy strategy)
    {
        if (strategy == null) return;

        // Toggle off nếu đang là mode PickLockedShooter của chính booster này
        if (currentMode == ActiveBoosterMode.PickLockedShooter
            && activeStrategy?.BoosterName == strategy.BoosterName)
        {
            CancelPickLockedShooterMode();
            return;
        }

        // Toggle off nếu đang là mode HeroShooter của chính booster này
        if (currentMode == ActiveBoosterMode.HeroShooter
            && activeStrategy?.BoosterName == strategy.BoosterName)
        {
            CancelHeroShooterMode();
            return;
        }

        // Reset mode hiện tại nếu khác booster được activate trước
        if (currentMode != ActiveBoosterMode.None)
        {
            ResetCurrentMode();
        }

        if (!strategy.CanUse()) return;

        activeStrategy = strategy;
        strategy.Execute(OnStrategyComplete);
    }

    /// <summary>
    /// Reset mode hiện tại: clear VFX, unsubscribe events, return về trạng thái None.
    /// Gọi khi switch từ mode này sang mode khác hoặc quay về normal state.
    /// </summary>
    private void ResetCurrentMode()
    {
        if (currentMode == ActiveBoosterMode.PickLockedShooter)
        {
            CancelPickLockedShooterMode();
        }
        else if (currentMode == ActiveBoosterMode.HeroShooter)
        {
            CancelHeroShooterMode();
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // PickLockedShooter mode
    // ──────────────────────────────────────────────────────────────────

    public void EnterPickLockedShooterMode(PickLockedShooterBoosterConfig cfg,
                                           System.Action onComplete)
    {
        pickLockedCfg     = cfg;
        ActiveConfig      = cfg;
        pendingOnComplete = onComplete;
        currentMode       = ActiveBoosterMode.PickLockedShooter;
        SetMainRoutePausedForBooster(true);

        GameEventHub.Instance.Invoke(GameEventType.OnBoosterActivated, currentMode);
        TutorialManager.Instance?.SetDimOverlayActiveForBooster(true);
        NotifyAllLockedShooters(highlight: true);
    }

    public void OnLockedShooterPicked(BaseShooter shooter)
    {
        if (currentMode != ActiveBoosterMode.PickLockedShooter) return;

        currentMode = ActiveBoosterMode.None;
        NotifyAllLockedShooters(highlight: false, except: shooter);

        if (!TryConsumeBooster(pickLockedCfg.boosterName, 1))
        {
            CancelPickLockedShooterMode();
            return;
        }

        SlotBar slotBar = this.slotBar != null ? this.slotBar : SlotBar.Instance;
        if (slotBar != null && slotBar.AddShooter(shooter))
        {
            AudioManager.Instance?.PlaySFX(Const.popUISFX);
            GameEventHub.Instance.Invoke(GameEventType.OnShooterJumpStart, shooter);
            GameEventHub.Instance.Invoke(GameEventType.OnShooterSelected,  shooter);
            GameEventHub.Instance.Invoke(GameEventType.OnShooterAddedToSlot, null);
        }

        ExitPickLockedShooterMode(completed: true);
    }

    public void CancelPickLockedShooterMode()
    {
        if (currentMode != ActiveBoosterMode.PickLockedShooter) return;
        currentMode = ActiveBoosterMode.None;
        NotifyAllLockedShooters(highlight: false);
        ExitPickLockedShooterMode(completed: false);
    }

    private void ExitPickLockedShooterMode(bool completed)
    {
        currentMode    = ActiveBoosterMode.None;
        activeStrategy = null;
        pickLockedCfg  = null;
        ActiveConfig   = null;
        SetMainRoutePausedForBooster(false);

        TutorialManager.Instance?.SetDimOverlayActiveForBooster(false);
        GameEventHub.Instance.Invoke(GameEventType.OnBoosterDeactivated, null);
        GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, null);

        if (completed) pendingOnComplete?.Invoke();
        pendingOnComplete = null;
    }

    private void OnStrategyComplete()
    {
        activeStrategy = null;
        currentMode    = ActiveBoosterMode.None;
        ActiveConfig   = null;
        SetMainRoutePausedForBooster(false);
        GameEventHub.Instance.Invoke(GameEventType.OnBoosterDeactivated, null);
        GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, null);
    }

    // ──────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────

    private void NotifyAllLockedShooters(bool highlight, BaseShooter except = null)
    {
        TutorialManager tutMgr = TutorialManager.Instance;

        BaseShooter.FillRegisteredShooterBuffer(shooterBuffer, true);
        for (int i = 0; i < shooterBuffer.Count; i++)
        {
            BaseShooter s = shooterBuffer[i];
            if (s == except) continue;
            if (s.GetCurrentState() == ShooterState.Lock)
            {
                if (highlight)
                {
                    s.PlayBoosterHighlightAnimation();
                    tutMgr?.HighlightGameObjectForBooster(s.gameObject);
                }
                else
                {
                    s.StopBoosterHighlightAnimation();
                }
                s.RefreshBlockedStateScale();
            }
        }

        if (!highlight)
        {
            tutMgr?.RestoreBoosterHighlights();
        }
    }

    public bool IsPickLockedShooterModeActive()
        => currentMode == ActiveBoosterMode.PickLockedShooter;

    /// <summary>
    /// Trả về true nếu mode 2-step đang active CHO booster có tên này.
    /// Dùng bởi BoosterButtonPrefab để bật/tắt activeHighlight.
    /// </summary>
    public bool IsBoosterModeActive(string boosterName)
        => currentMode != ActiveBoosterMode.None
           && ActiveConfig?.boosterName == boosterName;

    public bool IsHeroShooterModeActive()
        => currentMode == ActiveBoosterMode.HeroShooter;

    // ──────────────────────────────────────────────────────────────────
    // HeroShooter mode
    // ──────────────────────────────────────────────────────────────────

    public void EnterHeroShooterMode(HeroShooterBoosterConfig cfg, System.Action onComplete)
    {
        heroShooterCfg    = cfg;
        ActiveConfig      = cfg;
        pendingOnComplete = onComplete;
        currentMode       = ActiveBoosterMode.HeroShooter;
        SetMainRoutePausedForBooster(true);

        GameEventHub.Instance.Invoke(GameEventType.OnBoosterActivated, currentMode);

        UpdateHeroShooterSelectionUI(highlight:true);
    }

    /// <summary>
    /// Update hero shooter selection UI - show glow on slots with selectable shooters (Idle state).
    /// Only shooters in Idle state can be selected for hero mode.
    /// </summary>
    private void UpdateHeroShooterSelectionUI(bool highlight)
    {
        if (slotBar != null)
            slotBar.ToggleHeroShooterSelectionGlow(highlight);
    }

    public void OnHeroShooterPicked(BaseShooter shooter)
    {
        if (currentMode != ActiveBoosterMode.HeroShooter) return;

        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        if (!TryConsumeBooster(heroShooterCfg.boosterName, 1))
        {
            CancelHeroShooterMode();
            return;
        }

        List<BlockRowSeedSpawner> targets = CollectHeroTargets(shooter);

        HeroShooterBoosterConfig cfg = heroShooterCfg;

        // Exit mode first — input returns to normal immediately
        ExitHeroShooterMode(completed: false);

        // Kick off the hero sequence (camera, fly, shoot, return)
        shooter.StartHeroSequence(cfg, targets);
    }

    public void CancelHeroShooterMode()
    {
        if (currentMode != ActiveBoosterMode.HeroShooter) return;
        UpdateHeroShooterSelectionUI(highlight: false);
        ExitHeroShooterMode(completed: false);
    }

    private void ExitHeroShooterMode(bool completed)
    {
        currentMode       = ActiveBoosterMode.None;
        activeStrategy    = null;
        heroShooterCfg    = null;
        ActiveConfig      = null;
        SetMainRoutePausedForBooster(false);

        // Clear glow VFX from all potential targets
        UpdateHeroShooterSelectionUI(highlight: false);

        GameEventHub.Instance.Invoke(GameEventType.OnBoosterDeactivated, null);
        GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, null);

        if (completed) pendingOnComplete?.Invoke();
        pendingOnComplete = null;
    }

    /// <summary>
    /// Gom target HeroShooter theo thứ tự index của route.
    /// Main route ưu tiên index giảm dần, side route ưu tiên index tăng dần.
    /// </summary>
    private static List<BlockRowSeedSpawner> CollectHeroTargets(BaseShooter hero)
    {
        SplineController sc = SplineController.Instance;
        if (sc == null) return new List<BlockRowSeedSpawner>();

        SeedColor heroColor = hero.GetTargetColor();
        int bulletCount  = hero.GetBulletCount();
        int decreaseAmt  = Mathf.Max(1, hero.GetBulletDecreaseAmount());
        int maxTargets   = bulletCount / decreaseAmt;

        var result = new List<BlockRowSeedSpawner>();

        CollectFromRoute(sc.GetMainRoute(), result, maxTargets, heroColor);

        if (result.Count < maxTargets)
        {
            foreach (SplineRoute side in sc.GetSideRoutes())
            {
                if (result.Count >= maxTargets) break;
                CollectFromRoute(side, result, maxTargets, heroColor);
            }
        }

        return result;
    }

    private static void CollectFromRoute(SplineRoute route,
                                         List<BlockRowSeedSpawner> result, int maxTargets,
                                         SeedColor heroColor)
    {
        if (route == null || result.Count >= maxTargets) return;

        List<GameObject> rows = route.GetActiveBlockRows();

        bool isMainRoute = route.GetRouteMode() == SplineRoute.RouteMode.Main;
        int start = isMainRoute ? rows.Count - 1 : 0;
        int endExclusive = isMainRoute ? -1 : rows.Count;
        int step = isMainRoute ? -1 : 1;

        for (int i = start; i != endExclusive; i += step)
        {
            if (result.Count >= maxTargets) break;
            GameObject rowGO = rows[i];
            if (rowGO == null) continue;

            var seeder = rowGO.GetComponent<BlockRowSeedSpawner>();
            if (seeder == null || seeder.GetSeedCount() <= 0) continue;
            if (seeder.GetCurrentColor() != heroColor) continue;

            result.Add(seeder);
        }
    }
}

