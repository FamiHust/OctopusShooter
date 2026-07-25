#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Development-only helper that auto-plays by selecting shooters into slot bar.
/// It does not modify gameplay logic, only triggers existing public flows.
/// </summary>
public class AutoPlayWinTool : MonoBehaviour
{
    private const string LegacyLearnedDataPrefsKey = "AutoPlayWinTool_LearnedData_v1";
    private const string LearnedDataFileName = "autoplay_win_tool_learning_v1.json";
    private const string LearnedDataSharedFolderName = "FlowBlast";

    private sealed class LevelLearningData
    {
        public readonly Dictionary<SeedColor, int> globalColorPickCounts = new Dictionary<SeedColor, int>(16);
        public readonly Dictionary<SeedColor, int> contextTotals = new Dictionary<SeedColor, int>(16);
        public readonly Dictionary<int, int> contextPickCounts = new Dictionary<int, int>(64);
        public readonly List<SeedColor> strictPickSequence = new List<SeedColor>(256);
        public int sampleCount;

        public void Clear()
        {
            globalColorPickCounts.Clear();
            contextTotals.Clear();
            contextPickCounts.Clear();
            strictPickSequence.Clear();
            sampleCount = 0;
        }
    }

    [System.Serializable]
    private sealed class LearnedDataSavePayload
    {
        public List<LevelLearningSaveData> levels = new List<LevelLearningSaveData>();
    }

    [System.Serializable]
    private sealed class LevelLearningSaveData
    {
        public int level;
        public int sampleCount;
        public List<ColorCountSaveData> globalColorPickCounts = new List<ColorCountSaveData>();
        public List<ColorCountSaveData> contextTotals = new List<ColorCountSaveData>();
        public List<IntCountSaveData> contextPickCounts = new List<IntCountSaveData>();
        public List<int> strictPickSequence = new List<int>();
    }

    [System.Serializable]
    private sealed class ColorCountSaveData
    {
        public int key;
        public int count;
    }

    [System.Serializable]
    private sealed class IntCountSaveData
    {
        public int key;
        public int count;
    }

    [Header("Tool State")]
    [SerializeField] private bool autoModeEnabled = false;
    [SerializeField] private bool showOverlay = false;

    [Header("Window Layout")]
    [SerializeField] private float overlayWidth = 620f;
    [SerializeField] private float overlayHeight = 300f;
    [SerializeField] private float overlayTopOffset = 64f;
    [SerializeField] private float overlayHorizontalPadding = 12f;

    [Header("Timing")]
    [SerializeField] private float autoPickInterval = 0.25f;
    [SerializeField] private float gameplayReadyBufferDelay = 0.35f;

    [Header("Planning (Win-Oriented)")]
    [SerializeField, Min(1)] private int mainRouteLookaheadRows = 8;
    [SerializeField, Min(1)] private int sideRouteLookaheadRows = 4;
    [SerializeField, Min(0f)] private float mainRoutePlanWeight = 2.2f;
    [SerializeField, Min(0f)] private float sideRoutePlanWeight = 1.1f;
    [SerializeField, Min(0f)] private float scarceColorBonusWeight = 1.4f;

    [Header("Learning From Player")]
    [SerializeField] private bool learnFromPlayerPattern = true;
    [SerializeField, Min(0f)] private float learnedPatternWeight = 1.6f;
    [SerializeField, Min(1)] private int minimumLearnedSamplesToApply = 1;
    [SerializeField] private bool stronglyPreferLearnedContextChoice = true;
    [SerializeField, Min(1)] private int minimumContextSamplesForStrongReplay = 2;
    [SerializeField, Range(0.5f, 1f)] private float strongReplayConfidenceThreshold = 0.6f;
    [SerializeField, Min(0f)] private float strongReplayPreferredColorBonus = 8f;
    [SerializeField, Min(0f)] private float strongReplayOtherColorPenalty = 3.5f;
    [SerializeField, Min(0.2f)] private float learningAutoSaveInterval = 1.5f;

    [Header("Hotkeys (Editor/Desktop)")]
    [SerializeField] private KeyCode pickOneNowKey = KeyCode.F8;
    [SerializeField] private KeyCode toggleAutoModeKey = KeyCode.F9;

    private readonly List<BaseShooter> shooterBuffer = new List<BaseShooter>(128);
    private readonly Dictionary<SeedColor, float> colorDemandScores = new Dictionary<SeedColor, float>(16);
    private readonly Dictionary<SeedColor, int> slotColorCounts = new Dictionary<SeedColor, int>(16);
    private readonly Dictionary<SeedColor, int> availableGridColorCounts = new Dictionary<SeedColor, int>(16);
    private readonly Dictionary<int, LevelLearningData> learnedDataByLevel = new Dictionary<int, LevelLearningData>(32);
    private readonly Dictionary<SeedColor, int> pendingGlobalColorPickCounts = new Dictionary<SeedColor, int>(16);
    private readonly Dictionary<SeedColor, int> pendingContextTotals = new Dictionary<SeedColor, int>(16);
    private readonly Dictionary<int, int> pendingContextPickCounts = new Dictionary<int, int>(64);
    private readonly List<SeedColor> pendingPickSequence = new List<SeedColor>(256);
    private float nextAutoPickAt;
    private float gameplayReadySince = -1f;
    private float nextLoadingUiScanAt;
    private bool hasActiveLoadingUiCached;
    private int lastInitializedLevel = -1;
    private int pendingLearningLevel = -1;
    private int pendingLearningSampleCount;
    private int activeReplaySequenceLevel = -1;
    private int replaySequenceIndex;
    private bool isLearningListenerBound;
    private bool isLearningDataDirty;
    private float nextLearningDataAutoSaveAt;

