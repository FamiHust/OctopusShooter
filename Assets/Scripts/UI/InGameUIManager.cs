using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.Serialization;
using UnityEngine.Profiling;

/// <summary>
/// Quáº£n lÃ½ toÃ n bá»™ UI trong mÃ n chÆ¡i.
/// 
/// Flow khá»Ÿi táº¡o (gá»i tá»« luá»“ng init level):
///   Init(GamePlayController) â†’ check unlock â†’ áº©n/hiá»‡n booster panel â†’ init buttons
/// 
/// Flow booster:
///   BoosterButtonPrefab.OnClick â†’ InGameUIManager.OnUseBooster(id)
///   â†’ BoosterManager.TryActivate â†’ (1-step: execute; 2-step: enter mode)
///   â†’ Náº¿u 2-step: hiá»‡n instruction panel; nháº¥n button láº¡i â†’ cancel
/// </summary>
public class InGameUIManager : MonoBehaviour
{
    public static InGameUIManager Instance { get; private set; }

    // â”€â”€â”€ Booster UI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [Header("Booster")]
    [Tooltip("Táº¥t cáº£ BoosterButtonPrefab trong scene, theo thá»© tá»±")]
    [SerializeField] private List<BoosterButtonPrefab> boosterButtons = new List<BoosterButtonPrefab>();

    [Tooltip("Panel chá»©a cÃ¡c booster button â€” áº©n Ä‘i náº¿u chÆ°a unlock booster nÃ o")]
    [SerializeField] private GameObject boosterPanel;

    [Tooltip("Hiá»ƒn thá»‹ bottom UI tá»« level nÃ y trá»Ÿ lÃªn, ká»ƒ cáº£ khi chÆ°a unlock booster nÃ o")]
    [Min(1)]
    [SerializeField] private int showBottomUIAtLevel = 1;

    [Header("Top UI")]
    [Tooltip("Panel top UI (coin/level/setting)")]
    [SerializeField] private GameObject topUIPanel;

    [Tooltip("Hiá»ƒn thá»‹ top UI tá»« level nÃ y trá»Ÿ lÃªn")]
    [Min(1)]
    [SerializeField] private int showTopUIAtLevel = 1;

    [Header("Hard Level UI")]
    [SerializeField] private GameObject hardLevel;
    [SerializeField] private HardLevelConfigSO hardLevelConfig;
    [SerializeField] private float hardLevelFirstImageMoveDuration = 0.45f;
    [SerializeField] private float hardLevelSecondImageScaleDuration = 0.3f;
    [SerializeField] private float hardLevelSecondImageDelay = 0.08f;
    [SerializeField] private float hardLevelHideDelayAfterIntro = 0.8f;
    [SerializeField] private float hardLevelFirstImageOutroDuration = 0.4f;
    [SerializeField] private float hardLevelFirstImageOutroDistance = 1200f;
    [SerializeField] private float hardLevelFirstImageStartDistance = 900f;
    [SerializeField] private Ease hardLevelMoveEase = Ease.OutBack;
    [SerializeField] private Ease hardLevelScaleEase = Ease.OutBack;
    [SerializeField] private Ease hardLevelOutroEase = Ease.InCubic;

    [Header("HUD Intro Animation")]
    [SerializeField] private bool playHUDIntroOnInit = true;
    [SerializeField] private float bottomUIDelay = 0.2f;
    [SerializeField] private float bottomUIDuration = 0.4f;
    [SerializeField] private float bottomUIStartOffsetY = -350f;
    [SerializeField] private Ease bottomUIEase = Ease.OutCubic;
    [SerializeField] private float topUIDelay = 0.3f;
    [SerializeField] private float topUIDuration = 0.35f;
    [SerializeField] private float topUIStartOffsetY = 260f;
    [SerializeField] private Ease topUIEase = Ease.OutCubic;
    [FormerlySerializedAs("levelElementAnimatorDelayAfterBottomUI")]
    [SerializeField] private float levelElementAnimatorDelayAfterTopUI = 0f;

    [Header("Intro Lite Mode")]
    [SerializeField] private bool enableIntroLiteModeOnLowEnd = true;
    [SerializeField] private int introLiteLowEndSystemMemoryMb = 3000;
    [SerializeField] private int introLiteLowEndProcessorCount = 4;
    [SerializeField] private bool skipHUDIntroOnLowEnd = true;
    [SerializeField] private bool skipHardLevelIntroOnLowEnd = true;

    [Header("HUD Outro Animation (On Win)")]
    [SerializeField] private bool playHUDOutroOnWin = true;
    [SerializeField] private float hudOutroDelayOnWin = 0f;
    [SerializeField] private float hudOutroDurationOnWin = 0.3f;
    [SerializeField] private float bottomUIOutroOffsetY = -350f;
    [SerializeField] private float topUIOutroOffsetY = 260f;
    [SerializeField] private Ease hudOutroEaseOnWin = Ease.InCubic;

    [Header("Last Four Shooter Praise")]
    [SerializeField] private Sprite greatSprite;
    [SerializeField] private Sprite niceSprite;
    [SerializeField] private Sprite awesomeSprite;
    [SerializeField] private Sprite perfectSprite;
    [SerializeField] private Image praiseImage;
    [SerializeField] private Vector2 praiseStartAnchoredPos = Vector2.zero;
    [SerializeField] private Vector2 praiseEndAnchoredPos = new Vector2(0f, 180f);
    [SerializeField] private float praiseMoveDuration = 0.55f;
    [SerializeField] private float praiseHoldDuration = 0.18f;
    [SerializeField] private float praiseFadeOutDuration = 0.2f;

    // â”€â”€â”€ Instruction panel (2-step boosters) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [Header("Instruction Panel (2-step boosters)")]
    [Tooltip("Panel 'Choose Any Shooter' Ä‘Ã£ táº¡o sáºµn trong scene")]
    [SerializeField] private GameObject       boosterInstructionPanel;
    [SerializeField] private Image            boosterInstructionIcon;
    [SerializeField] private Text  boosterInstructionTitle;
    [SerializeField] private Text  boosterInstructionDesc;
    [SerializeField] private Button speedUpButton;
    [SerializeField] private Image speedUpButtonIcon;
    [SerializeField] private Sprite sprite1x;
    [SerializeField] private Sprite sprite2x;
    [SerializeField] private Text coinText;
    [SerializeField] private RectTransform coinFlyTarget;
    [Header("Gameplay Coin Fly (MagicStone)")]
    [SerializeField] private GameObject gameplayCoinFlyPrefab;
    [SerializeField] private RectTransform gameplayCoinFlyParent;
    [SerializeField, Min(0f)] private float gameplayCoinSpawnBounceHeight = 26f;
    [SerializeField, Min(0.05f)] private float gameplayCoinBounceDuration = 0.18f;
    [SerializeField, Min(0.05f)] private float gameplayCoinFlyDuration = 0.34f;
    [SerializeField, Min(0f)] private float gameplayCoinScatterRadius = 22f;
    [SerializeField, Min(0.01f)] private float gameplayCoinStartScale = 0.62f;
    [SerializeField, Min(0.01f)] private float gameplayCoinPopScale = 1.12f;
    [SerializeField, Min(0.01f)] private float gameplayCoinEndScale = 0.86f;
    [SerializeField, Min(0f)] private float gameplayCoinCollectSfxCooldown = 0.08f;
    [SerializeField] private Ease gameplayCoinBounceEase = Ease.OutQuad;
    [SerializeField] private Ease gameplayCoinFlyEase = Ease.InQuad;
    [SerializeField] private Text levelText;
    [Header("Magic Stone Objective UI")]
    [SerializeField] private GameObject magicStoneObjectivePanel;
    [SerializeField] private Text magicStoneObjectiveText;
    [SerializeField] private Button magicStoneButton;
    [SerializeField] private Image magicStoneImage;
    [Tooltip("Hiển thị UI MagicStone từ level này trở lên")]
    [Min(1)]
    [SerializeField] private int showMagicStoneUIAtLevel = 3;
    [Tooltip("Glow VFX khi MagicStone đủ điều kiện dùng")]
    [SerializeField] private ParticleSystem magicStoneReadyHighlight;
    [Header("Magic Stone Debug Hold")]
    [SerializeField] private bool enableMagicStoneHoldDebugTrigger = true;
    [SerializeField, Min(0.1f)] private float magicStoneHoldDebugDuration = 0.35f;
    [SerializeField] private Button settingButton;

