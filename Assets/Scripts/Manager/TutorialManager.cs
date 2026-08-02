using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Tutorial Canvas")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private CanvasGroup tutorialCanvasGroup;
    [SerializeField] private Camera tutorialCamera;
    [SerializeField] private Camera mainCamera;

    [Header("UI Elements")]
    [SerializeField] private Image dimOverlay;
    [SerializeField] private RectTransform targetContainer; // Container Ä‘á»ƒ reparent target button vÃ o
    [SerializeField] private Text instructionText;
    [SerializeField] private GameObject instructionPanel;


    // âœ… Arrow CanvasGroup Ä‘á»ƒ control visibility mÃ  khÃ´ng bá»‹ áº£nh hÆ°á»Ÿng SetActive cá»§a panel
    private CanvasGroup arrowCanvasGroup;

    [Header("ArrowImage")]
    [SerializeField] private GameObject arrowImage;
    [SerializeField] private GameObject arrowImageUI;
    [Header("Layer Setting")]
    public LayerMask tutorialCamLayer;

    [Header("Tutorial Configs - Lists (New)")]
    [SerializeField] private List<TutorialConfigSO> tutorials = new List<TutorialConfigSO>();

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float stepTransitionDuration = 0.3f;
    [SerializeField] private float arrowFadeDuration = 0.15f;
    [SerializeField] private float arrowFadeDelay = 0.05f;

    [Header("Performance")]
    [SerializeField] private bool enableVerboseRuntimeLogs = false;
    [SerializeField] private bool lockNonTargetGraphicsRaycast = true;
    [SerializeField, Min(0.1f)] private float uiBlockCacheRefreshInterval = 0.5f;

    // Runtime state
    private TutorialConfigSO currentConfig;
    private int currentStepIndex = 0;
    private GameObject currentTarget;
    private bool isTutorialActive = false;
    private bool isStepTransitionDelayActive = false;

    // Public property Ä‘á»ƒ check tá»« bÃªn ngoÃ i
    public bool IsTutorialActive => isTutorialActive;
    public bool IsStepTransitionDelayActive => isStepTransitionDelayActive;

    // Prewarm helpers de chuan bi cache som trong luc loading, khong thay doi tutorial flow.
    public void PrewarmForLoadingPhase()
    {
        RefreshUIBlockCacheIfNeeded(forceRefresh: true);
    }

    public void PrewarmForLevelRuntime()
    {
        RebuildTutorialTargetLookup();
        RefreshUIBlockCacheIfNeeded(forceRefresh: true);
    }

    // âœ… Queue system Ä‘á»ƒ xá»­ lÃ½ nhiá»u tutorials liÃªn tiáº¿p
    private Queue<string> tutorialQueue = new Queue<string>();

    // âœ… Arrow tween reference
    private Tween arrowTween;
    private Tween pendingNextStepTween;
    private bool isWaitingForMagicStoneProgress;
    private int waitingMagicStoneRequiredCount;
    private float waitingMagicStonePostDelay;
    private ArrowVisualCache currentArrowVisualCache;

    private class ArrowVisualCache
    {
        public CanvasGroup canvasGroup;
        public Graphic[] graphics;
        public SkeletonGraphic[] skeletonGraphics;
        public SpriteRenderer[] spriteRenderers;
        public SkeletonAnimation[] skeletonAnimations;
    }

    // âœ… Drag state tracking
    private bool isDragStep = false;
    private Vector2 dragTargetLocalPos; // Local position cá»§a target trong tutorial panel
    private bool isDragging = false;
    private Vector3 dragPointerStartPos;

    // âœ… Target tracking
    private int originalTargetLayer = -1; // LÆ°u layer gá»‘c cá»§a target Ä‘á»ƒ restore
    private Transform originalTargetParent = null; // LÆ°u parent gá»‘c cá»§a UI
    private int originalTargetSiblingIndex = 0; // LÆ°u vá»‹ trÃ­ index gá»‘c
    private Canvas uiTargetCanvas = null;
    private bool addedUiTargetCanvas = false;
    private bool originalUiTargetCanvasOverrideSorting = false;
    private int originalUiTargetCanvasSortingOrder = 0;
    private int originalUiTargetCanvasSortingLayerId = 0;
    private GraphicRaycaster uiTargetGraphicRaycaster = null;
    private bool addedUiTargetGraphicRaycaster = false;
    private bool originalUiTargetGraphicRaycasterEnabled = false;
    private bool isTargetUI = false; // Target lÃ  UI hay GameObject
    private GameObject currentArrowInstance = null; // Arrow instance hiá»‡n táº¡i Ä‘ang hiá»ƒn thá»‹
    private readonly Dictionary<Selectable, bool> lockedSelectableStates = new Dictionary<Selectable, bool>();
    private readonly Dictionary<Graphic, bool> lockedGraphicRaycastStates = new Dictionary<Graphic, bool>();
    private Button currentTargetButton;
    private UnityAction currentTargetButtonClickAction;
    public GameObject GetCurrentTarget() => currentTarget;

    /// <summary>
    /// Trả về true nếu tutorial đang active VÀ selectable này đang bị lock (không phải target hiện tại).
    /// Dùng để guard các chỗ tự ý set interactable từ bên ngoài TutorialManager.
    /// </summary>
    public bool IsSelectableLockedByTutorial(Selectable selectable)
    {
        if (!isTutorialActive || selectable == null)
        {
            return false;
        }
        return lockedSelectableStates.ContainsKey(selectable);
    }

    private Action onTutorialCompleteCallback;
    private Transform adoptedBoosterDescriptionBgTransform;
    private Transform adoptedBoosterDescriptionBgOriginalParent;
    private int adoptedBoosterDescriptionBgOriginalSiblingIndex = -1;
    private bool suppressInstructionPanelWhileBoosterDescriptionActive;
    private readonly Dictionary<string, GameObject> tutorialTargetLookup = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly List<GameObject> sceneRootsBuffer = new List<GameObject>(64);
    private readonly List<Transform> transformTraversalBuffer = new List<Transform>(512);
    private readonly List<Selectable> selectableBuffer = new List<Selectable>(128);
    private readonly List<Graphic> graphicBuffer = new List<Graphic>(256);
    private readonly List<SplineRoute> conveyorRoutesBuffer = new List<SplineRoute>(8);
    private readonly List<Selectable> cachedSceneSelectables = new List<Selectable>(128);
    private readonly List<Graphic> cachedSceneGraphics = new List<Graphic>(256);
    private Tween boosterRefreshTweenShort;
    private Tween boosterRefreshTweenLong;
    private int cachedUiSceneHandle = -1;
    private int cachedTargetLookupSceneHandle = -1;
    private float nextUiBlockCacheRefreshAt = -1f;


    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (tutorialPanel != null)
        {
            tutorialPanel.gameObject.SetActive(false);
        }

        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 0f;
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
        }

        instructionPanel?.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameEventHub.Instance?.AddListener(GameEventType.OnMagicStoneProgressChanged, OnMagicStoneProgressChanged);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEventHub.Instance?.RemoveListener(GameEventType.OnMagicStoneProgressChanged, OnMagicStoneProgressChanged);
        CancelPendingNextStepTween();
        CancelMagicStoneProgressWait();
        CancelBoosterRefreshTweens();
        ClearCurrentTargetButtonListener();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InvalidateTutorialRuntimeCaches();
    }


    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // PUBLIC API
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    // ──────────────────────────────────────────────────────────────────
    // PUBLIC API (BOOSTER DIM & HIGHLIGHT)
    // ──────────────────────────────────────────────────────────────────

    private readonly List<(GameObject obj, int originalLayer)> boosterHighlightedObjects = new List<(GameObject, int)>();
    private int originalTutorialCanvasSortingOrder = -1;
    private bool hasSavedTutorialCanvasSortingOrder = false;

    /// <summary>
    /// Bật/tắt dim overlay cho Booster (KHÔNG làm tối UI và KHÔNG block button click)
    /// </summary>
    public void SetDimOverlayActiveForBooster(bool active)
    {
        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(active);
            dimOverlay.raycastTarget = false; // Không block click UI
        }

        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.DOKill();
            tutorialCanvasGroup.alpha = active ? 1f : 0f;
            tutorialCanvasGroup.interactable = false; // Không block UI
            tutorialCanvasGroup.blocksRaycasts = false; // Không block UI
        }

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(active);
        }

        // Đưa Canvas tutorial xuống dưới Canvas InGameUI để UI không bị tối
        Canvas tutCanvas = tutorialPanel != null ? tutorialPanel.GetComponentInParent<Canvas>() : null;
        if (tutCanvas != null)
        {
            if (active)
            {
                if (!hasSavedTutorialCanvasSortingOrder)
                {
                    originalTutorialCanvasSortingOrder = tutCanvas.sortingOrder;
                    hasSavedTutorialCanvasSortingOrder = true;
                }

                Canvas inGameCanvas = InGameUIManager.Instance != null ? InGameUIManager.Instance.GetComponentInParent<Canvas>() : null;
                if (inGameCanvas != null)
                {
                    tutCanvas.overrideSorting = true;
                    tutCanvas.sortingLayerID = inGameCanvas.sortingLayerID;
                    tutCanvas.sortingOrder = Mathf.Max(0, inGameCanvas.sortingOrder - 1);
                }
            }
            else
            {
                if (hasSavedTutorialCanvasSortingOrder)
                {
                    tutCanvas.sortingOrder = originalTutorialCanvasSortingOrder;
                    hasSavedTutorialCanvasSortingOrder = false;
                }
            }
        }

        if (!active)
        {
            RestoreBoosterHighlights();
        }
    }

    /// <summary>
    /// Đưa 1 GameObject sang tutorial layer để hiển thị sáng nổi bật trên nền dim
    /// </summary>
    public void HighlightGameObjectForBooster(GameObject obj)
    {
        if (obj == null) return;
        int tutLayer = LayerMaskToLayer(tutorialCamLayer);
        if (tutLayer >= 0)
        {
            boosterHighlightedObjects.Add((obj, obj.layer));
            SetLayerRecursively(obj, tutLayer);
        }
    }

    /// <summary>
    /// Trả tất cả GameObject đã highlight về layer ban đầu
    /// </summary>
    public void RestoreBoosterHighlights()
    {
        for (int i = 0; i < boosterHighlightedObjects.Count; i++)
        {
            var item = boosterHighlightedObjects[i];
            if (item.obj != null)
            {
                SetLayerRecursively(item.obj, item.originalLayer);
            }
        }
        boosterHighlightedObjects.Clear();
    }

    /// <summary>
    /// Quát tất cả tutorial chưa chạy theo type
    /// Tráº£ vá» danh sÃ¡ch tutorial names chÆ°a hoÃ n thÃ nh VÃ€ Ä‘Ã£ Ä‘á»§ Ä‘iá»u kiá»‡n cháº¡y (feature Ä‘Ã£ unlock)
    /// </summary>
    public List<string> GetIncompleteTutorials()
    {
        List<string> incompleteTutorials = new List<string>();

        foreach (var config in tutorials)
        {
            if (config == null || string.IsNullOrEmpty(config.tutorialName)) continue;

            if (!IsTutorialCompleted(config.tutorialName))
            {
                incompleteTutorials.Add(config.tutorialName);
            }
        }
        return incompleteTutorials;
    }



    /// <summary>
    /// Láº¥y config tutorial tá»« tutorialName vÃ  type
    /// </summary>
    private TutorialConfigSO GetTutorialConfig(string tutorialName)
    {
        foreach (var config in tutorials)
        {
            if (config != null && config.tutorialName == tutorialName) return config;
        }
        return null;
    }

    /// <summary>
    /// Báº¯t Ä‘áº§u tutorial (check xem Ä‘Ã£ complete chÆ°a, queue náº¿u cÃ³ tutorial Ä‘ang active)
    /// </summary>
    public void StartTutorial(string tutorialName)
    {
        // Skip náº¿u Ä‘Ã£ complete
        if (IsTutorialCompleted(tutorialName))
        {
            LogTutorial($"[Tutorial] {tutorialName} Ä‘Ã£ hoÃ n thÃ nh trÆ°á»›c Ä‘Ã³");
            return;
        }

        // âœ… Náº¿u cÃ³ tutorial Ä‘ang active â†’ enqueue Ä‘á»ƒ cháº¡y sau
        if (isTutorialActive)
        {
            tutorialQueue.Enqueue(tutorialName);
            LogTutorial($"[Tutorial] Queued tutorial: {tutorialName} (queue size: {tutorialQueue.Count})");
            return;
        }

        // âœ… KhÃ´ng cÃ³ tutorial Ä‘ang active â†’ báº¯t Ä‘áº§u ngay
        StartTutorialInternal(tutorialName);
    }

    /// <summary>
    /// Internal: Thá»±c sá»± báº¯t Ä‘áº§u tutorial (khÃ´ng check queue)
    /// </summary>
    private void StartTutorialInternal(string tutorialName)
    {
        // Sá»­ dá»¥ng tutorialName Ä‘á»ƒ tÃ¬m config phÃ¹ há»£p
        currentConfig = GetTutorialConfig(tutorialName);

        // Chinh cam tutorial size
        tutorialCamera.orthographicSize = mainCamera.orthographicSize;
        if (currentConfig == null)
        {
            ;
            TryStartNextInQueue(); // Thá»­ tutorial tiáº¿p theo trong queue
            return;
        }

        if (currentConfig.steps.Count == 0)
        {
            LogTutorialWarning($"[Tutorial] Config {tutorialName} khÃ´ng cÃ³ steps!");
            CompleteTutorial();
            return;
        }

        currentStepIndex = 0;
        isTutorialActive = true;
        isStepTransitionDelayActive = false;
        InvalidateTutorialRuntimeCaches();
        SetConveyorsPaused(true);

        LogTutorial($"[Tutorial] Starting: {tutorialName}");



        // Show canvas vá»›i animation
        SetTutorialPanelVisible(true, true);
        tutorialCanvasGroup.DOKill();
        tutorialCanvasGroup.alpha = 0f;
        tutorialCanvasGroup.interactable = false;
        tutorialCanvasGroup.blocksRaycasts = false;
        tutorialCanvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true).OnComplete(() =>
        {
            if (tutorialCanvasGroup != null)
            {
                tutorialCanvasGroup.interactable = true;
                tutorialCanvasGroup.blocksRaycasts = true;
            }

            ShowStep(0);
        });
    }

    /// <summary>
    /// Check xem tutorial Ä‘Ã£ hoÃ n thÃ nh chÆ°a
    /// </summary>
    public bool IsTutorialCompleted(string tutorialName)
    {
        if (string.IsNullOrEmpty(tutorialName))
        {
            return false;
        }

        string key = Const.TUTORIAL_PREFIX + tutorialName;
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    /// <summary>
    /// Force complete tutorial (cheat/testing)
    /// </summary>
    public void ForceCompleteTutorial(string tutorialName)
    {
        if (string.IsNullOrEmpty(tutorialName))
        {
            return;
        }

        string key = Const.TUTORIAL_PREFIX + tutorialName;
        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            return;
        }

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    public void CheckAndStartTutorial(int currentLevel, Action onTutorialCompleteCallback = null)
    {
        this.onTutorialCompleteCallback = onTutorialCompleteCallback;
        TutorialConfigSO targetConfig = null;
        if (targetConfig == null)
        {
            foreach (var cfg in tutorials)
            {
                if (cfg != null && cfg.tutorialLevel == currentLevel && !IsTutorialCompleted(cfg.tutorialName))
                {
                    targetConfig = cfg;
                    break;
                }
            }
        }

        if (targetConfig != null)
        {
            StartTutorial(targetConfig.tutorialName);
        }
        else
        {
            this.onTutorialCompleteCallback?.Invoke();
            this.onTutorialCompleteCallback = null;
        }
    }

    /// <summary>
    /// Láº¥y tÃªn cá»§a tutorial Ä‘ang cháº¡y hiá»‡n táº¡i. Tráº£ vá» null náº¿u khÃ´ng cÃ³.
    /// </summary>
    public string GetActiveTutorialName()
    {
        if (isTutorialActive && currentConfig != null)
        {
            return currentConfig.tutorialName;
        }
        return null;
    }

    public bool TryAdoptBoosterDescriptionBg(GameObject boosterDescriptionBg)
    {
        if (!isTutorialActive || boosterDescriptionBg == null)
        {
            return false;
        }

        Transform boosterTransform = boosterDescriptionBg.transform;
        if (boosterTransform == null)
        {
            return false;
        }

        if (adoptedBoosterDescriptionBgTransform != null && adoptedBoosterDescriptionBgTransform != boosterTransform)
        {
            RestoreBoosterDescriptionBgParent();
        }

        if (adoptedBoosterDescriptionBgTransform == null)
        {
            adoptedBoosterDescriptionBgTransform = boosterTransform;
            adoptedBoosterDescriptionBgOriginalParent = boosterTransform.parent;
            adoptedBoosterDescriptionBgOriginalSiblingIndex = boosterTransform.GetSiblingIndex();
        }

        boosterTransform.SetParent(transform, true);
        boosterTransform.SetAsLastSibling();
        suppressInstructionPanelWhileBoosterDescriptionActive = true;

        if (instructionPanel != null)
        {
            instructionPanel.SetActive(false);
        }

        return true;
    }

    public void RestoreBoosterDescriptionBgParent()
    {
        if (adoptedBoosterDescriptionBgTransform == null)
        {
            return;
        }

        if (adoptedBoosterDescriptionBgOriginalParent != null)
        {
            int safeSiblingIndex = Mathf.Clamp(adoptedBoosterDescriptionBgOriginalSiblingIndex, 0, adoptedBoosterDescriptionBgOriginalParent.childCount);
            adoptedBoosterDescriptionBgTransform.SetParent(adoptedBoosterDescriptionBgOriginalParent, true);
            adoptedBoosterDescriptionBgTransform.SetSiblingIndex(safeSiblingIndex);
        }

        adoptedBoosterDescriptionBgTransform = null;
        adoptedBoosterDescriptionBgOriginalParent = null;
        adoptedBoosterDescriptionBgOriginalSiblingIndex = -1;
        suppressInstructionPanelWhileBoosterDescriptionActive = false;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // STEP MANAGEMENT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void ShowStep(int index)
    {
        if (index >= currentConfig.steps.Count)
        {
            CompleteTutorial();
            return;
        }

        SetTutorialPanelVisible(true);
        SetConveyorsPaused(true);

        TutorialStep step = currentConfig.GetStep(index);
        if (step == null)
        {
            ;
            NextStep();
            return;
        }

        dimOverlay.gameObject.SetActive(step.enableOverlay);

        // âœ… Update instruction text - GÃ¡n text vÃ  set panel active atomically Ä‘á»ƒ trÃ¡nh flicker
        if (instructionPanel != null)
        {
            if (suppressInstructionPanelWhileBoosterDescriptionActive)
            {
                instructionPanel.SetActive(false);
            }
            else
            {
            // TRÆ¯á»šC: GÃ¡n text vÃ  kiá»ƒm tra náº¿u cÃ³ ná»™i dung
            if (!string.IsNullOrWhiteSpace(step.description))
            {
                instructionText.text = step.description;
                instructionPanel.SetActive(true); // Activate AFTER text is set
            }
            else
            {
                // KhÃ´ng cÃ³ text -> áº¨n panel luÃ´n
                instructionPanel.SetActive(false);
            }
            }
        }


        ShowClickStep(step, index);


    }

    /// <summary>
    /// Hiá»ƒn thá»‹ Click type step - logic cÅ©
    /// </summary>
    private void ShowClickStep(TutorialStep step, int index)
    {
        isDragStep = false;

        // Resolve target object from current active scene hierarchy.
        currentTarget = ResolveTargetByName(step.targetObjectName);
        if (currentTarget == null)
        {
            ;
            NextStep();
            return;
        }

        // âœ… Detect target lÃ  UI hay GameObject
        isTargetUI = IsTargetUI(currentTarget);

        if (step.requireClick && isTargetUI)
        {
            currentTarget = ResolveClickableUITarget(currentTarget);
            isTargetUI = IsTargetUI(currentTarget);
        }

        LogTutorial($"[Tutorial] Target '{currentTarget.name}' is UI: {isTargetUI}");

        // KhÃ³a toÃ n bá»™ UI khÃ¡c khi tutorial chÆ°a xong.
        RefreshTutorialUIBlockingForCurrentTarget();

        // âœ… Instantiate arrow tÆ°Æ¡ng á»©ng dá»±a vÃ o target type
        InstantiateArrow();

        // Reparent/Highlight target
        ReparentTarget(currentTarget);

        // Position arrow
        if (currentArrowInstance != null)
        {
            PositionArrow(step);
        }

        // Setup click listener náº¿u cáº§n
        if (step.requireClick)
        {
            SetupClickListener(currentTarget, step);
            RefreshBoosterTargetButtonState();
        }

    }


    private void NextStep()
    {
        // âœ… RESTORE TARGET Vá»€ Vá»Š TRÃ CÅ¨ trÆ°á»›c khi next
        CancelBoosterRefreshTweens();
        RestoreTarget();

        // âœ… Reset drag state
        isDragStep = false;
        isDragging = false;

        currentStepIndex++;
        ShowStep(currentStepIndex);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // REPARENTING LOGIC
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Kiá»ƒm tra target cÃ³ pháº£i UI khÃ´ng
    /// </summary>
    private bool IsTargetUI(GameObject target)
    {
        return target.GetComponent<RectTransform>() != null;
    }

    /// <summary>
    /// âœ… Instantiate arrow dá»±a vÃ o target type
    /// </summary>
    private void InstantiateArrow()
    {
        // Cleanup arrow cũ nếu có
        if (currentArrowInstance != null)
        {
            Destroy(currentArrowInstance);
            currentArrowInstance = null;
            currentArrowVisualCache = null;
        }

        if (isTargetUI)
        {
            // Instantiate UI arrow và set parent là tutorial panel
            if (arrowImageUI != null && tutorialPanel != null)
            {
                currentArrowInstance = Instantiate(arrowImageUI, tutorialPanel.transform);
                LogTutorial($"[Tutorial] Instantiated UI arrow as child of tutorial panel");
            }
        }
        else
        {
            // Instantiate GameObject arrow cùng cấp với target đang xử lý
            if (arrowImage != null && currentTarget != null)
            {
                Transform targetParent = currentTarget.transform.parent;
                currentArrowInstance = Instantiate(arrowImage);
                if (targetParent != null)
                {
                    // Keep world transform to avoid inheriting non-1 parent scale.
                    currentArrowInstance.transform.SetParent(targetParent, true);
                }

                // Set layer giá»‘ng tutorial layer Ä‘á»ƒ camera soi Ä‘Æ°á»£c
                int tutorialLayer = LayerMaskToLayer(tutorialCamLayer);
                if (tutorialLayer >= 0)
                {
                    SetLayerRecursively(currentArrowInstance, tutorialLayer);
                }

                LogTutorial($"[Tutorial] Instantiated GameObject arrow as sibling of target");
            }
        }

        if (currentArrowInstance != null)
        {
            currentArrowVisualCache = BuildArrowVisualCache(currentArrowInstance);

            // LuÃ´n set alpha 0 ngay khi spawn Ä‘á»ƒ trÃ¡nh flash 1 frame trÆ°á»›c khi fade loop báº¯t Ä‘áº§u.
            currentArrowInstance.SetActive(true);
            SetArrowInstanceAlpha(currentArrowInstance, 0f);
            PrimeArrowAnimationComponents(currentArrowInstance);
        }
    }

    private ArrowVisualCache BuildArrowVisualCache(GameObject arrowGO)
    {
        if (arrowGO == null)
        {
            return null;
        }

        ArrowVisualCache cache = new ArrowVisualCache
        {
            canvasGroup = arrowGO.GetComponent<CanvasGroup>(),
            graphics = arrowGO.GetComponentsInChildren<Graphic>(true),
            skeletonGraphics = arrowGO.GetComponentsInChildren<SkeletonGraphic>(true),
            spriteRenderers = arrowGO.GetComponentsInChildren<SpriteRenderer>(true),
            skeletonAnimations = arrowGO.GetComponentsInChildren<SkeletonAnimation>(true)
        };

        return cache;
    }

    private void PrimeArrowAnimationComponents(GameObject arrowGO)
    {
        if (arrowGO == null)
        {
            return;
        }

        ArrowVisualCache cache = (arrowGO == currentArrowInstance)
            ? currentArrowVisualCache
            : BuildArrowVisualCache(arrowGO);
        if (cache == null)
        {
            return;
        }

        if (cache.skeletonGraphics != null)
        {
            for (int i = 0; i < cache.skeletonGraphics.Length; i++)
            {
                SkeletonGraphic skeletonGraphic = cache.skeletonGraphics[i];
                if (skeletonGraphic == null)
                {
                    continue;
                }

                skeletonGraphic.freeze = true;
            }
        }

        if (cache.skeletonAnimations != null)
        {
            for (int i = 0; i < cache.skeletonAnimations.Length; i++)
            {
                SkeletonAnimation skeletonAnimation = cache.skeletonAnimations[i];
                if (skeletonAnimation == null)
                {
                    continue;
                }

                skeletonAnimation.timeScale = 0f;
            }
        }
    }

    /// <summary>
    /// âœ… Highlight target: UI hoáº·c GameObject
    /// </summary>
    private void ReparentTarget(GameObject target)
    {
        if (isTargetUI)
        {
            // Xá»­ lÃ½ UI target
            ReparentUITarget(target);
        }
        else
        {
            // Xá»­ lÃ½ GameObject target
            ReparentGameObjectTarget(target);
        }
    }

    /// <summary>
    /// Highlight UI target - logic cÅ©
    /// </summary>
    private void ReparentUITarget(GameObject target)
    {
        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (targetRect == null)
        {
            ;
            return;
        }

        originalTargetParent = target.transform.parent;
        originalTargetSiblingIndex = target.transform.GetSiblingIndex();

        // KhÃ´ng reparent target UI Ä‘á»ƒ trÃ¡nh phÃ¡ layout gá»‘c (Ä‘áº·c biá»‡t lÃ  bottom UI dÃ¹ng layout group).
        // DÃ¹ng Canvas override sorting táº¡m thá»i Ä‘á»ƒ target ná»•i lÃªn trÃªn dim overlay.
        uiTargetCanvas = target.GetComponent<Canvas>();
        addedUiTargetCanvas = uiTargetCanvas == null;
        if (addedUiTargetCanvas)
        {
            uiTargetCanvas = target.AddComponent<Canvas>();
        }

        if (uiTargetCanvas != null)
        {
            originalUiTargetCanvasOverrideSorting = uiTargetCanvas.overrideSorting;
            originalUiTargetCanvasSortingOrder = uiTargetCanvas.sortingOrder;
            originalUiTargetCanvasSortingLayerId = uiTargetCanvas.sortingLayerID;

            Canvas tutorialPanelCanvas = tutorialPanel != null ? tutorialPanel.GetComponentInParent<Canvas>() : null;
            int tutorialSortingLayerId = tutorialPanelCanvas != null
                ? tutorialPanelCanvas.sortingLayerID
                : uiTargetCanvas.sortingLayerID;
            int tutorialSortingOrder = tutorialPanelCanvas != null
                ? tutorialPanelCanvas.sortingOrder
                : 0;

            uiTargetCanvas.overrideSorting = true;
            uiTargetCanvas.sortingLayerID = tutorialSortingLayerId;
            uiTargetCanvas.sortingOrder = tutorialSortingOrder + 1;
        }

        // âœ… Add GraphicRaycaster to target so it remains clickable
        uiTargetGraphicRaycaster = target.GetComponent<GraphicRaycaster>();
        addedUiTargetGraphicRaycaster = uiTargetGraphicRaycaster == null;
        if (addedUiTargetGraphicRaycaster)
        {
            uiTargetGraphicRaycaster = target.AddComponent<GraphicRaycaster>();
        }

        if (uiTargetGraphicRaycaster != null)
        {
            originalUiTargetGraphicRaycasterEnabled = uiTargetGraphicRaycaster.enabled;
            uiTargetGraphicRaycaster.enabled = true;
        }

        LogTutorial($"[Tutorial] Highlighted UI '{target.name}' without reparenting");
    }

    /// <summary>
    /// Highlight GameObject target - Ä‘á»•i layer sang tutorial layer
    /// KHÃ”NG reparent, chá»‰ Ä‘á»•i layer
    /// </summary>
    private void ReparentGameObjectTarget(GameObject target)
    {
        // LÆ°u layer gá»‘c
        originalTargetLayer = target.layer;

        // Äá»•i sang layer mÃ  tutorial camera soi
        int tutorialLayer = LayerMaskToLayer(tutorialCamLayer);
        if (tutorialLayer >= 0)
        {
            SetLayerRecursively(target, tutorialLayer);
            LogTutorial($"[Tutorial] Changed GameObject '{target.name}' from layer {originalTargetLayer} to {tutorialLayer}");
        }
        else
        {
            LogTutorialWarning($"[Tutorial] Tutorial layer not set properly!");
        }

        // âœ… KHÃ”NG reparent - giá»¯ nguyÃªn hierarchy cá»§a GameObject
    }

    /// <summary>
    /// Convert LayerMask to layer number
    /// </summary>
    private int LayerMaskToLayer(LayerMask layerMask)
    {
        int mask = layerMask.value;
        if (mask == 0) return -1;

        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Set layer cho object vÃ  táº¥t cáº£ children
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>
    /// âœ… Restore target vá» tráº¡ng thÃ¡i ban Ä‘áº§u
    /// </summary>
    private void RestoreTarget()
    {
        if (currentTarget == null) return;

        // âœ… áº¨n arrow
        HideArrow();

        if (isTargetUI)
        {
            // Restore UI target
            RestoreUITarget();
        }
        else
        {
            // Restore GameObject target
            RestoreGameObjectTarget();
        }

        LogTutorial($"[Tutorial] Restored '{currentTarget.name}' to normal state");

        RestoreLockedUIInteractions();

        // Clear references
        currentTarget = null;
        originalTargetLayer = -1;
        isTargetUI = false;
    }

    /// <summary>
    /// Restore UI target
    /// </summary>
    private void RestoreUITarget()
    {
        RectTransform targetRect = currentTarget.GetComponent<RectTransform>();
        if (targetRect != null)
        {
            // Kill animations
            targetRect.DOKill();
        }

        if (uiTargetGraphicRaycaster != null)
        {
            if (addedUiTargetGraphicRaycaster)
            {
                Destroy(uiTargetGraphicRaycaster);
            }
            else
            {
                uiTargetGraphicRaycaster.enabled = originalUiTargetGraphicRaycasterEnabled;
            }
        }

        if (uiTargetCanvas != null)
        {
            if (addedUiTargetCanvas)
            {
                Destroy(uiTargetCanvas);
            }
            else
            {
                uiTargetCanvas.overrideSorting = originalUiTargetCanvasOverrideSorting;
                uiTargetCanvas.sortingOrder = originalUiTargetCanvasSortingOrder;
                uiTargetCanvas.sortingLayerID = originalUiTargetCanvasSortingLayerId;
            }
        }

        if (originalTargetParent != null)
        {
            currentTarget.transform.SetParent(originalTargetParent, true);
            currentTarget.transform.SetSiblingIndex(originalTargetSiblingIndex);
        }

        originalTargetParent = null;
        originalTargetSiblingIndex = 0;
        uiTargetCanvas = null;
        addedUiTargetCanvas = false;
        originalUiTargetCanvasOverrideSorting = false;
        originalUiTargetCanvasSortingOrder = 0;
        originalUiTargetCanvasSortingLayerId = 0;
        uiTargetGraphicRaycaster = null;
        addedUiTargetGraphicRaycaster = false;
        originalUiTargetGraphicRaycasterEnabled = false;
    }

    /// <summary>
    /// Restore GameObject target - restore layer gá»‘c
    /// </summary>
    private void RestoreGameObjectTarget()
    {
        if (originalTargetLayer >= 0)
        {
            SetLayerRecursively(currentTarget, originalTargetLayer);
            LogTutorial($"[Tutorial] Restored GameObject '{currentTarget.name}' to original layer {originalTargetLayer}");
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // UI HELPERS - ARROW VISIBILITY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Hiá»‡n arrow vá»›i alpha tá»« min Ä‘áº¿n max
    /// </summary>
    private void ShowArrow(float alpha = 1f)
    {
        if (arrowImage == null) return;
        arrowImage.SetActive(true);

        if (arrowCanvasGroup != null)
        {
            arrowCanvasGroup.alpha = alpha;
        }

        var spine = arrowImage.GetComponent<Spine.Unity.SkeletonGraphic>();
        if (spine != null)
        {
            spine.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    /// <summary>
    /// áº¨n arrow ngay láº­p tá»©c - destroy arrow instance
    /// </summary>
    private void HideArrow()
    {
        // Kill animation tween náº¿u Ä‘ang cháº¡y
        if (arrowTween != null)
        {
            arrowTween.Kill();
            arrowTween = null;
        }

        // Destroy arrow instance (UI hoặc GameObject)
        if (currentArrowInstance != null)
        {
            Destroy(currentArrowInstance);
            currentArrowInstance = null;
            currentArrowVisualCache = null;
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // UI HELPERS - ARROW ANIMATION
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Thiáº¿t láº­p vá»‹ trÃ­ vÃ  animation cá»§a arrow cho Click type
    /// Detect UI vs GameObject vÃ  dÃ¹ng arrow tÆ°Æ¡ng á»©ng
    /// </summary>
    private void PositionArrow(TutorialStep step)
    {
        if (currentTarget == null) return;

        if (isTargetUI)
        {
            PositionUIArrow(step);
        }
        else
        {
            PositionGameObjectArrow(step);
        }
    }

    /// <summary>
    /// Position arrow cho UI target
    /// </summary>
    private void PositionUIArrow(TutorialStep step)
    {
        if (currentArrowInstance == null || currentTarget == null) return;

        // Setup References
        RectTransform targetRect = currentTarget.GetComponent<RectTransform>();
        RectTransform arrowRect = currentArrowInstance.GetComponent<RectTransform>();

        if (targetRect == null || arrowRect == null) return;

        // Setup Canvas Override Ä‘á»ƒ arrow luÃ´n hiá»ƒn thá»‹ trÃªn cÃ¹ng
        SetupArrowCanvas(currentArrowInstance);

        // âœ… Set rotation
        currentArrowInstance.transform.localRotation = Quaternion.Euler(0, 0, step.arrowRotation);

        // âœ… TÃ­nh toÃ¡n vá»‹ trÃ­ Ä‘Ã­ch theo world offset
        Vector3 targetWorldPos = GetArrowWorldPosition(targetRect.position, step.arrowOffset);

        // âœ… Set world position trá»±c tiáº¿p, khÃ´ng convert vá» local
        arrowRect.position = targetWorldPos;

        // âœ… Show arrow vÃ  cháº¡y spine animation loop
        currentArrowInstance.SetActive(true);
        CreateClickArrowAnimationLoop(step, currentArrowInstance);
    }

    /// <summary>
    /// Position arrow cho GameObject target (trong world space)
    /// </summary>
    private void PositionGameObjectArrow(TutorialStep step)
    {
        if (currentArrowInstance == null || currentTarget == null) return;

        // âœ… Position arrow = tá»a Ä‘á»™ GameObject hiá»‡n táº¡i + offset world
        Vector3 targetWorldPos = currentTarget.transform.position + step.arrowOffset;
        currentArrowInstance.transform.position = targetWorldPos;

        // Giá»¯ arrow song song camera dÃ¹ parent khÃ´ng pháº£i camera.
        currentArrowInstance.transform.rotation = GetArrowWorldRotation(step.arrowRotation);

        // âœ… Set sorting order Ä‘á»ƒ arrow hiá»ƒn thá»‹ trÃªn target
        SpriteRenderer arrowSR = currentArrowInstance.GetComponent<SpriteRenderer>();
        if (arrowSR != null)
        {
            SpriteRenderer targetSR = currentTarget.GetComponent<SpriteRenderer>();
            if (targetSR != null)
            {
                arrowSR.sortingLayerID = targetSR.sortingLayerID;
                arrowSR.sortingOrder = targetSR.sortingOrder + 1; // TrÃªn target
            }
        }

        // âœ… Cháº¡y animation náº¿u cÃ³ Spine
        var spineAnim = currentArrowInstance.GetComponent<Spine.Unity.SkeletonAnimation>();
        if (spineAnim != null)
        {
            CreateGameObjectArrowAnimationLoop(step, currentArrowInstance, spineAnim);
        }

        currentArrowInstance.SetActive(true);
        LogTutorial($"[Tutorial] Positioned GameObject arrow at {targetWorldPos}");
    }

    private static Vector3 GetArrowWorldPosition(Vector3 targetWorldPosition, Vector3 worldOffset)
    {
        return targetWorldPosition + worldOffset;
    }

    private Quaternion GetArrowWorldRotation(float zRotation)
    {
        Camera referenceCamera = tutorialCamera != null ? tutorialCamera : mainCamera;
        if (referenceCamera != null)
        {
            return referenceCamera.transform.rotation * Quaternion.Euler(0f, 0f, zRotation);
        }

        return Quaternion.Euler(0f, 0f, zRotation);
    }

    /// <summary>
    /// Setup Canvas component cho arrow Ä‘á»ƒ hiá»ƒn thá»‹ trÃªn cÃ¹ng
    /// </summary>
    private void SetupArrowCanvas(GameObject arrowGO)
    {
        arrowGO.SetActive(true);

        Canvas arrowCanvas = arrowGO.GetComponent<Canvas>();
        if (arrowCanvas == null) arrowCanvas = arrowGO.AddComponent<Canvas>();
        arrowCanvas.overrideSorting = true;
        arrowCanvas.sortingOrder = 999;
    }

    /// <summary>
    /// Táº¡o animation loop cho Click type UI arrow - chá»‰ cháº¡y spine animation táº¡i chá»—
    /// </summary>
    private void CreateClickArrowAnimationLoop(TutorialStep step, GameObject arrowGO)
    {
        if (arrowTween != null) arrowTween.Kill();

        var animator = arrowGO.GetComponent<Spine.Unity.SkeletonGraphic>();
        if (animator == null) return;

        float handAnimLength = 0.5f;
        string animStateName = "Click";

        Sequence seq = DOTween.Sequence();
        float fadeDelay = Mathf.Max(0f, arrowFadeDelay);

        // Má»—i vÃ²ng loop: fade tá»« 0 -> 1 trÆ°á»›c khi cháº¡y animation.
        seq.AppendCallback(() => SetArrowInstanceAlpha(arrowGO, 0f));
        seq.AppendInterval(fadeDelay);
        seq.Append(DOVirtual.Float(0f, 1f, arrowFadeDuration, alpha => SetArrowInstanceAlpha(arrowGO, alpha)));

        // Cháº¡y Spine Animation
        seq.AppendCallback(() =>
        {
            if (animator != null)
            {
                animator.freeze = false;
                animator.AnimationState.SetAnimation(0, animStateName, false);
            }
        });

        // Chá» Animation cháº¡y xong
        seq.AppendInterval(handAnimLength);

        // Káº¿t thÃºc animation thÃ¬ fade vá» 0, vÃ²ng sau sáº½ fade lÃªn láº¡i.
        seq.AppendInterval(fadeDelay);
        seq.Append(DOVirtual.Float(1f, 0f, arrowFadeDuration, alpha => SetArrowInstanceAlpha(arrowGO, alpha)));

        // Delay trÆ°á»›c khi loop láº¡i
        seq.AppendInterval(step.arrowLoopDelay);

        // Cáº¤U HÃŒNH LOOP vÃ´ háº¡n
        seq.SetLoops(-1, LoopType.Restart);
        seq.SetUpdate(true); // Cháº¡y xuyÃªn TimeScale pause

        arrowTween = seq;
    }

    /// <summary>
    /// Táº¡o animation loop cho GameObject arrow (Spine.Unity.SkeletonAnimation)
    /// </summary>
    private void CreateGameObjectArrowAnimationLoop(TutorialStep step, GameObject arrowGO, Spine.Unity.SkeletonAnimation animator)
    {
        if (arrowTween != null) arrowTween.Kill();
        if (animator == null || arrowGO == null) return;

        float handAnimLength = 0.5f;
        string animStateName = "Click2"; // Thay Ä‘á»•i thÃ nh Click2

        Sequence seq = DOTween.Sequence();
        float fadeDelay = Mathf.Max(0f, arrowFadeDelay);

        // Má»—i vÃ²ng loop: fade tá»« 0 -> 1 trÆ°á»›c khi cháº¡y animation.
        seq.AppendCallback(() => SetArrowInstanceAlpha(arrowGO, 0f));
        seq.AppendInterval(fadeDelay);
        seq.Append(DOVirtual.Float(0f, 1f, arrowFadeDuration, alpha => SetArrowInstanceAlpha(arrowGO, alpha)));

        // Cháº¡y Spine Animation
        seq.AppendCallback(() =>
        {
            if (animator != null)
            {
                animator.timeScale = 1f;
                animator.AnimationState.SetAnimation(0, animStateName, false);
            }
        });

        // Chá» Animation cháº¡y xong
        seq.AppendInterval(handAnimLength);

        // Káº¿t thÃºc animation thÃ¬ fade vá» 0, vÃ²ng sau sáº½ fade lÃªn láº¡i.
        seq.AppendInterval(fadeDelay);
        seq.Append(DOVirtual.Float(1f, 0f, arrowFadeDuration, alpha => SetArrowInstanceAlpha(arrowGO, alpha)));

        // Delay trÆ°á»›c khi loop láº¡i
        seq.AppendInterval(step.arrowLoopDelay);

        // Cáº¤U HÃŒNH LOOP vÃ´ háº¡n
        seq.SetLoops(-1, LoopType.Restart);
        seq.SetUpdate(true);

        arrowTween = seq;
    }

    private void SetArrowInstanceAlpha(GameObject arrowGO, float alpha)
    {
        if (arrowGO == null) return;

        float clampedAlpha = Mathf.Clamp01(alpha);

        ArrowVisualCache cache = (arrowGO == currentArrowInstance)
            ? currentArrowVisualCache
            : BuildArrowVisualCache(arrowGO);
        if (cache == null)
        {
            return;
        }

        if (cache.canvasGroup != null)
        {
            cache.canvasGroup.alpha = clampedAlpha;
        }

        if (cache.graphics != null)
        {
            for (int i = 0; i < cache.graphics.Length; i++)
            {
                Graphic g = cache.graphics[i];
                if (g == null) continue;
                Color c = g.color;
                c.a = clampedAlpha;
                g.color = c;
            }
        }

        if (cache.skeletonGraphics != null)
        {
            for (int i = 0; i < cache.skeletonGraphics.Length; i++)
            {
                SkeletonGraphic sg = cache.skeletonGraphics[i];
                if (sg == null) continue;
                Color c = sg.color;
                c.a = clampedAlpha;
                sg.color = c;
            }
        }

        if (cache.spriteRenderers != null)
        {
            for (int i = 0; i < cache.spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = cache.spriteRenderers[i];
                if (sr == null) continue;
                Color c = sr.color;
                c.a = clampedAlpha;
                sr.color = c;
            }
        }

        if (cache.skeletonAnimations != null)
        {
            for (int i = 0; i < cache.skeletonAnimations.Length; i++)
            {
                SkeletonAnimation sa = cache.skeletonAnimations[i];
                if (sa?.Skeleton == null) continue;
                sa.Skeleton.A = clampedAlpha;
            }
        }
    }










    private void SetupClickListener(GameObject target, TutorialStep step)
    {
        Button btn = target.GetComponent<Button>();
        if (btn == null)
        {
            btn = target.GetComponentInParent<Button>();
        }
        if (btn == null)
        {
            btn = target.GetComponentInChildren<Button>();
        }
        if (btn == null)
        {
            return;
        }

        ClearCurrentTargetButtonListener();

        currentTargetButton = btn;
        currentTargetButtonClickAction = () => OnTargetClicked(step);
        btn.onClick.AddListener(currentTargetButtonClickAction);
    }

    /// <summary>
    /// Xá»­ lÃ½ khi user click vÃ o target button
    /// </summary>
    private void OnTargetClicked(TutorialStep step)
    {
        CancelPendingNextStepTween();
        CancelBoosterRefreshTweens();
        ClearCurrentTargetButtonListener();

        // âœ… áº¨n arrow ngay láº­p tá»©c khi click
        HideArrow();

        // áº¨n text hiá»‡n táº¡i ngay Ä‘á»ƒ trÃ¡nh tháº¥y text cá»§a step trÆ°á»›c trong lÃºc chá» delay.
        PrepareInstructionForNextStep();
        AdvanceStepWithOptionalDelay(step);
    }

    /// <summary>
    /// ÄÆ°á»£c gá»i tá»« InputManager khi click trÃºng má»™t GameObject
    /// </summary>
    public void NotifyGameObjectClicked(GameObject clickedObj)
    {
        // ThÃªm check currentTarget == null Ä‘á»ƒ an toÃ n
        if (!isTutorialActive || currentConfig == null || currentTarget == null) return;

        // âœ… Cáº­p nháº­t: TrÃºng target HOáº¶C trÃºng báº¥t ká»³ tháº±ng con nÃ o cá»§a target Ä‘á»u há»£p lá»‡
        if (clickedObj == currentTarget || clickedObj.transform.IsChildOf(currentTarget.transform))
        {
            TutorialStep step = currentConfig.GetStep(currentStepIndex);
            if (step != null && step.requireClick)
            {
                ClearCurrentTargetButtonListener();
                CancelBoosterRefreshTweens();
                HideArrow();
                PrepareInstructionForNextStep();
                AdvanceStepWithOptionalDelay(step);
            }
        }
    }

    private void AdvanceStepWithOptionalDelay(TutorialStep step)
    {
        if (TryBeginMagicStoneProgressWait(step))
        {
            return;
        }

        float nextDelay = Mathf.Max(0f, step != null ? step.nextStepDelay : 0f);
        BeginTimedStepAdvance(nextDelay);
    }

    private void BeginTimedStepAdvance(float nextDelay)
    {
        if (nextDelay <= 0f)
        {
            isStepTransitionDelayActive = false;
            SetTutorialPanelVisible(true);
            SetConveyorsPaused(true);
            NextStep();
            return;
        }

        // Trong lÃºc chá» delay thÃ¬ cho route cháº¡y bÃ¬nh thÆ°á»ng, Ä‘áº¿n step má»›i sáº½ pause láº¡i.
        isStepTransitionDelayActive = true;
        SetTutorialPanelVisible(false);
        SetConveyorsPaused(false);
        pendingNextStepTween = DOVirtual.DelayedCall(nextDelay, () =>
        {
            pendingNextStepTween = null;
            isStepTransitionDelayActive = false;

            if (!isTutorialActive)
            {
                return;
            }

            SetTutorialPanelVisible(true);
            SetConveyorsPaused(true);
            NextStep();
        });
    }

    private bool TryBeginMagicStoneProgressWait(TutorialStep step)
    {
        if (!isTutorialActive || step == null || !step.waitForMagicStoneProgress)
        {
            return false;
        }

        int requiredCount = Mathf.Max(1, step.requiredMagicStoneCount);
        int collectedCount = BaseShooter.GetCollectedMagicStoneForCurrentLevel();
        if (collectedCount >= requiredCount)
        {
            return false;
        }

        isWaitingForMagicStoneProgress = true;
        waitingMagicStoneRequiredCount = requiredCount;
        waitingMagicStonePostDelay = Mathf.Max(0f, step.nextStepDelay);
        isStepTransitionDelayActive = true;

        SetTutorialPanelVisible(false);
        SetConveyorsPaused(false);
        return true;
    }

    private void OnMagicStoneProgressChanged(object data)
    {
        if (!isTutorialActive || !isWaitingForMagicStoneProgress)
        {
            return;
        }

        int collectedCount = BaseShooter.GetCollectedMagicStoneForCurrentLevel();
        if (data is int intValue)
        {
            collectedCount = intValue;
        }
        else if (data is float floatValue)
        {
            collectedCount = Mathf.RoundToInt(floatValue);
        }

        if (collectedCount < Mathf.Max(1, waitingMagicStoneRequiredCount))
        {
            return;
        }

        float postDelay = Mathf.Max(0f, waitingMagicStonePostDelay);
        CancelMagicStoneProgressWait();
        BeginTimedStepAdvance(postDelay);
    }

    private void CancelMagicStoneProgressWait()
    {
        isWaitingForMagicStoneProgress = false;
        waitingMagicStoneRequiredCount = 0;
        waitingMagicStonePostDelay = 0f;
    }

    private GameObject ResolveClickableUITarget(GameObject rawTarget)
    {
        if (rawTarget == null)
        {
            return null;
        }

        Button button = rawTarget.GetComponent<Button>();
        if (button == null)
        {
            button = rawTarget.GetComponentInParent<Button>();
        }

        return button != null ? button.gameObject : rawTarget;
    }

    private void RefreshBoosterTargetButtonState()
    {
        if (!isTutorialActive || currentTarget == null)
        {
            return;
        }

        BoosterButtonPrefab boosterButton = currentTarget.GetComponent<BoosterButtonPrefab>();
        if (boosterButton == null)
        {
            boosterButton = currentTarget.GetComponentInParent<BoosterButtonPrefab>();
        }

        if (boosterButton == null)
        {
            return;
        }

        boosterButton.Refresh();
        GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);

        CancelBoosterRefreshTweens();

        boosterRefreshTweenShort = DOVirtual.DelayedCall(0.12f, () =>
        {
            if (!isTutorialActive || currentTarget == null || boosterButton == null)
            {
                return;
            }

            boosterButton.Refresh();
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
        }).SetUpdate(true);

        boosterRefreshTweenLong = DOVirtual.DelayedCall(0.35f, () =>
        {
            if (!isTutorialActive || currentTarget == null || boosterButton == null)
            {
                return;
            }

            boosterButton.Refresh();
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
        }).SetUpdate(true);
    }

    private void ClearCurrentTargetButtonListener()
    {
        if (currentTargetButton != null && currentTargetButtonClickAction != null)
        {
            currentTargetButton.onClick.RemoveListener(currentTargetButtonClickAction);
        }

        currentTargetButton = null;
        currentTargetButtonClickAction = null;
    }

    private void PrepareInstructionForNextStep()
    {
        if (instructionText != null)
        {
            instructionText.text = string.Empty;
        }

        if (instructionPanel != null)
        {
            instructionPanel.SetActive(false);
        }
    }

    private void CancelPendingNextStepTween()
    {
        isStepTransitionDelayActive = false;

        if (pendingNextStepTween == null)
        {
            return;
        }

        if (pendingNextStepTween.IsActive())
        {
            pendingNextStepTween.Kill();
        }

        pendingNextStepTween = null;

        if (isTutorialActive)
        {
            SetTutorialPanelVisible(true);
        }
    }

    private void SetTutorialPanelVisible(bool visible)
    {
        SetTutorialPanelVisible(visible, false);
    }

    private void SetTutorialPanelVisible(bool visible, bool forceSetActive)
    {
        if (tutorialPanel == null)
        {
            return;
        }

        if (visible)
        {
            if (forceSetActive || !tutorialPanel.activeSelf)
            {
                tutorialPanel.SetActive(true);
            }

            if (tutorialCanvasGroup != null)
            {
                tutorialCanvasGroup.DOKill();
                tutorialCanvasGroup.alpha = 1f;
                tutorialCanvasGroup.interactable = true;
                tutorialCanvasGroup.blocksRaycasts = true;
            }

            return;
        }

        if (tutorialCanvasGroup != null && tutorialPanel.activeSelf)
        {
            tutorialCanvasGroup.DOKill();
            tutorialCanvasGroup.alpha = 0f;
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (tutorialPanel.activeSelf)
        {
            tutorialPanel.SetActive(false);
        }
    }


    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // TUTORIAL COMPLETION
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void CompleteTutorial()
    {
        CancelPendingNextStepTween();
        CancelMagicStoneProgressWait();
        CancelBoosterRefreshTweens();
        isTutorialActive = false;
        isStepTransitionDelayActive = false;
        RestoreBoosterDescriptionBgParent();

        // Restore target náº¿u cÃ²n
        RestoreTarget();
        RestoreLockedUIInteractions();

        // âœ… Save completion state via PlayerData using tutorialName
        if (currentConfig != null && !string.IsNullOrEmpty(currentConfig.tutorialName))
        {
            string key = Const.TUTORIAL_PREFIX + currentConfig.tutorialName;
            if (PlayerPrefs.GetInt(key, 0) == 0)
            {
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
            }
            LogTutorial($"[Tutorial] Completed: {currentConfig.tutorialName}");
        }
        else
        {
            LogTutorialWarning($"[Tutorial] Cannot complete tutorial - config or tutorialName is null!");
        }

        // âœ… Check queue - náº¿u cÃ²n tutorial thÃ¬ cháº¡y tiáº¿p, khÃ´ng hide panel
        if (TryStartNextInQueue())
        {
            // CÃ³ tutorial tiáº¿p theo trong queue â†’ khÃ´ng hide panel
            return;
        }

        SetConveyorsPaused(false);

        SetTutorialPanelVisible(false);
        tutorialPanel.SetActive(false);

        onTutorialCompleteCallback?.Invoke();
        onTutorialCompleteCallback = null;
    }

    /// <summary>
    /// Thá»­ start tutorial tiáº¿p theo trong queue
    /// </summary>
    private bool TryStartNextInQueue()
    {
        while (tutorialQueue.Count > 0)
        {
            var tutorialName = tutorialQueue.Dequeue();

            // Skip náº¿u Ä‘Ã£ complete (cÃ³ thá»ƒ Ä‘Ã£ complete trong lÃºc queue)
            if (IsTutorialCompleted(tutorialName))
            {
                LogTutorial($"[Tutorial] Skipped queued tutorial (already completed): {tutorialName}");
                continue;
            }

            // âœ… Start tutorial tiáº¿p theo
            LogTutorial($"[Tutorial] Starting next from queue: {tutorialName} (remaining: {tutorialQueue.Count})");
            StartTutorialInternal(tutorialName);
            return true;
        }

        LogTutorial($"[Tutorial] Queue empty - all tutorials done");
        SetTutorialPanelVisible(false);
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
        return false;
    }

    private void SetConveyorsPaused(bool shouldPause)
    {
        conveyorRoutesBuffer.Clear();

        SplineController splineController = SplineController.Instance;
        if (splineController != null)
        {
            SplineRoute mainRoute = splineController.GetMainRoute();
            if (mainRoute != null)
            {
                conveyorRoutesBuffer.Add(mainRoute);
            }

            SplineRoute[] sideRoutes = splineController.GetSideRoutes();
            if (sideRoutes != null)
            {
                for (int i = 0; i < sideRoutes.Length; i++)
                {
                    SplineRoute sideRoute = sideRoutes[i];
                    if (sideRoute == null || conveyorRoutesBuffer.Contains(sideRoute))
                    {
                        continue;
                    }

                    conveyorRoutesBuffer.Add(sideRoute);
                }
            }
        }

        for (int i = 0; i < conveyorRoutesBuffer.Count; i++)
        {
            SplineRoute route = conveyorRoutesBuffer[i];
            if (route == null)
            {
                continue;
            }

            route.SetTutorialPaused(shouldPause);
        }
    }

    private void SkipTutorial()
    {
        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        CompleteTutorial();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEventHub.Instance?.RemoveListener(GameEventType.OnMagicStoneProgressChanged, OnMagicStoneProgressChanged);
        CancelPendingNextStepTween();
        CancelMagicStoneProgressWait();
        CancelBoosterRefreshTweens();
        ClearCurrentTargetButtonListener();
        RestoreBoosterDescriptionBgParent();
        RestoreLockedUIInteractions();
        SetConveyorsPaused(false);
    }

    private void RefreshTutorialUIBlockingForCurrentTarget()
    {
        RestoreLockedUIInteractions();

        if (!isTutorialActive)
        {
            return;
        }

        RefreshUIBlockCacheIfNeeded(forceRefresh: true);

        for (int i = 0; i < cachedSceneSelectables.Count; i++)
        {
            Selectable selectable = cachedSceneSelectables[i];
            if (selectable == null)
            {
                continue;
            }

            if (IsSelectableAllowedDuringTutorial(selectable))
            {
                continue;
            }

            lockedSelectableStates[selectable] = selectable.interactable;
            selectable.interactable = false;
        }

        if (lockNonTargetGraphicsRaycast)
        {
            for (int i = 0; i < cachedSceneGraphics.Count; i++)
            {
                Graphic graphic = cachedSceneGraphics[i];
                if (graphic == null || !graphic.raycastTarget)
                {
                    continue;
                }

                if (IsGraphicAllowedDuringTutorial(graphic))
                {
                    continue;
                }

                lockedGraphicRaycastStates[graphic] = true;
                graphic.raycastTarget = false;
            }
        }

        GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
    }

    private bool IsSelectableAllowedDuringTutorial(Selectable selectable)
    {
        if (selectable == null || currentTarget == null)
        {
            return false;
        }

        Transform selectableTransform = selectable.transform;
        Transform targetTransform = currentTarget.transform;

        return selectable.gameObject == currentTarget
               || selectableTransform.IsChildOf(targetTransform)
               || targetTransform.IsChildOf(selectableTransform);
    }

    private bool IsGraphicAllowedDuringTutorial(Graphic graphic)
    {
        if (graphic == null || currentTarget == null)
        {
            return false;
        }

        Transform graphicTransform = graphic.transform;
        Transform targetTransform = currentTarget.transform;

        return graphic.gameObject == currentTarget
               || graphicTransform.IsChildOf(targetTransform)
               || targetTransform.IsChildOf(graphicTransform);
    }

    private void RestoreLockedUIInteractions()
    {
        if (lockedSelectableStates.Count == 0 && lockedGraphicRaycastStates.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Selectable, bool> pair in lockedSelectableStates)
        {
            if (pair.Key != null)
            {
                pair.Key.interactable = pair.Value;
            }
        }

        lockedSelectableStates.Clear();

        foreach (KeyValuePair<Graphic, bool> pair in lockedGraphicRaycastStates)
        {
            if (pair.Key != null)
            {
                pair.Key.raycastTarget = pair.Value;
            }
        }

        lockedGraphicRaycastStates.Clear();

        GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
    }

    private GameObject ResolveTargetByName(string targetObjectName)
    {
        if (string.IsNullOrWhiteSpace(targetObjectName))
        {
            return null;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.handle != cachedTargetLookupSceneHandle)
        {
            tutorialTargetLookup.Clear();
            cachedTargetLookupSceneHandle = activeScene.handle;
        }

        if (tutorialTargetLookup.TryGetValue(targetObjectName, out GameObject cachedTarget) && cachedTarget != null)
        {
            return cachedTarget;
        }

        RebuildTutorialTargetLookup();

        if (tutorialTargetLookup.TryGetValue(targetObjectName, out cachedTarget) && cachedTarget != null)
        {
            return cachedTarget;
        }

        return null;
    }

    private void RebuildTutorialTargetLookup()
    {
        tutorialTargetLookup.Clear();

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return;
        }

        cachedTargetLookupSceneHandle = activeScene.handle;

        sceneRootsBuffer.Clear();
        activeScene.GetRootGameObjects(sceneRootsBuffer);

        for (int i = 0; i < sceneRootsBuffer.Count; i++)
        {
            GameObject root = sceneRootsBuffer[i];
            if (root == null)
            {
                continue;
            }

            transformTraversalBuffer.Clear();
            transformTraversalBuffer.Add(root.transform);

            for (int j = 0; j < transformTraversalBuffer.Count; j++)
            {
                Transform childTransform = transformTraversalBuffer[j];
                if (childTransform == null)
                {
                    continue;
                }

                GameObject candidate = childTransform.gameObject;
                if (candidate == null)
                {
                    continue;
                }

                string candidateName = candidate.name;
                if (!tutorialTargetLookup.ContainsKey(candidateName))
                {
                    tutorialTargetLookup.Add(candidateName, candidate);
                }

                for (int c = 0; c < childTransform.childCount; c++)
                {
                    Transform child = childTransform.GetChild(c);
                    if (child != null)
                    {
                        transformTraversalBuffer.Add(child);
                    }
                }
            }
        }
    }

    private void RefreshUIBlockCacheIfNeeded(bool forceRefresh = false)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            cachedSceneSelectables.Clear();
            cachedSceneGraphics.Clear();
            cachedUiSceneHandle = -1;
            nextUiBlockCacheRefreshAt = -1f;
            return;
        }
        bool sceneChanged = cachedUiSceneHandle != activeScene.handle;

        if (!forceRefresh && !sceneChanged)
        {
            return;
        }

        cachedUiSceneHandle = activeScene.handle;
        nextUiBlockCacheRefreshAt = Time.unscaledTime + Mathf.Max(0.1f, uiBlockCacheRefreshInterval);

        CollectSceneSelectables(cachedSceneSelectables);
        if (lockNonTargetGraphicsRaycast)
        {
            CollectSceneGraphics(cachedSceneGraphics);
        }
        else
        {
            cachedSceneGraphics.Clear();
        }
    }

    private void InvalidateTutorialRuntimeCaches()
    {
        tutorialTargetLookup.Clear();
        cachedSceneSelectables.Clear();
        cachedSceneGraphics.Clear();
        cachedUiSceneHandle = -1;
        cachedTargetLookupSceneHandle = -1;
        nextUiBlockCacheRefreshAt = -1f;
    }

    private void CancelBoosterRefreshTweens()
    {
        if (boosterRefreshTweenShort != null && boosterRefreshTweenShort.IsActive())
        {
            boosterRefreshTweenShort.Kill();
        }

        if (boosterRefreshTweenLong != null && boosterRefreshTweenLong.IsActive())
        {
            boosterRefreshTweenLong.Kill();
        }

        boosterRefreshTweenShort = null;
        boosterRefreshTweenLong = null;
    }

    private void LogTutorial(string message)
    {
        if (!enableVerboseRuntimeLogs)
        {
            return;
        }

        ;
    }

    private void LogTutorialWarning(string message)
    {
        if (!enableVerboseRuntimeLogs)
        {
            return;
        }

        ;
    }

    private void CollectSceneSelectables(List<Selectable> output)
    {
        output.Clear();

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return;
        }

        sceneRootsBuffer.Clear();
        activeScene.GetRootGameObjects(sceneRootsBuffer);

        for (int i = 0; i < sceneRootsBuffer.Count; i++)
        {
            GameObject root = sceneRootsBuffer[i];
            if (root == null)
            {
                continue;
            }

            transformTraversalBuffer.Clear();
            transformTraversalBuffer.Add(root.transform);

            for (int j = 0; j < transformTraversalBuffer.Count; j++)
            {
                Transform current = transformTraversalBuffer[j];
                if (current == null)
                {
                    continue;
                }

                selectableBuffer.Clear();
                current.GetComponents(selectableBuffer);
                for (int k = 0; k < selectableBuffer.Count; k++)
                {
                    Selectable selectable = selectableBuffer[k];
                    if (selectable != null)
                    {
                        output.Add(selectable);
                    }
                }

                for (int c = 0; c < current.childCount; c++)
                {
                    Transform child = current.GetChild(c);
                    if (child != null)
                    {
                        transformTraversalBuffer.Add(child);
                    }
                }
            }
        }
    }

    private void CollectSceneGraphics(List<Graphic> output)
    {
        output.Clear();

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return;
        }

        sceneRootsBuffer.Clear();
        activeScene.GetRootGameObjects(sceneRootsBuffer);

        for (int i = 0; i < sceneRootsBuffer.Count; i++)
        {
            GameObject root = sceneRootsBuffer[i];
            if (root == null)
            {
                continue;
            }

            transformTraversalBuffer.Clear();
            transformTraversalBuffer.Add(root.transform);

            for (int j = 0; j < transformTraversalBuffer.Count; j++)
            {
                Transform current = transformTraversalBuffer[j];
                if (current == null)
                {
                    continue;
                }

                graphicBuffer.Clear();
                current.GetComponents(graphicBuffer);
                for (int k = 0; k < graphicBuffer.Count; k++)
                {
                    Graphic graphic = graphicBuffer[k];
                    if (graphic != null)
                    {
                        output.Add(graphic);
                    }
                }

                for (int c = 0; c < current.childCount; c++)
                {
                    Transform child = current.GetChild(c);
                    if (child != null)
                    {
                        transformTraversalBuffer.Add(child);
                    }
                }
            }
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DEBUG/TESTING
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [ContextMenu("Reset All Tutorials")]
    private void ResetAllTutorials()
    {
        // XÃ³a táº¥t cáº£ tutorial keys tá»« PlayerPrefs
        // Tá»« danh sÃ¡ch Menu tutorials
        foreach (var config in tutorials)
        {
            if (config != null && !string.IsNullOrEmpty(config.tutorialName))
            {
                string key = Const.TUTORIAL_PREFIX + config.tutorialName;
                PlayerPrefs.DeleteKey(key);
            }
        }


        PlayerPrefs.Save();
        LogTutorial("[Tutorial] Reset all tutorials");
    }
}