    public static void ToggleOverlayFromExternal()
    {
        AutoPlayWinTool tool = Object.FindObjectOfType<AutoPlayWinTool>(true);
        if (tool == null)
        {
            return;
        }

        tool.showOverlay = !tool.showOverlay;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        AutoPlayWinTool existing = Object.FindObjectOfType<AutoPlayWinTool>(true);
        if (existing != null)
        {
            return;
        }

        GameObject toolRoot = new GameObject("AutoPlayWinTool");
        Object.DontDestroyOnLoad(toolRoot);
        toolRoot.AddComponent<AutoPlayWinTool>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        showOverlay = false;
        LoadPersistedLearningData();
        BindLearningListener();
    }

    private void OnEnable()
    {
        BindLearningListener();
    }

    private void OnDisable()
    {
        DiscardPendingLearning();
        SavePersistedLearningData(true);
    }

    private void OnDestroy()
    {
        DiscardPendingLearning();
        SavePersistedLearningData(true);
        UnbindLearningListener();
    }

    private void OnApplicationQuit()
    {
        DiscardPendingLearning();
        SavePersistedLearningData(true);
    }

    private void Update()
    {
        HandleHotkeys();
        TryAutoSaveLearningData();

        if (!autoModeEnabled)
        {
            gameplayReadySince = -1f;
            return;
        }

        float now = Time.unscaledTime;
        if (!IsGameplayReadyForAutoPlay(now))
        {
            if (HasAnyActiveLoadingUI(now))
            {
                ResetReplaySequenceState();
            }

            gameplayReadySince = -1f;
            return;
        }

        if (gameplayReadySince < 0f)
        {
            gameplayReadySince = now;
        }

        if (now - gameplayReadySince < Mathf.Max(0f, gameplayReadyBufferDelay))
        {
            return;
        }

        if (now >= nextAutoPickAt)
        {
            TryPickOneShooterToSlot();
            nextAutoPickAt = now + Mathf.Max(0.05f, autoPickInterval);
        }
    }

    private void HandleHotkeys()
    {
        if (Input.GetKeyDown(toggleAutoModeKey))
        {
            autoModeEnabled = !autoModeEnabled;
        }

        if (Input.GetKeyDown(pickOneNowKey))
        {
            TryPickOneShooterToSlot();
        }
    }

    private bool TryPickOneShooterToSlot()
    {
        if (!IsGameplayReadyForAutoPlay(Time.unscaledTime))
        {
            return false;
        }

        InGameUIManager inGameUI = Object.FindObjectOfType<InGameUIManager>();
        if (inGameUI == null || !inGameUI.gameObject.activeInHierarchy)
        {
            return false;
        }

        SlotBar slotBar = SlotBar.Instance;
        if (slotBar == null || slotBar.IsFull())
        {
            return false;
        }

        BaseShooter selected = FindNextPickableShooterOnGrid();
        if (selected == null)
        {
            return false;
        }

        if (!slotBar.AddShooter(selected))
        {
            return false;
        }

        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        if (IsLastPickableShooterOnGrid(selected) &&
            SpeedMultiplierManager.Instance != null &&
            !SpeedMultiplierManager.IsSpeedUpActive())
        {
            SpeedMultiplierManager.Instance.ToggleSpeedUp();
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
        }

        GameEventHub.Instance?.Invoke(GameEventType.OnShooterJumpStart, selected);
        GameEventHub.Instance?.Invoke(GameEventType.OnShooterSelected, selected);
        GameEventHub.Instance?.Invoke(GameEventType.OnShooterAddedToSlot, null);

        return true;
    }

    private bool IsGameplayReadyForAutoPlay(float now)
    {
        InGameUIManager inGameUI = Object.FindObjectOfType<InGameUIManager>();
        if (inGameUI == null || !inGameUI.gameObject.activeInHierarchy)
        {
            return false;
        }

        InputManager inputManager = InputManager.Instance;
        if (inputManager == null || !inputManager.IsInputActive())
        {
            return false;
        }

        if (HasAnyActiveLoadingUI(now))
        {
            return false;
        }

        return true;
    }

    private bool HasAnyActiveLoadingUI(float now)
    {
        if (now < nextLoadingUiScanAt)
        {
            return hasActiveLoadingUiCached;
        }

        nextLoadingUiScanAt = now + 0.15f;
        hasActiveLoadingUiCached = false;

        LoadingUI[] loadingUIs = Object.FindObjectsOfType<LoadingUI>(true);
        if (loadingUIs == null || loadingUIs.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < loadingUIs.Length; i++)
        {
            LoadingUI loading = loadingUIs[i];
            if (loading == null)
            {
                continue;
            }

            if (loading.gameObject.activeInHierarchy)
            {
                hasActiveLoadingUiCached = true;
                return true;
            }
        }

        return false;
    }