    [Header("Debug Level Gestures")]
    [SerializeField] private bool enableLevelDebugGestures = true;
    [SerializeField] private Graphic levelDebugTapTarget;



    // â”€â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private GamePlayController gamePlayController;
    private string pendingBuyBoosterId;
    private int    pendingBuyPrice;
    private bool isSpeedingUp;
    private int activeBoosterPopupPauseCount;

    // Instruction panel animation
    private RectTransform instrPanelRT;
    private Vector2       instrShownPos;
    private Vector2       instrHiddenPos;
    private Vector3 bottomUIShownLocalPos;
    private Vector3 topUIShownLocalPos;
    private bool hasCachedHUDLocalPos;
    private Tween bottomUIIntroTween;
    private Tween topUIIntroTween;
    private Tween bottomUIOutroTween;
    private Tween topUIOutroTween;
    private RectTransform praiseRect;
    private CanvasGroup praiseCanvasGroup;
    private Sequence praiseSequence;
    private EventTrigger levelTextEventTrigger;
    private EventTrigger magicStoneButtonEventTrigger;
    private bool hasBoundLevelDebugGestures;
    private bool hasBoundMagicStoneHoldEvents;
    private bool isHoldingMagicStoneButton;
    private bool hasTriggeredMagicStoneHold;
    private bool suppressMagicStoneClickAfterHold;
    private float magicStoneHoldPressStartTime;
    private Sequence hardLevelIntroSequence;
    private bool shouldShowHardLevelAfterLevelIntro;
    private bool hasPlayedHardLevelIntro;
    private bool useIntroLiteMode;
    private bool hasCachedInstructionPanelLayout;
    private bool hasConfiguredStaticButtonAnims;
    private int cachedCoinBalance = -1;
    private readonly List<Tween> gameplayCoinFlyTweens = new List<Tween>(32);
    private readonly List<GameObject> activeGameplayCoinFlyObjects = new List<GameObject>(32);
    private Tween magicStoneFillTween;

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Init (gá»i trÆ°á»›c InitLevel)
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Khá»Ÿi táº¡o UI cho mÃ n chÆ¡i. Pháº£i gá»i trÆ°á»›c gamePlayController.InitLevel().
    /// </summary>
    /// 

    public void Init(GamePlayController controller)
    {
        gamePlayController = controller;
        useIntroLiteMode = ShouldUseIntroLiteMode();

        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            enableLevelDebugGestures = false;
        }

        AutoUnlockBoostersByCurrentLevel();

        // Init tá»«ng booster button vá»›i reference vá» manager nÃ y
        foreach (var btn in boosterButtons)
            btn?.Initialize(this);

        // áº¨n/hiá»‡n booster panel
        UpdateBoosterPanelVisibility();
        UpdateTopUIVisibility();
        PrepareHardLevelVisualForCurrentLevel();
        CacheHUDShownLocalPositions();