    private BaseShooter FindNextPickableShooterOnGrid()
    {
        int currentLevel = GetCurrentLearningLevel();
        LevelLearningData levelLearningData = GetLearningData(currentLevel, false);

        if (HasStrictSequenceSample(levelLearningData))
        {
            return FindNextPickableShooterFromStrictSequence(levelLearningData, currentLevel);
        }

        // No stored sequence yet for this level: allow heuristic exploration.
        SlotBar slotBar = SlotBar.Instance;
        if (slotBar == null)
        {
            return null;
        }

        BaseShooter.FillRegisteredShooterBuffer(shooterBuffer, true);
        BuildAvailableGridColorCounts();
        BuildColorDemandScores();
        BuildRouteForecastDemandScores();
        BuildSlotColorCounts(slotBar);
        bool hasDemandTargets = colorDemandScores.Count > 0;
        bool hasDemandColorNotCoveredInSlot = false;
        bool hasCandidateForDemandColorNotCovered = false;
        bool hasPrimaryDemandColor = TryGetPrimaryDemandColor(out SeedColor primaryDemandColor);
        bool hasStrongReplayPreferredColor = TryGetStrongReplayPreferredColor(levelLearningData, hasPrimaryDemandColor, primaryDemandColor, out SeedColor strongReplayPreferredColor);

        if (hasStrongReplayPreferredColor)
        {
            int preferredAvailable = 0;
            availableGridColorCounts.TryGetValue(strongReplayPreferredColor, out preferredAvailable);
            hasStrongReplayPreferredColor = preferredAvailable > 0;
        }

        if (hasDemandTargets)
        {
            foreach (KeyValuePair<SeedColor, float> kv in colorDemandScores)
            {
                if (kv.Value <= 0f)
                {
                    continue;
                }

                int countInSlot = 0;
                slotColorCounts.TryGetValue(kv.Key, out countInSlot);
                if (countInSlot <= 0)
                {
                    hasDemandColorNotCoveredInSlot = true;
                    break;
                }
            }
        }

        BaseShooter bestShooter = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < shooterBuffer.Count; i++)
        {
            BaseShooter shooter = shooterBuffer[i];
            if (shooter == null || !shooter.isActiveAndEnabled)
            {
                continue;
            }

            if (shooter.GetCurrentState() != ShooterState.IdleGrid)
            {
                continue;
            }

            SeedColor shooterColor = shooter.GetTargetColor();
            float demandScore = 0f;
            colorDemandScores.TryGetValue(shooterColor, out demandScore);

            int countInSlot = 0;
            slotColorCounts.TryGetValue(shooterColor, out countInSlot);

            int availableInGrid = 0;
            availableGridColorCounts.TryGetValue(shooterColor, out availableInGrid);

            if (hasDemandColorNotCoveredInSlot)
            {
                if (demandScore > 0f && countInSlot <= 0)
                {
                    hasCandidateForDemandColorNotCovered = true;
                }
                else
                {
                    // While there is still an uncovered demanded color, avoid spending slot on other colors.
                    continue;
                }
            }

            float score = demandScore * 2.2f;
            if (countInSlot <= 0)
            {
                score += 1.25f;
            }
            else
            {
                score -= countInSlot * 1.1f;
            }

            if (demandScore <= 0f)
            {
                score -= 0.8f;
            }

            if (demandScore > 0f && availableInGrid > 0)
            {
                // Scarce demanded colors should be picked earlier to avoid later deadlocks.
                score += (scarceColorBonusWeight / availableInGrid);
            }

            if (hasStrongReplayPreferredColor)
            {
                if (shooterColor == strongReplayPreferredColor)
                {
                    score += strongReplayPreferredColorBonus;
                }
                else
                {
                    score -= strongReplayOtherColorPenalty;
                }
            }

            score += EvaluateLearnedPreferenceBonus(levelLearningData, shooterColor, hasPrimaryDemandColor, primaryDemandColor);

            // Stable tie-breaker to avoid flickering pick order between equal choices.
            score += (shooter.GetInstanceID() & 31) * 0.0001f;

            if (score > bestScore)
            {
                bestScore = score;
                bestShooter = shooter;
            }
        }

        if (bestShooter == null)
        {
            return null;
        }

        // Keep one slot flexible when no currently demanded missing color is available.
        if (hasDemandTargets && !hasCandidateForDemandColorNotCovered)
        {
            int emptySlots = slotBar.GetEmptySlotCount();
            int shooterCount = slotBar.GetShooterCount();
            if (emptySlots <= 1 && shooterCount >= 2)
            {
                return null;
            }
        }

        return bestShooter;
    }

    private BaseShooter FindNextPickableShooterFromStrictSequence(LevelLearningData levelLearningData, int level)
    {
        if (!HasStrictSequenceSample(levelLearningData))
        {
            return null;
        }

        if (activeReplaySequenceLevel != level)
        {
            activeReplaySequenceLevel = level;
            replaySequenceIndex = 0;
        }

        if (replaySequenceIndex < 0 || replaySequenceIndex >= levelLearningData.strictPickSequence.Count)
        {
            return null;
        }

        SeedColor expectedColor = levelLearningData.strictPickSequence[replaySequenceIndex];
        BaseShooter.FillRegisteredShooterBuffer(shooterBuffer, true);

        for (int i = 0; i < shooterBuffer.Count; i++)
        {
            BaseShooter shooter = shooterBuffer[i];
            if (shooter == null || !shooter.isActiveAndEnabled)
            {
                continue;
            }

            if (shooter.GetCurrentState() != ShooterState.IdleGrid)
            {
                continue;
            }

            if (shooter.GetTargetColor() != expectedColor)
            {
                continue;
            }

            replaySequenceIndex++;
            return shooter;
        }

        // Strict replay mode: no fallback to heuristic if expected color is currently unavailable.
        return null;
    }

    private bool HasStrictSequenceSample(LevelLearningData levelLearningData)
    {
        return levelLearningData != null &&
               levelLearningData.strictPickSequence != null &&
               levelLearningData.strictPickSequence.Count > 0;
    }

    private bool TryGetStrongReplayPreferredColor(LevelLearningData levelLearningData, bool hasPrimaryDemandColor, SeedColor primaryDemandColor, out SeedColor preferredColor)
    {
        preferredColor = SeedColor.Hidden;

        if (!learnFromPlayerPattern ||
            !stronglyPreferLearnedContextChoice ||
            !hasPrimaryDemandColor ||
            levelLearningData == null ||
            levelLearningData.sampleCount < Mathf.Max(1, minimumLearnedSamplesToApply))
        {
            return false;
        }

        int contextTotal = 0;
        levelLearningData.contextTotals.TryGetValue(primaryDemandColor, out contextTotal);
        if (contextTotal < Mathf.Max(1, minimumContextSamplesForStrongReplay))
        {
            return false;
        }

        int bestCount = 0;
        SeedColor bestColor = SeedColor.Hidden;

        foreach (KeyValuePair<int, int> kv in levelLearningData.contextPickCounts)
        {
            int key = kv.Key;
            int count = kv.Value;
            if (count <= 0)
            {
                continue;
            }

            SeedColor contextColor = (SeedColor)((key >> 16) & 0xFFFF);
            if (contextColor != primaryDemandColor)
            {
                continue;
            }

            SeedColor pickedColor = (SeedColor)(key & 0xFFFF);
            if (count > bestCount)
            {
                bestCount = count;
                bestColor = pickedColor;
            }
        }

        if (bestCount <= 0)
        {
            return false;
        }

        float confidence = bestCount / Mathf.Max(1f, contextTotal);
        if (confidence < Mathf.Clamp01(strongReplayConfidenceThreshold))
        {
            return false;
        }

        preferredColor = bestColor;
        return true;
    }

    private void BindLearningListener()
    {
        if (isLearningListenerBound)
        {
            return;
        }

        if (GameEventHub.Instance == null)
        {
            return;
        }

        GameEventHub.Instance.AddListener(GameEventType.OnShooterSelected, OnShooterSelectedForLearning);
        GameEventHub.Instance.AddListener(GameEventType.OnGameWin, OnGameWinForLearning);
        GameEventHub.Instance.AddListener(GameEventType.OnGameLose, OnGameLoseForLearning);
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarInit, OnSlotBarInitForReplayReset);
        isLearningListenerBound = true;
    }

    private void UnbindLearningListener()
    {
        if (!isLearningListenerBound)
        {
            return;
        }

        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.RemoveListener(GameEventType.OnShooterSelected, OnShooterSelectedForLearning);
            GameEventHub.Instance.RemoveListener(GameEventType.OnGameWin, OnGameWinForLearning);
            GameEventHub.Instance.RemoveListener(GameEventType.OnGameLose, OnGameLoseForLearning);
            GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarInit, OnSlotBarInitForReplayReset);
        }

        isLearningListenerBound = false;
    }

    private void OnShooterSelectedForLearning(object data)
    {
        if (!learnFromPlayerPattern)
        {
            return;
        }

        BaseShooter pickedShooter = data as BaseShooter;
        if (pickedShooter == null)
        {
            return;
        }

        BuildColorDemandScores();
        BuildRouteForecastDemandScores();

        int currentLevel = GetCurrentLearningLevel();
        SeedColor pickedColor = pickedShooter.GetTargetColor();
        SeedColor contextColor;
        bool hasContext = TryGetPrimaryDemandColor(out contextColor);

        EnsurePendingLearningLevel(currentLevel);
        IncrementPendingGlobalColorPickCount(pickedColor);
        pendingPickSequence.Add(pickedColor);
        if (hasContext)
        {
            IncrementPendingContextColorPickCount(contextColor, pickedColor);
        }
    }

    private void OnGameWinForLearning(object _)
    {
        CommitPendingLearningOnWin();
        ResetReplaySequenceState();
    }

    private void OnGameLoseForLearning(object _)
    {
        DiscardPendingLearning();
        ResetReplaySequenceState();
    }

    private void OnSlotBarInitForReplayReset(object _)
    {
        int currentLevel = GetCurrentLearningLevel();
        if (lastInitializedLevel > 0 && currentLevel != lastInitializedLevel)
        {
            // Stop auto carry-over when moving to another level.
            autoModeEnabled = false;
        }

        lastInitializedLevel = currentLevel;

        // New level session starts here (including restart/home flow).
        DiscardPendingLearning();
        ResetReplaySequenceState();
        gameplayReadySince = -1f;
        nextAutoPickAt = 0f;
    }

    private void EnsurePendingLearningLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);

        if (pendingLearningLevel < 0)
        {
            pendingLearningLevel = safeLevel;
            return;
        }

        if (pendingLearningLevel == safeLevel)
        {
            return;
        }

        // Different level context without win/lose finalization: discard old pending samples.
        DiscardPendingLearning();
        pendingLearningLevel = safeLevel;
    }

    private void IncrementPendingGlobalColorPickCount(SeedColor pickedColor)
    {
        int count = 0;
        pendingGlobalColorPickCounts.TryGetValue(pickedColor, out count);
        pendingGlobalColorPickCounts[pickedColor] = count + 1;
        pendingLearningSampleCount++;
    }

    private void IncrementPendingContextColorPickCount(SeedColor contextColor, SeedColor pickedColor)
    {
        int total = 0;
        pendingContextTotals.TryGetValue(contextColor, out total);
        pendingContextTotals[contextColor] = total + 1;

        int key = BuildContextColorKey(contextColor, pickedColor);
        int pairCount = 0;
        pendingContextPickCounts.TryGetValue(key, out pairCount);
        pendingContextPickCounts[key] = pairCount + 1;
    }

    private void CommitPendingLearningOnWin()
    {
        if (pendingLearningLevel < 0 || pendingLearningSampleCount <= 0)
        {
            DiscardPendingLearning();
            return;
        }

        LevelLearningData levelLearningData = GetLearningData(pendingLearningLevel, true);
        if (levelLearningData == null)
        {
            DiscardPendingLearning();
            return;
        }

        levelLearningData.sampleCount += pendingLearningSampleCount;

        MergeColorCountMap(levelLearningData.globalColorPickCounts, pendingGlobalColorPickCounts);
        MergeColorCountMap(levelLearningData.contextTotals, pendingContextTotals);
        MergeIntCountMap(levelLearningData.contextPickCounts, pendingContextPickCounts);

        if (pendingPickSequence.Count > 0)
        {
            levelLearningData.strictPickSequence.Clear();
            levelLearningData.strictPickSequence.AddRange(pendingPickSequence);
        }

        MarkLearningDataDirty();
        DiscardPendingLearning();
    }

    private void DiscardPendingLearning()
    {
        pendingLearningLevel = -1;
        pendingLearningSampleCount = 0;
        pendingGlobalColorPickCounts.Clear();
        pendingContextTotals.Clear();
        pendingContextPickCounts.Clear();
        pendingPickSequence.Clear();
    }

    private void ResetReplaySequenceState()
    {
        activeReplaySequenceLevel = -1;
        replaySequenceIndex = 0;
    }

    private void IncrementGlobalLearnedColorCount(LevelLearningData levelLearningData, SeedColor pickedColor)
    {
        if (levelLearningData == null)
        {
            return;
        }

        int count = 0;
        levelLearningData.globalColorPickCounts.TryGetValue(pickedColor, out count);
        levelLearningData.globalColorPickCounts[pickedColor] = count + 1;
        levelLearningData.sampleCount++;
        MarkLearningDataDirty();
    }

    private void IncrementContextLearnedColorCount(LevelLearningData levelLearningData, SeedColor contextColor, SeedColor pickedColor)
    {
        if (levelLearningData == null)
        {
            return;
        }

        int total = 0;
        levelLearningData.contextTotals.TryGetValue(contextColor, out total);
        levelLearningData.contextTotals[contextColor] = total + 1;

        int key = BuildContextColorKey(contextColor, pickedColor);
        int pairCount = 0;
        levelLearningData.contextPickCounts.TryGetValue(key, out pairCount);
        levelLearningData.contextPickCounts[key] = pairCount + 1;
        MarkLearningDataDirty();
    }

    private float EvaluateLearnedPreferenceBonus(LevelLearningData levelLearningData, SeedColor shooterColor, bool hasPrimaryDemandColor, SeedColor primaryDemandColor)
    {
        if (!learnFromPlayerPattern || levelLearningData == null || levelLearningData.sampleCount < Mathf.Max(1, minimumLearnedSamplesToApply))
        {
            return 0f;
        }

        float bonus = 0f;

        int globalColorCount = 0;
        levelLearningData.globalColorPickCounts.TryGetValue(shooterColor, out globalColorCount);
        if (globalColorCount > 0)
        {
            float globalProbability = globalColorCount / Mathf.Max(1f, levelLearningData.sampleCount);
            bonus += globalProbability * learnedPatternWeight * 0.8f;
        }

        if (hasPrimaryDemandColor)
        {
            int contextTotal = 0;
            levelLearningData.contextTotals.TryGetValue(primaryDemandColor, out contextTotal);
            if (contextTotal > 0)
            {
                int pairCount = 0;
                int key = BuildContextColorKey(primaryDemandColor, shooterColor);
                levelLearningData.contextPickCounts.TryGetValue(key, out pairCount);
                float contextProbability = pairCount / Mathf.Max(1f, contextTotal);
                bonus += contextProbability * learnedPatternWeight * 1.6f;
            }
        }

        return bonus;
    }

    private bool TryGetPrimaryDemandColor(out SeedColor color)
    {
        color = SeedColor.Hidden;
        if (colorDemandScores == null || colorDemandScores.Count == 0)
        {
            return false;
        }

        float bestScore = float.MinValue;
        bool found = false;

        foreach (KeyValuePair<SeedColor, float> kv in colorDemandScores)
        {
            if (kv.Value <= 0f)
            {
                continue;
            }

            if (!found || kv.Value > bestScore)
            {
                found = true;
                bestScore = kv.Value;
                color = kv.Key;
            }
        }

        return found;
    }

    private int BuildContextColorKey(SeedColor contextColor, SeedColor pickedColor)
    {
        return (((int)contextColor & 0xFFFF) << 16) | ((int)pickedColor & 0xFFFF);
    }

    private int GetCurrentLearningLevel()
    {
        return Mathf.Max(1, PlayerPrefs.GetInt(Const.player_level_key, 1));
    }

    private LevelLearningData GetLearningData(int level, bool createIfMissing)
    {
        level = Mathf.Max(1, level);

        if (learnedDataByLevel.TryGetValue(level, out LevelLearningData existingData))
        {
            return existingData;
        }

        if (!createIfMissing)
        {
            return null;
        }

        LevelLearningData newData = new LevelLearningData();
        learnedDataByLevel[level] = newData;
        return newData;
    }

    private int GetLearnedSampleCountForLevel(int level)
    {
        LevelLearningData data = GetLearningData(level, false);
        return data != null ? data.sampleCount : 0;
    }

    private void MarkLearningDataDirty()
    {
        isLearningDataDirty = true;
        nextLearningDataAutoSaveAt = Time.unscaledTime + Mathf.Max(0.2f, learningAutoSaveInterval);
    }

    private void TryAutoSaveLearningData()
    {
        if (!isLearningDataDirty)
        {
            return;
        }

        if (Time.unscaledTime < nextLearningDataAutoSaveAt)
        {
            return;
        }

        SavePersistedLearningData(false);
    }

    private void LoadPersistedLearningData()
    {
        learnedDataByLevel.Clear();

        string json = LoadPersistedLearningJson();
        if (string.IsNullOrEmpty(json))
        {
            isLearningDataDirty = false;
            return;
        }

        LearnedDataSavePayload payload = JsonUtility.FromJson<LearnedDataSavePayload>(json);
        if (payload == null || payload.levels == null)
        {
            isLearningDataDirty = false;
            return;
        }

        for (int i = 0; i < payload.levels.Count; i++)
        {
            LevelLearningSaveData levelSaveData = payload.levels[i];
            if (levelSaveData == null)
            {
                continue;
            }

            int level = Mathf.Max(1, levelSaveData.level);
            LevelLearningData levelData = GetLearningData(level, true);
            levelData.Clear();
            levelData.sampleCount = Mathf.Max(0, levelSaveData.sampleCount);

            RestoreColorCountMap(levelData.globalColorPickCounts, levelSaveData.globalColorPickCounts);
            RestoreColorCountMap(levelData.contextTotals, levelSaveData.contextTotals);
            RestoreIntCountMap(levelData.contextPickCounts, levelSaveData.contextPickCounts);
            RestoreColorSequence(levelData.strictPickSequence, levelSaveData.strictPickSequence);
        }

        isLearningDataDirty = false;
    }

    private string LoadPersistedLearningJson()
    {
        string filePath = GetLearnedDataFilePath();
        string json = TryReadTextFromFile(filePath);
        if (!string.IsNullOrEmpty(json))
        {
            return json;
        }

        string sharedBackupPath = GetSharedBackupFilePath();
        json = TryReadTextFromFile(sharedBackupPath);
        if (!string.IsNullOrEmpty(json))
        {
            TryWriteTextToFile(filePath, json);
            return json;
        }

        // One-time migration path from old PlayerPrefs storage.
        string legacyJson = PlayerPrefs.GetString(LegacyLearnedDataPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(legacyJson))
        {
            return string.Empty;
        }

        TryWriteLearningJsonToFile(legacyJson);
        PlayerPrefs.DeleteKey(LegacyLearnedDataPrefsKey);
        PlayerPrefs.Save();
        return legacyJson;
    }

    private void SavePersistedLearningData(bool forceSave)
    {
        if (!forceSave && !isLearningDataDirty)
        {
            return;
        }

        LearnedDataSavePayload payload = new LearnedDataSavePayload();

        foreach (KeyValuePair<int, LevelLearningData> kv in learnedDataByLevel)
        {
            int level = kv.Key;
            LevelLearningData levelData = kv.Value;
            if (levelData == null || levelData.sampleCount <= 0)
            {
                continue;
            }

            LevelLearningSaveData saveData = new LevelLearningSaveData
            {
                level = Mathf.Max(1, level),
                sampleCount = levelData.sampleCount
            };

            FillColorCountList(saveData.globalColorPickCounts, levelData.globalColorPickCounts);
            FillColorCountList(saveData.contextTotals, levelData.contextTotals);
            FillIntCountList(saveData.contextPickCounts, levelData.contextPickCounts);
            FillColorSequence(saveData.strictPickSequence, levelData.strictPickSequence);

            payload.levels.Add(saveData);
        }

        string filePath = GetLearnedDataFilePath();
        if (payload.levels.Count <= 0)
        {
            TryDeleteFile(filePath);
        }
        else
        {
            string json = JsonUtility.ToJson(payload);
            TryWriteLearningJsonToFile(json);
        }

        // Ensure legacy key no longer participates in persistence.
        if (PlayerPrefs.HasKey(LegacyLearnedDataPrefsKey))
        {
            PlayerPrefs.DeleteKey(LegacyLearnedDataPrefsKey);
            PlayerPrefs.Save();
        }

        isLearningDataDirty = false;
    }

    private string GetLearnedDataFilePath()
    {
        return Path.Combine(Application.persistentDataPath, LearnedDataFileName);
    }

    private string GetSharedBackupFilePath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass environmentClass = new AndroidJavaClass("android.os.Environment"))
            {
                string documentsDirectoryType = environmentClass.GetStatic<string>("DIRECTORY_DOCUMENTS");
                using (AndroidJavaObject documentsDirectory = environmentClass.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", documentsDirectoryType))
                {
                    if (documentsDirectory == null)
                    {
                        return string.Empty;
                    }

                    string documentsPath = documentsDirectory.Call<string>("getAbsolutePath");
                    if (string.IsNullOrEmpty(documentsPath))
                    {
                        return string.Empty;
                    }

                    return Path.Combine(documentsPath, LearnedDataSharedFolderName, LearnedDataFileName);
                }
            }
        }
        catch
        {
            return string.Empty;
        }