        // Cache instruction panel RT + compute slide positions
        if (boosterInstructionPanel != null)
        {
            if (instrPanelRT == null)
            {
                instrPanelRT = boosterInstructionPanel.GetComponent<RectTransform>();
            }

            if (instrPanelRT != null)
            {
                if (!hasCachedInstructionPanelLayout || instrPanelRT.rect.height <= 0.01f)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(instrPanelRT);
                    instrShownPos = instrPanelRT.anchoredPosition;
                    float panelH = instrPanelRT.rect.height > 0f ? instrPanelRT.rect.height : 400f;
                    instrHiddenPos = instrShownPos + new Vector2(0f, panelH);
                    hasCachedInstructionPanelLayout = true;
                }

                instrPanelRT.anchoredPosition = instrHiddenPos;
            }
        }

        // áº¨n instruction + buy popup
        boosterInstructionPanel?.SetActive(false);
        ResetLastFourPraiseState(true);

        SetButton();

        BindLevelDebugGestures();

        UpdateSpeedUpButtonVisual();
        UpdateMagicStoneObjectiveUI();
        RefreshAllButtons();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Unity lifecycle
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnEnable()
    {
        Instance = this;

        if (GameEventHub.Instance == null) return;
        GameEventHub.Instance.AddListener(GameEventType.OnBoosterActivated,    OnBoosterActivated);
        GameEventHub.Instance.AddListener(GameEventType.OnBoosterDeactivated,  OnBoosterDeactivated);
        GameEventHub.Instance.AddListener(GameEventType.OnBoosterButtonRefresh, OnBoosterButtonRefresh);
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarInit,          OnSlotBarInit);
        GameEventHub.Instance.AddListener(GameEventType.OnShooterAddedToSlot,   OnGameStateChanged);
        GameEventHub.Instance.AddListener(GameEventType.OnShooterDisappear,     OnGameStateChanged);
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarFull,          OnGameStateChanged);
        GameEventHub.Instance.AddListener(GameEventType.OnGameWin,              OnGameWin);
        GameEventHub.Instance.AddListener(GameEventType.OnGameLose,             OnGameLose);
        GameEventHub.Instance.AddListener(GameEventType.OnMagicStoneProgressChanged, OnMagicStoneProgressChanged);
        UpdateCoinAndLevelUI(true);
        UpdateMagicStoneObjectiveUI(animate: false);
        UpdateTopUIVisibility();
        SetButton();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ResetMagicStoneHoldDebugState();

        KillHUDIntroTweens();
        KillHUDOutroTweens();
        KillHardLevelIntroTween();
        shouldShowHardLevelAfterLevelIntro = false;
        hasPlayedHardLevelIntro = false;
        ResetLastFourPraiseState(true);
        KillGameplayCoinFlyTweens();
        if (magicStoneFillTween != null && magicStoneFillTween.IsActive())
        {
            magicStoneFillTween.Kill();
        }
        magicStoneFillTween = null;
        UpdateMagicStoneReadyHighlight(false);

        if (GameEventHub.Instance == null) return;
        GameEventHub.Instance.RemoveListener(GameEventType.OnBoosterActivated,    OnBoosterActivated);
        GameEventHub.Instance.RemoveListener(GameEventType.OnBoosterDeactivated,  OnBoosterDeactivated);
        GameEventHub.Instance.RemoveListener(GameEventType.OnBoosterButtonRefresh, OnBoosterButtonRefresh);
        GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarInit,          OnSlotBarInit);
        GameEventHub.Instance.RemoveListener(GameEventType.OnShooterAddedToSlot,   OnGameStateChanged);
        GameEventHub.Instance.RemoveListener(GameEventType.OnShooterDisappear,     OnGameStateChanged);
        GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarFull,          OnGameStateChanged);
        GameEventHub.Instance.RemoveListener(GameEventType.OnGameWin,              OnGameWin);
        GameEventHub.Instance.RemoveListener(GameEventType.OnGameLose,             OnGameLose);
        GameEventHub.Instance.RemoveListener(GameEventType.OnMagicStoneProgressChanged, OnMagicStoneProgressChanged);
    }

    private void OnSpeedUpClick()
    {
        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        if (SpeedMultiplierManager.Instance == null) return;
        SpeedMultiplierManager.Instance.ToggleSpeedUp();
        UpdateSpeedUpButtonVisual();
        RefreshAllButtons();
    }

    private void UpdateCoinAndLevelUI(bool refreshSavedCoinFromPrefs = false)
    {
        int savedCoins = GetCachedCoinBalance(refreshSavedCoinFromPrefs);
        int pendingMagicStoneCoins = gamePlayController != null
            ? gamePlayController.GetPendingMagicStoneCoinReward()
            : 0;
        coinText.text = (savedCoins + Mathf.Max(0, pendingMagicStoneCoins)).ToString();
        levelText.text = "Level " +PlayerPrefs.GetInt(Const.player_level_key, 1).ToString();
    }

    // -----------------------------------------------------------------------
    // Level Navigation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Chuyển sang level tiếp theo và khởi tạo lại level gameplay.
    /// </summary>
    public void NextLevel()
    {
        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        int nextLevel = currentLevel + 1;
        PlayerPrefs.SetInt(Const.player_level_key, nextLevel);
        PlayerPrefs.Save();

        LoadLevel(nextLevel);
    }

    /// <summary>
    /// Quay lại level trước đó (tối thiểu level 1) và khởi tạo lại level gameplay.
    /// </summary>
    public void BackLevel()
    {
        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        int backLevel = Mathf.Max(1, currentLevel - 1);
        PlayerPrefs.SetInt(Const.player_level_key, backLevel);
        PlayerPrefs.Save();

        LoadLevel(backLevel);
    }

    public void GoToNextLevel() => NextLevel();
    public void GoToBackLevel() => BackLevel();

    private void LoadLevel(int level)
    {
        if (gamePlayController == null)
        {
            gamePlayController = GamePlayController.Instance;
        }

        if (gamePlayController != null)
        {
            gamePlayController.InitLevel(level);
        }

        boosterInstructionPanel?.SetActive(false);
        ResetLastFourPraiseState(true);
        KillGameplayCoinFlyTweens();

        AutoUnlockBoostersByCurrentLevel();
        UpdateBoosterPanelVisibility();
        UpdateTopUIVisibility();
        PrepareHardLevelVisualForCurrentLevel();
        UpdateCoinAndLevelUI(true);
        UpdateMagicStoneObjectiveUI();
        RefreshAllButtons();
    }


    public bool TryGetCoinFlyTargetScreenPosition(out Vector2 screenPosition)
    {
        RectTransform target = coinFlyTarget;
        if (target == null && coinText != null)
        {
            target = coinText.rectTransform;
        }

        if (target == null)
        {
            screenPosition = Vector2.zero;
            return false;
        }

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera uiCamera = null;
        if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = targetCanvas.worldCamera;
        }

        screenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);
        return true;
    }

    public void AddCoinsFromGameplay(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            return;
        }

        if (gamePlayController != null)
        {
            gamePlayController.AddPendingMagicStoneCoinReward(safeAmount);
        }
        else
        {
            int currentCoins = GetCachedCoinBalance();
            SetCoinBalance(currentCoins + safeAmount);
        }

        UpdateCoinAndLevelUI();
    }

    public void PlayGameplayCoinFlyFromWorld(Vector3 worldPosition, int rewardAmount, Camera worldCamera = null)
    {
        int safeReward = Mathf.Max(0, rewardAmount);
        if (safeReward <= 0)
        {
            return;
        }

        if (gameplayCoinFlyPrefab == null)
        {
            AddCoinsFromGameplay(safeReward);
            return;
        }

        RectTransform parentRect = ResolveGameplayCoinFlyParent();
        if (parentRect == null)
        {
            AddCoinsFromGameplay(safeReward);
            return;
        }

        if (!TryGetCoinFlyTargetScreenPosition(out Vector2 targetScreen))
        {
            AddCoinsFromGameplay(safeReward);
            return;
        }

        Camera sourceCamera = worldCamera != null ? worldCamera : Camera.main;
        Vector2 spawnScreen = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPosition);
        Camera uiCamera = GetCanvasEventCamera(parentRect);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, spawnScreen, uiCamera, out Vector2 spawnAnchored))
        {
            AddCoinsFromGameplay(safeReward);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, targetScreen, uiCamera, out Vector2 targetAnchored))
        {
            AddCoinsFromGameplay(safeReward);
            return;
        }

        GameObject coin = ObjectPoolManager.SpawnObject(
            gameplayCoinFlyPrefab,
            parentRect,
            ObjectPoolManager.PoolType.Coin
        );

        if (coin == null)
        {
            AddCoinsFromGameplay(safeReward);
            return;
        }

        activeGameplayCoinFlyObjects.Add(coin);
        coin.transform.SetAsLastSibling();

        RectTransform coinRect = coin.GetComponent<RectTransform>();
        if (coinRect == null)
        {
            AddCoinsFromGameplay(safeReward);
            ReturnActiveGameplayCoinFlyObject(coin);
            return;
        }

        Vector2 scatter = Random.insideUnitCircle * Mathf.Max(0f, gameplayCoinScatterRadius);
        Vector2 start = spawnAnchored + scatter;
        coinRect.anchoredPosition = start;
        coinRect.localScale = Vector3.zero;

        float bounceDuration = Mathf.Max(0.05f, gameplayCoinBounceDuration);
        float flyDuration = Mathf.Max(0.05f, gameplayCoinFlyDuration);

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(coinRect.DOScale(Mathf.Max(0.01f, gameplayCoinPopScale), bounceDuration * 0.55f).SetEase(Ease.OutBack));
        seq.Join(coinRect.DOAnchorPosY(start.y + Mathf.Max(0f, gameplayCoinSpawnBounceHeight), bounceDuration).SetEase(gameplayCoinBounceEase));
        seq.Append(coinRect.DOScale(Mathf.Max(0.01f, gameplayCoinStartScale), bounceDuration * 0.45f).SetEase(Ease.InQuad));
        seq.Append(coinRect.DOScale(Mathf.Max(0.01f, gameplayCoinEndScale), flyDuration * 0.72f).SetEase(Ease.OutQuad));
        seq.Join(coinRect.DOAnchorPos(targetAnchored, flyDuration).SetEase(gameplayCoinFlyEase));
        seq.OnComplete(() =>
        {
            AddCoinsFromGameplay(safeReward);
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                float sfxCooldown = Mathf.Max(0f, gameplayCoinCollectSfxCooldown);
                if (sfxCooldown > 0f)
                {
                    audioManager.TryPlaySFXWithCooldown(Const.goldEarnSFX, sfxCooldown);
                }
                else
                {
                    audioManager.PlaySFX(Const.goldEarnSFX);
                }
            }
            ReturnActiveGameplayCoinFlyObject(coin);
        });
        seq.OnKill(() =>
        {
            if (coin != null)
            {
                ReturnActiveGameplayCoinFlyObject(coin);
            }
        });

        gameplayCoinFlyTweens.Add(seq);
    }

    private RectTransform ResolveGameplayCoinFlyParent()
    {
        if (gameplayCoinFlyParent != null)
        {
            return gameplayCoinFlyParent;
        }

        if (coinFlyTarget != null)
        {
            return coinFlyTarget.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        return rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : null;
    }

    private static Camera GetCanvasEventCamera(RectTransform targetRect)
    {
        if (targetRect == null)
        {
            return null;
        }

        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private void KillGameplayCoinFlyTweens()
    {
        for (int i = 0; i < gameplayCoinFlyTweens.Count; i++)
        {
            Tween tween = gameplayCoinFlyTweens[i];
            if (tween != null && tween.IsActive())
            {
                tween.Kill(false);
            }
        }

        gameplayCoinFlyTweens.Clear();

        for (int i = 0; i < activeGameplayCoinFlyObjects.Count; i++)
        {
            GameObject coin = activeGameplayCoinFlyObjects[i];
            if (coin != null)
            {
                ObjectPoolManager.ReturnObject(coin, ObjectPoolManager.PoolType.Coin);
            }
        }

        activeGameplayCoinFlyObjects.Clear();
    }

    private void ReturnActiveGameplayCoinFlyObject(GameObject coin)
    {
        if (coin == null)
        {
            return;
        }

        activeGameplayCoinFlyObjects.Remove(coin);
        ObjectPoolManager.ReturnObject(coin, ObjectPoolManager.PoolType.Coin);
    }

    private void SetButton()
    {
        if (magicStoneButton == null && magicStoneObjectivePanel != null)
        {
            magicStoneButton = magicStoneObjectivePanel.GetComponentInChildren<Button>(true);
        }

        if (speedUpButton != null)
        {
            if (!hasConfiguredStaticButtonAnims)
            {
                ButtonAnimHelper.AddScaleAnimation(speedUpButton);
            }

            speedUpButton.onClick.RemoveListener(OnSpeedUpClick);
            speedUpButton.onClick.AddListener(OnSpeedUpClick);
        }

        if (settingButton != null)
        {
            if (!hasConfiguredStaticButtonAnims)
            {
                ButtonAnimHelper.AddScaleAnimation(settingButton);
            }

            settingButton.onClick.RemoveListener(OnSettingButtonClicked);
            settingButton.onClick.AddListener(OnSettingButtonClicked);
        }

        if (magicStoneButton != null)
        {
            if (magicStoneReadyHighlight == null)
            {
                Transform highlightTransform = magicStoneButton.transform.Find("ActiveHighlight");
                if (highlightTransform != null)
                {
                    magicStoneReadyHighlight = highlightTransform.GetComponent<ParticleSystem>();
                }

                if (magicStoneReadyHighlight == null)
                {
                    magicStoneReadyHighlight = magicStoneButton.GetComponentInChildren<ParticleSystem>(true);
                }
            }

            if (!hasConfiguredStaticButtonAnims)
            {
                ButtonAnimHelper.AddScaleAnimation(magicStoneButton);
            }

            magicStoneButton.onClick.RemoveListener(OnMagicStoneButtonClicked);
            magicStoneButton.onClick.AddListener(OnMagicStoneButtonClicked);
            BindMagicStoneHoldDebugEvents();
        }

        hasConfiguredStaticButtonAnims = true;
    }

    private void OnSettingButtonClicked()
    {
        UIManager.Instance?.ShowPopup(Const.settingIngamePopUp);
    }

    private void OnMagicStoneButtonClicked()
    {
        if (suppressMagicStoneClickAfterHold)
        {
            suppressMagicStoneClickAfterHold = false;
            return;
        }

        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        if (gamePlayController == null)
        {
            return;
        }

        gamePlayController.TryActivateMagicStoneClearFromUI();
        UpdateMagicStoneObjectiveUI();
        RefreshAllButtons();
    }

    private void Update()
    {
        if (!isHoldingMagicStoneButton || hasTriggeredMagicStoneHold)
        {
            return;
        }

        float holdDuration = Mathf.Max(0.1f, magicStoneHoldDebugDuration);
        if ((Time.unscaledTime - magicStoneHoldPressStartTime) < holdDuration)
        {
            return;
        }

        TryTriggerMagicStoneDebugHold();
    }

    private bool IsMagicStoneHoldDebugEnabled()
    {
        if (!enableMagicStoneHoldDebugTrigger)
        {
            return false;
        }

        return Application.isEditor || Debug.isDebugBuild;
    }

    private void BindMagicStoneHoldDebugEvents()
    {
        if (magicStoneButton == null || hasBoundMagicStoneHoldEvents)
        {
            return;
        }

        magicStoneButtonEventTrigger = magicStoneButton.GetComponent<EventTrigger>();
        if (magicStoneButtonEventTrigger == null)
        {
            magicStoneButtonEventTrigger = magicStoneButton.gameObject.AddComponent<EventTrigger>();
        }

        if (magicStoneButtonEventTrigger.triggers == null)
        {
            magicStoneButtonEventTrigger.triggers = new List<EventTrigger.Entry>();
        }

        AddMagicStoneButtonTrigger(EventTriggerType.PointerDown, OnMagicStoneButtonPointerDown);
        AddMagicStoneButtonTrigger(EventTriggerType.PointerUp, OnMagicStoneButtonPointerUpOrExit);
        AddMagicStoneButtonTrigger(EventTriggerType.PointerExit, OnMagicStoneButtonPointerUpOrExit);
        hasBoundMagicStoneHoldEvents = true;
    }

    private void AddMagicStoneButtonTrigger(EventTriggerType eventType, System.Action<BaseEventData> callback)
    {
        if (magicStoneButtonEventTrigger == null)
        {
            return;
        }

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(eventData => callback?.Invoke(eventData));
        magicStoneButtonEventTrigger.triggers.Add(entry);
    }

    private void OnMagicStoneButtonPointerDown(BaseEventData _)
    {
        if (!IsMagicStoneHoldDebugEnabled() || magicStoneButton == null || !magicStoneButton.interactable)
        {
            return;
        }

        isHoldingMagicStoneButton = true;
        hasTriggeredMagicStoneHold = false;
        magicStoneHoldPressStartTime = Time.unscaledTime;
    }

    private void OnMagicStoneButtonPointerUpOrExit(BaseEventData _)
    {
        isHoldingMagicStoneButton = false;
    }

    private void TryTriggerMagicStoneDebugHold()
    {
        if (!IsMagicStoneHoldDebugEnabled() || gamePlayController == null)
        {
            return;
        }

        bool started = gamePlayController.TryActivateMagicStoneClearDebugFromUI();
        if (!started)
        {
            return;
        }

        hasTriggeredMagicStoneHold = true;
        suppressMagicStoneClickAfterHold = true;
        isHoldingMagicStoneButton = false;
        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        UpdateMagicStoneObjectiveUI();
        RefreshAllButtons();
    }

    private void ResetMagicStoneHoldDebugState()
    {
        isHoldingMagicStoneButton = false;
        hasTriggeredMagicStoneHold = false;
        suppressMagicStoneClickAfterHold = false;
        magicStoneHoldPressStartTime = 0f;
    }

    private void BindLevelDebugGestures()
    {
        if (hasBoundLevelDebugGestures)
        {
            return;
        }

        Graphic tapTarget = ResolveLevelDebugTapTarget();
        if (tapTarget == null)
        {
            return;
        }

        levelTextEventTrigger = tapTarget.GetComponent<EventTrigger>();
        if (levelTextEventTrigger == null)
        {
            levelTextEventTrigger = tapTarget.gameObject.AddComponent<EventTrigger>();
        }

        if (levelTextEventTrigger.triggers == null)
        {
            levelTextEventTrigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        }

        AddLevelDebugTrigger(EventTriggerType.PointerClick, OnLevelDebugPointerClick);
        hasBoundLevelDebugGestures = true;
    }

    private Graphic ResolveLevelDebugTapTarget()
    {
        if (levelDebugTapTarget != null)
        {
            return levelDebugTapTarget;
        }

        if (levelText == null)
        {
            return null;
        }

        Transform parent = levelText.transform.parent;
        if (parent != null)
        {
            Graphic parentGraphic = parent.GetComponent<Graphic>();
            if (parentGraphic != null)
            {
                return parentGraphic;
            }
        }

        return levelText;
    }

    private void AddLevelDebugTrigger(EventTriggerType eventType, System.Action<BaseEventData> callback)
    {
        if (levelTextEventTrigger == null)
        {
            return;
        }

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(eventData => callback?.Invoke(eventData));
        levelTextEventTrigger.triggers.Add(entry);
    }

    private void OnLevelDebugPointerClick(BaseEventData _)
    {
        if (!enableLevelDebugGestures)
        {
            return;
        }

        ToggleAutoPlayToolPanel();
    }

    private void ToggleAutoPlayToolPanel()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        AutoPlayWinTool.ToggleOverlayFromExternal();
        #endif
    }

    private void UpdateSpeedUpButtonVisual()
    {
        if (speedUpButtonIcon == null) return;

        bool isSpeedUpActive = SpeedMultiplierManager.IsSpeedUpActive();
        if (isSpeedUpActive)
            speedUpButtonIcon.sprite = sprite2x;
        else
            speedUpButtonIcon.sprite = sprite1x;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Event callbacks
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnBoosterActivated(object data)
    {
        // ThÃªm dáº¥u ngoáº·c Ä‘Æ¡n () bao trá»n 2 Ä‘iá»u kiá»‡n OR láº¡i vá»›i nhau
        if (data is BoosterManager.ActiveBoosterMode mode &&
           (mode == BoosterManager.ActiveBoosterMode.PickLockedShooter || mode == BoosterManager.ActiveBoosterMode.HeroShooter))
        {
            ShowInstructionPanel(mode);
        }

        RefreshAllButtons();
    }

    private void OnBoosterDeactivated(object _)
    {
        HideInstructionPanel();
        RefreshAllButtons();
    }

    private void OnBoosterButtonRefresh(object _)
    {
        UpdateSpeedUpButtonVisual();
        UpdateBoosterPanelVisibility();
        UpdateTopUIVisibility();
        UpdateMagicStoneObjectiveUI();
        RefreshAllButtons();
    }
    private void OnSlotBarInit(object _)
    {
        UpdateMagicStoneObjectiveUI(animate: false);
        RefreshAllButtons();
    }

    private void OnGameStateChanged(object _)
    {
        UpdateMagicStoneObjectiveUI();
        RefreshAllButtons();
    }

    private void OnMagicStoneProgressChanged(object _)
    {
        UpdateMagicStoneObjectiveUI();
    }

    private void OnGameWin(object _)
    {
        UpdateCoinAndLevelUI(true);
        PlayHUDOutroOnWin();
    }

    private void OnGameLose(object _)
    {
        UpdateCoinAndLevelUI();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Public API â€” gá»i tá»« BoosterButtonPrefab
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Tráº£ vá» cÃ³ thá»ƒ dÃ¹ng booster nÃ y lÃºc nÃ y khÃ´ng (há»i GamePlayController).
    /// </summary>
    public bool CanUseBooster(string boosterId)
        => gamePlayController?.CanUseBooster(boosterId) ?? false;

    /// <summary>
    /// Sá»­ dá»¥ng booster â€” gá»i khi button cÃ³ count >= 1 Ä‘Æ°á»£c nháº¥n.
    /// Náº¿u Ä‘ang active 2-step cá»§a chÃ­nh booster nÃ y â†’ cancel thay vÃ¬ activate láº¡i.
    /// </summary>
    public void OnUseBooster(string boosterId)
    {
        IBoosterStrategy strategy = BoosterManager.Instance?.GetStrategy(boosterId);
        if (strategy == null) return;

        BoosterManager.Instance.TryActivate(strategy);
        RefreshAllButtons();
    }

    /// <summary>
    /// Hiá»ƒn thá»‹ áº£nh praise cho 4 shooter cuá»‘i: 4=Great, 3=Nice, 2=Awesome, 1=Perfect.
    /// </summary>
    public void ShowLastFourShooterPraise(int remainingBeforeDisappear)
    {
        Sprite praiseSprite = GetPraiseSpriteByRemaining(remainingBeforeDisappear);
        if (praiseSprite == null)
        {
            return;
        }

        if (!EnsurePraiseImage())
        {
            return;
        }

        if (praiseSequence != null && praiseSequence.IsActive())
        {
            praiseSequence.Kill();
        }

        praiseImage.sprite = praiseSprite;
        praiseImage.SetNativeSize();
        praiseImage.gameObject.SetActive(true);

        praiseRect.anchoredPosition = praiseStartAnchoredPos;
        praiseCanvasGroup.alpha = 0f;

        float moveDuration = Mathf.Max(0.05f, praiseMoveDuration);
        praiseSequence = DOTween.Sequence();
        praiseSequence.Join(praiseRect.DOAnchorPos(praiseEndAnchoredPos, moveDuration).SetEase(Ease.OutBack));
        praiseSequence.Join(praiseCanvasGroup.DOFade(1f, moveDuration).SetEase(Ease.OutBack));
        praiseSequence.AppendInterval(Mathf.Max(0f, praiseHoldDuration));
        praiseSequence.Append(praiseCanvasGroup.DOFade(0f, Mathf.Max(0.05f, praiseFadeOutDuration)).SetEase(Ease.OutQuad));
        praiseSequence.OnComplete(() =>
        {
            if (praiseImage != null)
            {
                praiseImage.gameObject.SetActive(false);
            }
        });
    }

    /// <summary>
    /// Hiá»‡n popup mua booster â€” gá»i khi button bá»‹ nháº¥n nhÆ°ng count = 0.
    /// </summary>
    public void ShowBuyPopup(string boosterId, int price)
    {
        string popupName = GetPopupNameForBooster(boosterId);
        if (string.IsNullOrEmpty(popupName))
        {
            ;
            return;
        }

        if (UIManager.Instance == null)
        {
            return;
        }

        pendingBuyBoosterId = boosterId;
        pendingBuyPrice = Mathf.Max(0, price);

        UIManager.Instance.ShowPopup(popupName, popup =>
        {
            if (popup == null)
            {
                return;
            }

            AttachBoosterPopupPauseRelay(popup);
            RequestBoosterPopupPause();

            Button coinButton = FindCoinPurchaseButtonForBooster(popup, boosterId);
            if (coinButton == null)
            {
                ;
                return;
            }

            int resolvedPopupPrice = ResolvePopupCoinPriceForBooster(popup, boosterId, pendingBuyPrice);
            pendingBuyPrice = resolvedPopupPrice;

            coinButton.onClick.RemoveAllListeners();
            coinButton.onClick.AddListener(() =>
            {
                HandleBoosterPurchaseByCoin(popup, pendingBuyBoosterId, resolvedPopupPrice);
            });
        });

    }

    private void AttachBoosterPopupPauseRelay(BasePopUp popup)
    {
        if (popup == null)
        {
            return;
        }

        BoosterPopupPauseRelay relay = popup.GetComponent<BoosterPopupPauseRelay>();
        if (relay == null)
        {
            relay = popup.gameObject.AddComponent<BoosterPopupPauseRelay>();
        }

        relay.Initialize(OnBoosterPopupClosed);
    }

    private void OnBoosterPopupClosed()
    {
        ReleaseBoosterPopupPause();
    }

    private void RequestBoosterPopupPause()
    {
        activeBoosterPopupPauseCount++;
        if (activeBoosterPopupPauseCount == 1)
        {
            GameEventHub.Instance?.Invoke(GameEventType.OnGamePause, true);
        }
    }

    private void ReleaseBoosterPopupPause()
    {
        if (activeBoosterPopupPauseCount <= 0)
        {
            activeBoosterPopupPauseCount = 0;
            return;
        }

        activeBoosterPopupPauseCount--;
        if (activeBoosterPopupPauseCount == 0)
        {
            GameEventHub.Instance?.Invoke(GameEventType.OnGamePause, false);
        }
    }

    /// <summary>
    /// Init sample data cho táº¥t cáº£ booster Ä‘ang quáº£n lÃ½ â€” tiá»‡n test.
    /// </summary>
    [ContextMenu("Init Sample Data")]
    public void InitSampleData()
    {

        PlayerData.Instance?.InitSampleData();
        UpdateBoosterPanelVisibility();
        RefreshAllButtons();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Internal helpers
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void RefreshAllButtons()
    {
        foreach (var btn in boosterButtons)
            btn?.Refresh();
    }

    private void UpdateBoosterPanelVisibility()
    {
        Profiler.BeginSample("InGameUIManager.UpdateBoosterPanelVisibility");

        if (boosterPanel == null)
        {
            Profiler.EndSample();
            return;
        }

        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        bool forceShowByLevel = currentLevel >= Mathf.Max(1, showBottomUIAtLevel);

        bool anyUnlocked = false;
        foreach (var btn in boosterButtons)
        {
            var cfg = btn?.GetConfig();
            if (cfg != null && BoosterUnlockPrefs.IsBoosterUnlocked(cfg.boosterName))
            {
                anyUnlocked = true;
                break;
            }
        }

        SetActiveIfChanged(boosterPanel, anyUnlocked || forceShowByLevel);
        Profiler.EndSample();
    }

    private Sprite GetPraiseSpriteByRemaining(int remainingBeforeDisappear)
    {
        switch (remainingBeforeDisappear)
        {
            case 4:
                return greatSprite;
            case 3:
                return niceSprite;
            case 2:
                return awesomeSprite;
            case 1:
                return perfectSprite;
            default:
                return null;
        }
    }

    private bool EnsurePraiseImage()
    {
        if (praiseImage == null)
        {
            GameObject imageObj = new GameObject("LastFourShooterPraise", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            imageObj.transform.SetParent(transform, false);

            praiseImage = imageObj.GetComponent<Image>();
            praiseRect = imageObj.GetComponent<RectTransform>();
            praiseCanvasGroup = imageObj.GetComponent<CanvasGroup>();

            praiseRect.anchorMin = new Vector2(0.5f, 0.5f);
            praiseRect.anchorMax = new Vector2(0.5f, 0.5f);
            praiseRect.pivot = new Vector2(0.5f, 0.5f);
            praiseRect.anchoredPosition = praiseStartAnchoredPos;
            praiseImage.raycastTarget = false;
            praiseImage.gameObject.SetActive(false);
        }

        if (praiseRect == null)
        {
            praiseRect = praiseImage.GetComponent<RectTransform>();
        }

        if (praiseCanvasGroup == null)
        {
            praiseCanvasGroup = praiseImage.GetComponent<CanvasGroup>();
            if (praiseCanvasGroup == null)
            {
                praiseCanvasGroup = praiseImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        return praiseImage != null && praiseRect != null && praiseCanvasGroup != null;
    }

    private void ResetLastFourPraiseState(bool hideImage)
    {
        if (praiseSequence != null && praiseSequence.IsActive())
        {
            praiseSequence.Kill();
        }

        if (hideImage && praiseImage != null)
        {
            praiseImage.gameObject.SetActive(false);
        }

        if (praiseCanvasGroup != null)
        {
            praiseCanvasGroup.alpha = 0f;
        }
    }

    private void UpdateTopUIVisibility()
    {
        if (topUIPanel == null) return;

        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        bool shouldShowTopUI = currentLevel >= Mathf.Max(1, showTopUIAtLevel);
        SetActiveIfChanged(topUIPanel, shouldShowTopUI);
    }

    private void UpdateMagicStoneObjectiveUI(bool animate = true)
    {
        int required = gamePlayController != null
            ? gamePlayController.GetMagicStoneClearCost()
            : 3;

        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        bool reachedMagicStoneLevel = currentLevel >= Mathf.Max(1, showMagicStoneUIAtLevel);

        bool shouldShow = magicStoneObjectivePanel != null && gamePlayController != null && reachedMagicStoneLevel;
        if (magicStoneObjectivePanel != null)
        {
            SetActiveIfChanged(magicStoneObjectivePanel, shouldShow);
        }

        if (!shouldShow)
        {
            if (magicStoneButton != null)
            {
                magicStoneButton.interactable = false;
            }

            UpdateMagicStoneReadyHighlight(false);
            return;
        }

        int collected = BaseShooter.GetCollectedMagicStoneForCurrentLevel();
        if (collected > 3)
        {
            collected = 3;
        }

        if (magicStoneObjectiveText != null)
        {
            magicStoneObjectiveText.text = Mathf.Max(0, collected) + "/" + Mathf.Max(1, required);
        }

        if (magicStoneImage == null && magicStoneObjectivePanel != null)
        {
            Transform imgChild = magicStoneObjectivePanel.transform.Find("MagicStoneImage");
            if (imgChild == null)
            {
                imgChild = magicStoneObjectivePanel.transform.Find("ProgressImage");
            }
            if (imgChild != null)
            {
                magicStoneImage = imgChild.GetComponent<Image>();
            }
        }

        if (magicStoneImage != null)
        {
            float targetFill = Mathf.Clamp01((float)collected / 3f);

            if (magicStoneFillTween != null && magicStoneFillTween.IsActive())
            {
                magicStoneFillTween.Kill();
            }

            if (animate && gameObject.activeInHierarchy && !Mathf.Approximately(magicStoneImage.fillAmount, targetFill))
            {
                magicStoneFillTween = DOVirtual.Float(
                    magicStoneImage.fillAmount,
                    targetFill,
                    0.45f,
                    fill =>
                    {
                        if (magicStoneImage != null)
                        {
                            magicStoneImage.fillAmount = fill;
                        }
                    }
                ).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            else
            {
                magicStoneImage.fillAmount = targetFill;
            }
        }

        if (magicStoneButton != null)
        {
            bool canNormalActivate = gamePlayController != null && gamePlayController.CanActivateMagicStoneClear();
            bool canDebugHoldActivate = gamePlayController != null && IsMagicStoneHoldDebugEnabled() && gamePlayController.CanActivateMagicStoneClearDebugBypassCost();
            magicStoneButton.interactable = canNormalActivate || canDebugHoldActivate;
        }

        bool shouldHighlight = collected >= Mathf.Max(1, required);
        UpdateMagicStoneReadyHighlight(shouldHighlight);
    }

    private void UpdateMagicStoneReadyHighlight(bool shouldHighlight)
    {
        if (magicStoneReadyHighlight == null)
        {
            return;
        }

        GameObject highlightObject = magicStoneReadyHighlight.gameObject;
        if (shouldHighlight)
        {
            if (!highlightObject.activeSelf)
            {
                highlightObject.SetActive(true);
            }

            if (!magicStoneReadyHighlight.isPlaying)
            {
                magicStoneReadyHighlight.Play(true);
            }

            return;
        }

        if (magicStoneReadyHighlight.isPlaying)
        {
            magicStoneReadyHighlight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (highlightObject.activeSelf)
        {
            highlightObject.SetActive(false);
        }
    }

    private void PrepareHardLevelVisualForCurrentLevel()
    {
        if (hardLevel == null)
        {
            shouldShowHardLevelAfterLevelIntro = false;
            hasPlayedHardLevelIntro = false;
            return;
        }

        KillHardLevelIntroTween();
        hasPlayedHardLevelIntro = false;

        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        shouldShowHardLevelAfterLevelIntro = hardLevelConfig != null && hardLevelConfig.IsHardLevel(currentLevel);
        SetActiveIfChanged(hardLevel, false);
    }

    public void PlayHardLevelAfterLevelIntro()
    {
        if (hardLevel == null || hasPlayedHardLevelIntro || !shouldShowHardLevelAfterLevelIntro)
        {
            return;
        }

        if (useIntroLiteMode && skipHardLevelIntroOnLowEnd)
        {
            SetActiveIfChanged(hardLevel, false);
            hasPlayedHardLevelIntro = true;
            return;
        }

        SetActiveIfChanged(hardLevel, true);
        PlayHardLevelIntroAnimation();
        hasPlayedHardLevelIntro = true;
    }

    private void PlayHardLevelIntroAnimation()
    {
        if (hardLevel == null || !hardLevel.activeInHierarchy)
        {
            return;
        }

        Transform hardLevelRoot = hardLevel.transform;
        if (hardLevelRoot.childCount < 2)
        {
            ;
            return;
        }

        Transform firstImage = hardLevelRoot.GetChild(1);
        if (firstImage == null || firstImage.childCount < 1)
        {
            ;
            return;
        }

        Transform secondImage = firstImage.GetChild(0);
        if (firstImage == null || secondImage == null)
        {
            return;
        }

        firstImage.gameObject.SetActive(true);
        secondImage.gameObject.SetActive(true);

        firstImage.DOKill();
        secondImage.DOKill();

        Vector3 secondImageTargetScale = secondImage.localScale;
        secondImage.localScale = Vector3.zero;

        Vector2 startOffset = GetHardLevelStartOffsetFromOrigin();
        Tween moveTween;
        Tween outroTween;
        Vector2 outroOffset = new Vector2(
            Mathf.Max(0f, hardLevelFirstImageOutroDistance),
            Mathf.Max(0f, hardLevelFirstImageOutroDistance) * Mathf.Sin(9f * Mathf.Deg2Rad)
        );

        RectTransform firstRect = firstImage as RectTransform;
        if (firstRect != null)
        {
            Vector2 endAnchoredPos = firstRect.anchoredPosition;
            firstRect.anchoredPosition = endAnchoredPos + startOffset;
            moveTween = firstRect.DOAnchorPos(endAnchoredPos, Mathf.Max(0.05f, hardLevelFirstImageMoveDuration));
            outroTween = firstRect.DOAnchorPos(
                endAnchoredPos + outroOffset,
                Mathf.Max(0.05f, hardLevelFirstImageOutroDuration)
            );
        }
        else
        {
            Vector3 endLocalPos = firstImage.localPosition;
            firstImage.localPosition = endLocalPos + new Vector3(startOffset.x, startOffset.y, 0f);
            moveTween = firstImage.DOLocalMove(endLocalPos, Mathf.Max(0.05f, hardLevelFirstImageMoveDuration));
            outroTween = firstImage.DOLocalMove(
                endLocalPos + new Vector3(outroOffset.x, outroOffset.y, 0f),
                Mathf.Max(0.05f, hardLevelFirstImageOutroDuration)
            );
        }

        moveTween.SetEase(hardLevelMoveEase).SetUpdate(true);
        outroTween.SetEase(hardLevelOutroEase).SetUpdate(true);

        Tween scaleTween = secondImage.DOScale(secondImageTargetScale, Mathf.Max(0.05f, hardLevelSecondImageScaleDuration))
            .SetEase(hardLevelScaleEase)
            .SetUpdate(true);

        hardLevelIntroSequence = DOTween.Sequence();
        hardLevelIntroSequence.Append(moveTween);
        hardLevelIntroSequence.AppendInterval(Mathf.Max(0f, hardLevelSecondImageDelay));
        hardLevelIntroSequence.Append(scaleTween);
        hardLevelIntroSequence.AppendInterval(Mathf.Max(0f, hardLevelHideDelayAfterIntro));
        hardLevelIntroSequence.Append(outroTween);
        hardLevelIntroSequence.OnComplete(() =>
        {
            if (hardLevel != null)
            {
                SetActiveIfChanged(hardLevel, false);
            }
        });
        hardLevelIntroSequence.SetUpdate(true);
    }

    private Vector2 GetHardLevelStartOffsetFromOrigin()
    {
        float distance = Mathf.Max(0f, hardLevelFirstImageStartDistance);
        float angleRad = 9f * Mathf.Deg2Rad;
        return new Vector2(-Mathf.Cos(angleRad) * distance, -Mathf.Sin(angleRad) * distance);
    }

    private void KillHardLevelIntroTween()
    {
        if (hardLevelIntroSequence != null && hardLevelIntroSequence.IsActive())
        {
            hardLevelIntroSequence.Kill();
        }

        hardLevelIntroSequence = null;
    }

    private void CacheHUDShownLocalPositions()
    {
        if (boosterPanel != null)
        {
            bottomUIShownLocalPos = boosterPanel.transform.localPosition;
        }

        if (topUIPanel != null)
        {
            topUIShownLocalPos = topUIPanel.transform.localPosition;
        }

        hasCachedHUDLocalPos = true;
    }

    private void PlayHUDIntroAnimation()
    {
        if (!playHUDIntroOnInit)
        {
            return;
        }

        if (useIntroLiteMode && skipHUDIntroOnLowEnd)
        {
            return;
        }

        if (!hasCachedHUDLocalPos)
        {
            CacheHUDShownLocalPositions();
        }

        AnimatePanelFromLocalOffset(
            boosterPanel,
            bottomUIShownLocalPos,
            bottomUIStartOffsetY,
            bottomUIDelay,
            bottomUIDuration,
            bottomUIEase,
            ref bottomUIIntroTween
        );

        AnimatePanelFromLocalOffset(
            topUIPanel,
            topUIShownLocalPos,
            topUIStartOffsetY,
            topUIDelay,
            topUIDuration,
            topUIEase,
            ref topUIIntroTween
        );
    }

    public void PlayHUDIntroAfterLoadingReady()
    {
        PlayHUDIntroAnimation();
    }

    public void PlayHUDOutroImmediatelyOnWin()
    {
        PlayHUDOutroOnWin();
    }

    public float GetLevelElementAnimatorDelayFromHUDStart()
    {
        if (useIntroLiteMode && skipHUDIntroOnLowEnd)
        {
            return Mathf.Max(0f, levelElementAnimatorDelayAfterTopUI);
        }

        float baseDelay = playHUDIntroOnInit ? Mathf.Max(0f, topUIDelay) : 0f;
        float extraDelay = Mathf.Max(0f, levelElementAnimatorDelayAfterTopUI);
        return baseDelay + extraDelay;
    }

    private bool ShouldUseIntroLiteMode()
    {
        if (!enableIntroLiteModeOnLowEnd)
        {
            return false;
        }

        int memoryMb = SystemInfo.systemMemorySize;
        if (memoryMb > 0 && memoryMb <= Mathf.Max(512, introLiteLowEndSystemMemoryMb))
        {
            return true;
        }

        return SystemInfo.processorCount <= Mathf.Max(1, introLiteLowEndProcessorCount);
    }

    private void AnimatePanelFromLocalOffset(
        GameObject panel,
        Vector3 shownLocalPos,
        float startOffsetY,
        float delay,
        float duration,
        Ease ease,
        ref Tween tweenRef)
    {
        if (panel == null || !panel.activeInHierarchy)
        {
            return;
        }

        if (tweenRef != null && tweenRef.IsActive())
        {
            tweenRef.Kill();
        }

        Transform panelTransform = panel.transform;
        panelTransform.localPosition = shownLocalPos + new Vector3(0f, startOffsetY, 0f);
        tweenRef = panelTransform.DOLocalMove(shownLocalPos, Mathf.Max(0.01f, duration))
            .SetEase(ease)
            .SetDelay(Mathf.Max(0f, delay));
    }

    private void KillHUDIntroTweens()
    {
        if (bottomUIIntroTween != null && bottomUIIntroTween.IsActive())
        {
            bottomUIIntroTween.Kill();
        }

        if (topUIIntroTween != null && topUIIntroTween.IsActive())
        {
            topUIIntroTween.Kill();
        }

        bottomUIIntroTween = null;
        topUIIntroTween = null;
    }

    private void PlayHUDOutroOnWin()
    {
        if (!playHUDOutroOnWin)
        {
            return;
        }

        if (!hasCachedHUDLocalPos)
        {
            CacheHUDShownLocalPositions();
        }

        KillHUDIntroTweens();
        KillHUDOutroTweens();

        AnimatePanelToLocalOffset(
            boosterPanel,
            bottomUIShownLocalPos,
            bottomUIOutroOffsetY,
            hudOutroDelayOnWin,
            hudOutroDurationOnWin,
            hudOutroEaseOnWin,
            ref bottomUIOutroTween
        );

        AnimatePanelToLocalOffset(
            topUIPanel,
            topUIShownLocalPos,
            topUIOutroOffsetY,
            hudOutroDelayOnWin,
            hudOutroDurationOnWin,
            hudOutroEaseOnWin,
            ref topUIOutroTween
        );
    }

    private void AnimatePanelToLocalOffset(
        GameObject panel,
        Vector3 baseLocalPos,
        float endOffsetY,
        float delay,
        float duration,
        Ease ease,
        ref Tween tweenRef)
    {
        if (panel == null || !panel.activeInHierarchy)
        {
            return;
        }

        if (tweenRef != null && tweenRef.IsActive())
        {
            tweenRef.Kill();
        }

        Transform panelTransform = panel.transform;
        Vector3 endLocalPos = baseLocalPos + new Vector3(0f, endOffsetY, 0f);
        tweenRef = panelTransform.DOLocalMove(endLocalPos, Mathf.Max(0.01f, duration))
            .SetEase(ease)
            .SetDelay(Mathf.Max(0f, delay))
            .OnComplete(() =>
            {
                if (panel != null)
                {
                    SetActiveIfChanged(panel, false);
                }
            });
    }

    private void KillHUDOutroTweens()
    {
        if (bottomUIOutroTween != null && bottomUIOutroTween.IsActive())
        {
            bottomUIOutroTween.Kill();
        }

        if (topUIOutroTween != null && topUIOutroTween.IsActive())
        {
            topUIOutroTween.Kill();
        }

        bottomUIOutroTween = null;
        topUIOutroTween = null;
    }

    private void AutoUnlockBoostersByCurrentLevel()
    {
        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        List<BoosterStrategyConfig> configs = new List<BoosterStrategyConfig>();

        foreach (var btn in boosterButtons)
        {
            BoosterStrategyConfig cfg = btn?.GetConfig();
            if (cfg != null)
                configs.Add(cfg);
        }

        BoosterUnlockPrefs.EvaluateUnlockByLevel(configs, currentLevel, save: true);
        BoosterManager.Instance?.SyncUnlockedBoosterInitialCount();
    }

    private void ShowInstructionPanel(BoosterManager.ActiveBoosterMode mode = BoosterManager.ActiveBoosterMode.None)
    {
        if (boosterInstructionPanel == null || instrPanelRT == null) return;

        if ((mode == BoosterManager.ActiveBoosterMode.PickLockedShooter ||
             mode == BoosterManager.ActiveBoosterMode.HeroShooter) &&
            TutorialManager.Instance != null &&
            TutorialManager.Instance.IsTutorialActive)
        {
            TutorialManager.Instance.TryAdoptBoosterDescriptionBg(boosterInstructionPanel);
        }

        var activeCfg = BoosterManager.Instance?.ActiveConfig;
        if (boosterInstructionIcon  != null) boosterInstructionIcon.sprite = activeCfg?.activeIcon;
        if (boosterInstructionTitle != null) boosterInstructionTitle.text  = activeCfg?.boosterName ?? "";
        if (boosterInstructionDesc  != null) boosterInstructionDesc.text   = !string.IsNullOrEmpty(activeCfg?.instructionText)
                                                                              ? activeCfg.instructionText
                                                                              : activeCfg?.description ?? "";
        instrPanelRT.DOKill();
        instrPanelRT.anchoredPosition = instrHiddenPos;
        SetActiveIfChanged(boosterInstructionPanel, true);
        instrPanelRT.DOAnchorPos(instrShownPos, 0.3f).SetEase(Ease.OutCubic);
    }

    private void HideInstructionPanel()
    {
        if (boosterInstructionPanel == null || instrPanelRT == null)
        {
            SetActiveIfChanged(boosterInstructionPanel, false);
            return;
        }
        instrPanelRT.DOKill();
        instrPanelRT.DOAnchorPos(instrHiddenPos, 0.25f)
                    .SetEase(Ease.InCubic)
                    .OnComplete(() => SetActiveIfChanged(boosterInstructionPanel, false));
    }

    private static void SetActiveIfChanged(GameObject target, bool shouldBeActive)
    {
        if (target == null || target.activeSelf == shouldBeActive)
        {
            return;
        }

        target.SetActive(shouldBeActive);
    }

    private void OnInstructionCancel()
    {
        BoosterManager.Instance?.CancelPickLockedShooterMode();
        HideInstructionPanel();
    }

    private void OnBuyConfirm()
    {
        if (string.IsNullOrEmpty(pendingBuyBoosterId)) return;
        HandleBoosterPurchaseByCoin(null, pendingBuyBoosterId, pendingBuyPrice);
    }

    private void OnBuyCancel()
    {
        //pendingBuyBoosterId = null;
        //buyBoosterPopup?.SetActive(false);
    }

    private static bool IsMoveShooterBooster(string boosterId)
    {
        return boosterId == Const.BOOSTER_UNLOCKSHOOTER || boosterId == "Spinner" || boosterId == "PickLockedShooter";
    }

    private string GetPopupNameForBooster(string boosterId)
    {
        if (boosterId == Const.BOOSTER_ADDSLOT)
        {
            return Const.addDeckPopUp;
        }

        if (IsMoveShooterBooster(boosterId))
        {
            return Const.addMoveShooterPopUp;
        }

        if (boosterId == Const.BOOSTER_HERO)
        {
            return Const.addSuperShooterPopUp;
        }

        return null;
    }

    private void HandleBoosterPurchaseByCoin(BasePopUp popup, string boosterId, int price)
    {
        if (string.IsNullOrEmpty(boosterId))
        {
            return;
        }

        int safePrice = Mathf.Max(0, price);
        if (!TrySpendCoins(safePrice))
        {
            ;
            return;
        }

        int addAmount = BoosterManager.Instance != null ? BoosterManager.Instance.GetPurchaseAmount(boosterId) : 3;

        if (BoosterManager.Instance == null || !BoosterManager.Instance.AddBooster(boosterId, addAmount))
        {
            AddCoinsBack(safePrice);
            return;
        }

        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        popup?.Hide();
        UpdateCoinAndLevelUI();
        RefreshAllButtons();
    }

    private bool TrySpendCoins(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount == 0)
        {
            return true;
        }

        int currentCoins = GetCachedCoinBalance();
        if (currentCoins < safeAmount)
        {
            return false;
        }

        SetCoinBalance(currentCoins - safeAmount);
        return true;
    }

    private void AddCoinsBack(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount == 0)
        {
            return;
        }

        int currentCoins = GetCachedCoinBalance();
        SetCoinBalance(currentCoins + safeAmount);
    }

    private void SetCoinBalance(int targetCoin)
    {
        int safeCoin = Mathf.Max(0, targetCoin);
        cachedCoinBalance = safeCoin;
        PlayerPrefs.SetInt(Const.player_coins_key, safeCoin);
        SyncCoinBalanceToPlayerData(safeCoin);
        PlayerPrefs.Save();
    }

    private int GetCachedCoinBalance(bool forceRefresh = false)
    {
        if (forceRefresh || cachedCoinBalance < 0)
        {
            cachedCoinBalance = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_coins_key, 0));
        }

        return cachedCoinBalance;
    }

    private void SyncCoinBalanceToPlayerData(int targetCoin)
    {
        if (PlayerData.Instance == null)
        {
            return;
        }

        int currentDataCoin = PlayerData.Instance.GetCoinBalance();
        if (currentDataCoin < targetCoin)
        {
            PlayerData.Instance.AddCoins(targetCoin - currentDataCoin);
        }
        else if (currentDataCoin > targetCoin)
        {
            PlayerData.Instance.SpendCoins(currentDataCoin - targetCoin);
        }
    }

    private Button FindCoinPurchaseButtonForBooster(BasePopUp popup, string boosterId)
    {
        if (popup == null)
        {
            return null;
        }

        if (boosterId == Const.BOOSTER_ADDSLOT)
        {
            AddDeckPopup addDeckPopup = popup.GetComponent<AddDeckPopup>();
            if (addDeckPopup == null)
            {
                addDeckPopup = popup.gameObject.AddComponent<AddDeckPopup>();
            }

            Button btn = addDeckPopup.GetCoinButton();
            if (btn != null)
            {
                return btn;
            }
        }

        if (IsMoveShooterBooster(boosterId))
        {
            AddMoveShooterPopup addMoveShooterPopup = popup.GetComponent<AddMoveShooterPopup>();
            if (addMoveShooterPopup == null)
            {
                addMoveShooterPopup = popup.gameObject.AddComponent<AddMoveShooterPopup>();
            }

            Button btn = addMoveShooterPopup.GetCoinButton();
            if (btn != null)
            {
                return btn;
            }
        }

        if (boosterId == Const.BOOSTER_HERO)
        {
            AddSuperShooterPopup addSuperShooterPopup = popup.GetComponent<AddSuperShooterPopup>();
            if (addSuperShooterPopup == null)
            {
                addSuperShooterPopup = popup.gameObject.AddComponent<AddSuperShooterPopup>();
            }

            Button btn = addSuperShooterPopup.GetCoinButton();
            if (btn != null)
            {
                return btn;
            }
        }

        return FindCoinPurchaseButtonFallback(popup);
    }

    private int ResolvePopupCoinPriceForBooster(BasePopUp popup, string boosterId, int fallbackPrice)
    {
        int safeFallbackPrice = Mathf.Max(0, fallbackPrice);
        if (popup == null)
        {
            return safeFallbackPrice;
        }

        if (boosterId == Const.BOOSTER_ADDSLOT)
        {
            AddDeckPopup addDeckPopup = popup.GetComponent<AddDeckPopup>();
            if (addDeckPopup != null)
            {
                return addDeckPopup.GetCoinCost(safeFallbackPrice);
            }
        }

        if (IsMoveShooterBooster(boosterId))
        {
            AddMoveShooterPopup addMoveShooterPopup = popup.GetComponent<AddMoveShooterPopup>();
            if (addMoveShooterPopup != null)
            {
                return addMoveShooterPopup.GetCoinCost(safeFallbackPrice);
            }
        }

        if (boosterId == Const.BOOSTER_HERO)
        {
            AddSuperShooterPopup addSuperShooterPopup = popup.GetComponent<AddSuperShooterPopup>();
            if (addSuperShooterPopup != null)
            {
                return addSuperShooterPopup.GetCoinCost(safeFallbackPrice);
            }
        }

        return safeFallbackPrice;
    }

    private Button FindCoinPurchaseButtonFallback(BasePopUp popup)
    {
        Button[] buttons = popup.GetComponentsInChildren<Button>(true);
        Button fallback = null;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null)
            {
                continue;
            }

            string buttonName = candidate.gameObject.name.ToLowerInvariant();
            if (buttonName.Contains("close") || buttonName.Contains("cancel") || buttonName == "x")
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (buttonName.Contains("coin") || buttonName.Contains("buy") || buttonName.Contains("usecoin"))
            {
                return candidate;
            }
        }

        return fallback;
    }
}