#else
        return string.Empty;
#endif
    }

    private void TryWriteLearningJsonToFile(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        string filePath = GetLearnedDataFilePath();
        TryWriteTextToFile(filePath, json);

        string sharedBackupPath = GetSharedBackupFilePath();
        if (!string.IsNullOrEmpty(sharedBackupPath))
        {
            TryWriteTextToFile(sharedBackupPath, json);
        }
    }

    private string TryReadTextFromFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }

        try
        {
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
        }
        catch
        {
            // Ignore file read failures and fallback to other sources.
        }

        return string.Empty;
    }

    private void TryWriteTextToFile(string filePath, string content)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(content))
        {
            return;
        }

        try
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
        }
        catch
        {
            // Keep tool resilient: write errors should not break runtime.
        }
    }

    private void TryDeleteFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore delete failures in development tool context.
        }
    }

    private void FillColorCountList(List<ColorCountSaveData> output, Dictionary<SeedColor, int> source)
    {
        output.Clear();
        if (source == null)
        {
            return;
        }

        foreach (KeyValuePair<SeedColor, int> kv in source)
        {
            if (kv.Value <= 0)
            {
                continue;
            }

            output.Add(new ColorCountSaveData
            {
                key = (int)kv.Key,
                count = kv.Value
            });
        }
    }

    private void FillIntCountList(List<IntCountSaveData> output, Dictionary<int, int> source)
    {
        output.Clear();
        if (source == null)
        {
            return;
        }

        foreach (KeyValuePair<int, int> kv in source)
        {
            if (kv.Value <= 0)
            {
                continue;
            }

            output.Add(new IntCountSaveData
            {
                key = kv.Key,
                count = kv.Value
            });
        }
    }

    private void RestoreColorCountMap(Dictionary<SeedColor, int> output, List<ColorCountSaveData> source)
    {
        output.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ColorCountSaveData item = source[i];
            if (item == null || item.count <= 0)
            {
                continue;
            }

            output[(SeedColor)item.key] = item.count;
        }
    }

    private void RestoreIntCountMap(Dictionary<int, int> output, List<IntCountSaveData> source)
    {
        output.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            IntCountSaveData item = source[i];
            if (item == null || item.count <= 0)
            {
                continue;
            }

            output[item.key] = item.count;
        }
    }

    private void FillColorSequence(List<int> output, List<SeedColor> source)
    {
        output.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            output.Add((int)source[i]);
        }
    }

    private void RestoreColorSequence(List<SeedColor> output, List<int> source)
    {
        output.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            output.Add((SeedColor)source[i]);
        }
    }

    private void MergeColorCountMap(Dictionary<SeedColor, int> target, Dictionary<SeedColor, int> source)
    {
        if (target == null || source == null)
        {
            return;
        }

        foreach (KeyValuePair<SeedColor, int> kv in source)
        {
            if (kv.Value <= 0)
            {
                continue;
            }

            int existing = 0;
            target.TryGetValue(kv.Key, out existing);
            target[kv.Key] = existing + kv.Value;
        }
    }

    private void MergeIntCountMap(Dictionary<int, int> target, Dictionary<int, int> source)
    {
        if (target == null || source == null)
        {
            return;
        }

        foreach (KeyValuePair<int, int> kv in source)
        {
            if (kv.Value <= 0)
            {
                continue;
            }

            int existing = 0;
            target.TryGetValue(kv.Key, out existing);
            target[kv.Key] = existing + kv.Value;
        }
    }

    private void BuildAvailableGridColorCounts()
    {
        availableGridColorCounts.Clear();

        for (int i = 0; i < shooterBuffer.Count; i++)
        {
            BaseShooter shooter = shooterBuffer[i];
            if (shooter == null || !shooter.isActiveAndEnabled)
            {
                continue;
            }

            if (shooter.GetCurrentState() != ShooterState.IdleGrid)
            {
                continue;
            }

            SeedColor color = shooter.GetTargetColor();
            int count = 0;
            availableGridColorCounts.TryGetValue(color, out count);
            availableGridColorCounts[color] = count + 1;
        }
    }

    private void BuildColorDemandScores()
    {
        colorDemandScores.Clear();

        FireRangeDetector detector = FireRangeDetector.Instance;
        if (detector == null)
        {
            return;
        }

        IReadOnlyList<BlockRowSeedSpawner> targets = detector.GetTargetsInRangeView();
        if (targets == null || targets.Count == 0)
        {
            return;
        }

        int targetCount = targets.Count;
        for (int i = 0; i < targetCount; i++)
        {
            BlockRowSeedSpawner row = targets[i];
            if (row == null || row.IsDestroyingSeedsSequentially)
            {
                continue;
            }

            int seedCount = row.GetSeedCount();
            if (seedCount <= 0)
            {
                continue;
            }

            SeedColor rowColor = row.GetCurrentColor();
            float proximityWeight = (targetCount - i) * 0.75f;
            float score = 2f + (seedCount * 0.6f) + proximityWeight;

            if (row.TryPeekFirstSeedColor(out SeedColor frontSeedColor))
            {
                float frontBonus = 1.4f + (targetCount - i) * 0.25f;
                AddColorDemand(frontSeedColor, frontBonus);
            }

            AddColorDemand(rowColor, score);
        }
    }

    private void BuildRouteForecastDemandScores()
    {
        SplineController splineController = SplineController.Instance;
        if (splineController == null)
        {
            return;
        }

        SplineRoute mainRoute = splineController.GetMainRoute();
        AddRouteLookaheadDemand(mainRoute, Mathf.Max(1, mainRouteLookaheadRows), Mathf.Max(0f, mainRoutePlanWeight));

        SplineRoute[] sideRoutes = splineController.GetSideRoutes();
        if (sideRoutes == null || sideRoutes.Length == 0)
        {
            return;
        }

        for (int i = 0; i < sideRoutes.Length; i++)
        {
            AddRouteLookaheadDemand(sideRoutes[i], Mathf.Max(1, sideRouteLookaheadRows), Mathf.Max(0f, sideRoutePlanWeight));
        }
    }

    private void AddRouteLookaheadDemand(SplineRoute route, int lookaheadRows, float routeWeight)
    {
        if (route == null || routeWeight <= 0f || lookaheadRows <= 0)
        {
            return;
        }

        List<GameObject> rows = route.GetActiveBlockRows();
        if (rows == null || rows.Count == 0)
        {
            return;
        }

        int maxCount = Mathf.Min(lookaheadRows, rows.Count);
        for (int i = 0; i < maxCount; i++)
        {
            GameObject rowObj = rows[i];
            if (rowObj == null)
            {
                continue;
            }

            BlockRowSeedSpawner seeder = rowObj.GetComponent<BlockRowSeedSpawner>();
            if (seeder == null || seeder.IsDestroyingSeedsSequentially)
            {
                continue;
            }

            int seedCount = seeder.GetSeedCount();
            if (seedCount <= 0)
            {
                continue;
            }

            float proximityWeight = (maxCount - i);
            float demand = routeWeight * (0.8f + (seedCount * 0.12f) + (proximityWeight * 0.5f));

            SeedColor rowColor = seeder.GetCurrentColor();
            AddColorDemand(rowColor, demand);

            if (seeder.TryPeekFirstSeedColor(out SeedColor frontColor))
            {
                AddColorDemand(frontColor, routeWeight * (0.9f + (proximityWeight * 0.35f)));
            }
        }
    }

    private void BuildSlotColorCounts(SlotBar slotBar)
    {
        slotColorCounts.Clear();
        if (slotBar == null)
        {
            return;
        }

        List<BaseShooter> slotShooters = slotBar.GetAllShooters();
        for (int i = 0; i < slotShooters.Count; i++)
        {
            BaseShooter shooter = slotShooters[i];
            if (shooter == null)
            {
                continue;
            }

            SeedColor color = shooter.GetTargetColor();
            int existingCount = 0;
            slotColorCounts.TryGetValue(color, out existingCount);
            slotColorCounts[color] = existingCount + 1;
        }
    }

    private void AddColorDemand(SeedColor color, float score)
    {
        if (score <= 0f)
        {
            return;
        }

        float existing = 0f;
        colorDemandScores.TryGetValue(color, out existing);
        colorDemandScores[color] = existing + score;
    }

    private bool IsLastPickableShooterOnGrid(BaseShooter selectedShooter)
    {
        if (selectedShooter == null)
        {
            return false;
        }

        BaseShooter.FillRegisteredShooterBuffer(shooterBuffer, true);
        for (int i = 0; i < shooterBuffer.Count; i++)
        {
            BaseShooter shooter = shooterBuffer[i];
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

    private void OnGUI()
    {
        if (!showOverlay)
        {
            return;
        }

        float width = Mathf.Clamp(overlayWidth, 460f, Mathf.Max(460f, Screen.width - 8f));
        float height = Mathf.Clamp(overlayHeight, 240f, Mathf.Max(240f, Screen.height - 8f));
        float x = Mathf.Clamp((Screen.width - width) * 0.5f, 4f, Mathf.Max(4f, Screen.width - width - 4f));
        float y = Mathf.Clamp(overlayTopOffset, 4f, Mathf.Max(4f, Screen.height - height - 4f));

        GUILayout.BeginArea(new Rect(x, y, width, height), "Auto Play Tool", GUI.skin.window);
        GUILayout.Space(Mathf.Max(0f, overlayHorizontalPadding * 0.25f));

        int currentLevel = GetCurrentLearningLevel();
        int learnedSampleCount = GetLearnedSampleCountForLevel(currentLevel);
        LevelLearningData currentLevelData = GetLearningData(currentLevel, false);
        int strictSequenceCount = currentLevelData != null ? currentLevelData.strictPickSequence.Count : 0;

        GUILayout.Label($"Auto mode: {(autoModeEnabled ? "ON" : "OFF")}");
        GUILayout.Label($"Learning level: {currentLevel}");
        GUILayout.Label($"Learned samples: {learnedSampleCount}");
        GUILayout.Label($"Strict sequence length: {strictSequenceCount}");
        GUILayout.Label($"Replay mode: {(strictSequenceCount > 0 ? "STRICT SAMPLE" : "HEURISTIC (NO SAMPLE)")}");
        GUILayout.Label($"Pending samples this run: {pendingLearningSampleCount}");
        GUILayout.Space(4f);

        if (GUILayout.Button(autoModeEnabled ? "Disable Auto" : "Enable Auto"))
        {
            autoModeEnabled = !autoModeEnabled;
        }

        if (GUILayout.Button("Pick One Shooter Now (Smart)"))
        {
            TryPickOneShooterToSlot();
        }

        GUILayout.Space(6f);
        GUILayout.Label("Open/Close panel: tap Level text in InGame");
        GUILayout.Label("F8: Pick One | F9: Toggle Auto");
        GUILayout.EndArea();
    }
}
#endif
