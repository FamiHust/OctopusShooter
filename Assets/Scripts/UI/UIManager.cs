using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.Profiling;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [Header("UI Prefabs")]
    [SerializeField] private GameObject inGameUIPrefab;
    [SerializeField] private GameObject menuUIPrefab;
    [SerializeField] private GameObject loadingUIPrefab;
    [SerializeField] private GameObject loadingUI2Prefab;
    [SerializeField] private GameObject winUIPrefab;
    [SerializeField] private GameObject loseUIPrefab;
    [SerializeField] private GameObject getMoreLiveUIPrefab;
    [SerializeField] private GameObject tutorialPrefab;
    [SerializeField] private GamePlayController gamePlayController;
    [Header("UI Container")]
    [SerializeField] private Transform uiContainer; // Parent cho UI instances

    [Header("Popup Container")]
    [SerializeField] private Transform popupContainer; // Parent cho táº¥t cáº£ popup

    [Header("Popup Prefabs")]
    [SerializeField] private List<GameObject> popupPrefabs = new List<GameObject>();

    [Header("UI Performance")]
    [SerializeField] private bool reuseLoadingUIWhenSamePrefab = true;
    [SerializeField] private bool restartLoadingAnimationOnReuse = false;
    [SerializeField] private bool cacheLoadingUIInstancesByPrefab = true;
    [SerializeField] private bool cachePopupInstances = true;
    [SerializeField] private bool keepPopupCacheOnHideAll = true;
    [SerializeField] private bool keepInGameUIInstanceOnMenu = true;
    [SerializeField] private bool keepResultUIInstances = true;
    [SerializeField, Min(0f)] private float firstLevelInitDelay = 0f;
    [SerializeField, Min(0f)] private float loadingUI2AutoHideDelay = 1f;
    [SerializeField, Min(0.05f)] private float loadingUI2FadeDuration = 0.25f;
    [SerializeField] private bool enableLowEndUILiteMode = true;
    [SerializeField] private int lowEndSystemMemoryMb = 3000;
    [SerializeField] private int lowEndProcessorCount = 4;

    // Cache cÃ¡c popup instances currently opened
    private List<BasePopUp> openedPopups = new List<BasePopUp>();
    private readonly Dictionary<string, GameObject> popupPrefabLookup = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, BasePopUp> openedPopupLookup = new Dictionary<string, BasePopUp>(StringComparer.Ordinal);
    private readonly Dictionary<string, BasePopUp> popupInstanceCache = new Dictionary<string, BasePopUp>(StringComparer.Ordinal);
    private readonly Dictionary<GameObject, GameObject> loadingInstanceCache = new Dictionary<GameObject, GameObject>();

    // Current UI instances
    private GameObject currentInGameUI;
    private GameObject currentMenuUI;
    private GameObject currentLoadingUI;
    private GameObject currentLoadingPrefab;
    private GameObject currentWinUI;
    private GameObject currentLoseUI;
    private GameObject currentGetMoreLiveUI;
    private Coroutine pendingDeferredActionRoutine;
    private Coroutine pendingInGameUIIntroRoutine;
    private Tween loadingUI2AutoHideTween;
    private Tween loadingUI2FadeTween;

    // Track UI state hiá»‡n táº¡i
    private UIState currentState = UIState.None;
    private bool useUILiteMode;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        useUILiteMode = ShouldUseLiteMode();

        // Äáº£m báº£o UI container tá»“n táº¡i
        if (uiContainer == null)
        {
            GameObject container = new GameObject("UIContainer");
            container.transform.SetParent(transform);
            uiContainer = container.transform;
        }

        // Äáº£m báº£o popup container tá»“n táº¡i
        if (popupContainer == null)
        {
            GameObject container = new GameObject("PopupContainer");
            container.transform.SetParent(transform);
            popupContainer = container.transform;
        }

        RebuildPopupPrefabLookup();
        Init();
    }

    private void OnDestroy()
    {
        // Cleanup UI instances
        HideAllUI();

        // Force release caches when UIManager is destroyed.
        DestroyCachedPopupInstances();
    }
    public void Init()
    {
        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);

        if (TryAutoStartFirstLevel(currentLevel))
        {
            return;
        }

        // Note: popups are instantiated/destroyed on demand now (no preload)
        // Load MenuUI ban Ä‘áº§u cho level 2+.
        LoadMenuUI();
        ShowLoadingUI();
    
    }

    private bool TryAutoStartFirstLevel(int currentLevel)
    {
        if (currentLevel != 1)
        {
            return false;
        }

        if (gamePlayController == null)
        {
            gamePlayController = GamePlayController.Instance;
        }

        if (gamePlayController == null)
        {
            ;
            return false;
        }

        // New player flow: vao thang level 1 va van dung loadingUI1.
        ShowLoadingUI();

        if (pendingDeferredActionRoutine != null)
        {
            StopCoroutine(pendingDeferredActionRoutine);
            pendingDeferredActionRoutine = null;
        }

        pendingDeferredActionRoutine = StartCoroutine(RunDeferredActionFirstLevel(() =>
        {
            LoadInGameUI();
            ShowLoadingUI();
            gamePlayController.InitLevel(1);
        }));

        return true;
    }

    // ========= LOAD UI GROUPS =========

    public void LoadInGameUI()
    {
        if (currentState == UIState.InGame && currentInGameUI != null && currentInGameUI.activeSelf) return;

        // âœ… Destroy MenuUI náº¿u Ä‘ang tá»“n táº¡i
        if (currentMenuUI != null)
        {
            Destroy(currentMenuUI);
            currentMenuUI = null;
        }

        HideWinUI();
        HideLoseUI();
        HideGetMoreLiveUI();

        // âœ… Instantiate InGameUI náº¿u chÆ°a cÃ³
        if (currentInGameUI == null && inGameUIPrefab != null)
        {
            currentInGameUI = Instantiate(inGameUIPrefab, uiContainer);
        }

        if (currentInGameUI != null)
        {
            if (!currentInGameUI.activeSelf)
            {
                currentInGameUI.SetActive(true);
            }

            // âœ… Initialize UI sau khi ensure active
            InGameUIManager inGameUIManager = currentInGameUI.GetComponent<InGameUIManager>();
            if (inGameUIManager != null)            
            {
                inGameUIManager.Init(gamePlayController);
            }

            if (pendingInGameUIIntroRoutine != null)
            {
                StopCoroutine(pendingInGameUIIntroRoutine);
            }

            pendingInGameUIIntroRoutine = StartCoroutine(PlayInGameUIIntroAfterLoadingComplete(inGameUIManager));
        }

        currentState = UIState.InGame;
        EnsureLoadingUIOnTop();
    }

    public void LoadMenuUI()
    {
        if (currentState == UIState.Menu) return;

        // âœ… Clear game data trÆ°á»›c khi vá» menu
   

        // âœ… Reset combo vÃ  fill amount vá» 0

        // âœ… Destroy/Hide InGameUI náº¿u Ä‘ang tá»“n táº¡i
        if (currentInGameUI != null)
        {
            if (keepInGameUIInstanceOnMenu)
            {
                currentInGameUI.SetActive(false);
            }
            else
            {
                Destroy(currentInGameUI);
                currentInGameUI = null;
            }
        }

        CloseAllPopups(); // ÄÃ³ng táº¥t cáº£ popup khi vá» menu

        // âœ… Instantiate MenuUI náº¿u chÆ°a cÃ³
        if (currentMenuUI == null && menuUIPrefab != null)
        {
            currentMenuUI = Instantiate(menuUIPrefab, uiContainer);

            // âœ… Initialize UI sau khi instantiate
            if (useUILiteMode)
            {
                EnsureCanvasGroupVisible(currentMenuUI);
            }
            else
            {
                AnimateUIEntry(currentMenuUI);
            }
            //SpawnMenuLoadingUI();
        }

        currentState = UIState.Menu;
        EnsureLoadingUIOnTop();
    }


    public void HideAllUI()
    {
        CancelLoadingUI2AutoHide();

        if (pendingInGameUIIntroRoutine != null)
        {
            StopCoroutine(pendingInGameUIIntroRoutine);
            pendingInGameUIIntroRoutine = null;
        }

        CloseAllPopups();
        openedPopups.Clear();
        openedPopupLookup.Clear();
        if (!keepPopupCacheOnHideAll)
        {
            DestroyCachedPopupInstances();
        }

        // âœ… Destroy táº¥t cáº£ UI instances
        if (currentMenuUI != null)
        {
            Destroy(currentMenuUI);
            currentMenuUI = null;
        }

        DestroyCachedLoadingInstances();
        currentLoadingUI = null;
        currentLoadingPrefab = null;

        if (currentInGameUI != null)
        {
            Destroy(currentInGameUI);
            currentInGameUI = null;
        }

        HideWinUI();
        HideLoseUI();
        HideGetMoreLiveUI();

        currentState = UIState.None;
        EnsureLoadingUIOnTop();
    }

    public void ShowLoadingUI()
    {
        CancelLoadingUI2AutoHide();
        ShowLoadingUIPrefab(loadingUIPrefab, "loadingUIPrefab");
    }

    public void ShowLoadingUI2(bool autoHide = true)
    {
        GameObject targetPrefab = loadingUI2Prefab != null ? loadingUI2Prefab : loadingUIPrefab;
        ShowLoadingUIPrefab(targetPrefab, "loadingUI2Prefab");

        if (autoHide)
        {
            ScheduleLoadingUI2AutoHide(currentLoadingUI);
        }
        else
        {
            CancelLoadingUI2AutoHide();
        }
    }

    public void ShowLoadingAndRunNextFrame(Action action, bool autoHideLoadingUI2 = true)
    {
        ShowLoadingUI2(autoHideLoadingUI2);

        if (pendingDeferredActionRoutine != null)
        {
            StopCoroutine(pendingDeferredActionRoutine);
            pendingDeferredActionRoutine = null;
        }

        pendingDeferredActionRoutine = StartCoroutine(RunDeferredActionNextFrame(action));
    }

    private IEnumerator RunDeferredActionNextFrame(Action action)
    {
        yield return null;
        pendingDeferredActionRoutine = null;
        action?.Invoke();
    }

    private IEnumerator RunDeferredActionFirstLevel(Action action)
    {
        PrewarmTutorialForFirstLevelLoading();

        yield return null;

        float delay = Mathf.Max(0f, firstLevelInitDelay);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        pendingDeferredActionRoutine = null;
        action?.Invoke();
    }

    private void PrewarmTutorialForFirstLevelLoading()
    {
        TutorialManager tutorialManager = TutorialManager.Instance;

        if (tutorialManager == null && tutorialPrefab != null)
        {
            GameObject tutorialGO = Instantiate(tutorialPrefab);
            tutorialManager = tutorialGO.GetComponent<TutorialManager>();
            if (tutorialManager == null)
            {
                tutorialManager = tutorialGO.GetComponentInChildren<TutorialManager>(true);
            }
        }

        tutorialManager?.PrewarmForLoadingPhase();
    }

    private void SpawnMenuLoadingUI()
    {
        ShowLoadingUI();
    }

    private void ShowLoadingUIPrefab(GameObject prefab, string prefabLabel)
    {
        Profiler.BeginSample("UIManager.ShowLoadingUIPrefab");

        if (prefab == null)
        {
            ;
            Profiler.EndSample();
            return;
        }

        if (reuseLoadingUIWhenSamePrefab && currentLoadingUI != null && currentLoadingPrefab == prefab)
        {
            if (restartLoadingAnimationOnReuse && currentLoadingUI.activeSelf)
            {
                currentLoadingUI.SetActive(false);
                currentLoadingUI.SetActive(true);
            }
            else if (!currentLoadingUI.activeSelf)
            {
                currentLoadingUI.SetActive(true);
            }

            EnsureLoadingUICanvasVisible(currentLoadingUI);

            EnsureLoadingUIOnTop();
            Profiler.EndSample();
            return;
        }

        GameObject nextLoadingUI = GetOrCreateLoadingInstance(prefab);
        if (nextLoadingUI == null)
        {
            Profiler.EndSample();
            return;
        }

        if (currentLoadingUI != null && currentLoadingUI != nextLoadingUI)
        {
            if (cacheLoadingUIInstancesByPrefab)
            {
                if (currentLoadingUI.activeSelf)
                {
                    currentLoadingUI.SetActive(false);
                }
            }
            else
            {
                Destroy(currentLoadingUI);
            }
        }

        currentLoadingUI = nextLoadingUI;
        currentLoadingPrefab = prefab;

        if (restartLoadingAnimationOnReuse && currentLoadingUI.activeSelf)
        {
            currentLoadingUI.SetActive(false);
            currentLoadingUI.SetActive(true);
        }
        else if (!currentLoadingUI.activeSelf)
        {
            currentLoadingUI.SetActive(true);
        }

        EnsureLoadingUICanvasVisible(currentLoadingUI);

        EnsureLoadingUIOnTop();
        Profiler.EndSample();
    }

    private static void EnsureLoadingUICanvasVisible(GameObject loadingUI)
    {
        if (loadingUI == null)
        {
            return;
        }

        CanvasGroup canvasGroup = loadingUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.DOKill(false);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void EnsureLoadingUIOnTop()
    {
        if (currentLoadingUI == null || uiContainer == null)
        {
            return;
        }

        if (currentLoadingUI.transform.parent != uiContainer)
        {
            currentLoadingUI.transform.SetParent(uiContainer, false);
        }

        if (currentLoadingUI.transform.GetSiblingIndex() != uiContainer.childCount - 1)
        {
            currentLoadingUI.transform.SetAsLastSibling();
        }
    }

    public void SpawnWinUI()
    {
        HideLoseUI();

        if (currentWinUI != null)
        {
            if (!currentWinUI.activeSelf)
            {
                currentWinUI.SetActive(true);
                if (useUILiteMode)
                {
                    EnsureCanvasGroupVisible(currentWinUI);
                }
                else
                {
                    AnimateUIEntry(currentWinUI);
                }
            }

            currentWinUI.transform.SetAsLastSibling();
            EnsureLoadingUIOnTop();
            return;
        }

        if (winUIPrefab == null)
        {
            ;
            return;
        }

        currentWinUI = Instantiate(winUIPrefab, uiContainer);
        currentWinUI.transform.SetAsLastSibling();

        if (useUILiteMode)
        {
            EnsureCanvasGroupVisible(currentWinUI);
        }
        else
        {
            AnimateUIEntry(currentWinUI);
        }

        EnsureLoadingUIOnTop();
    }

    public void SpawnLoseUI()
    {
        HideWinUI();

        if (currentLoseUI != null)
        {
            if (!currentLoseUI.activeSelf)
            {
                currentLoseUI.SetActive(true);
                if (useUILiteMode)
                {
                    EnsureCanvasGroupVisible(currentLoseUI);
                }
                else
                {
                    AnimateUIEntry(currentLoseUI);
                }
            }

            currentLoseUI.transform.SetAsLastSibling();
            EnsureLoadingUIOnTop();
            return;
        }

        if (loseUIPrefab == null)
        {
            ;
            return;
        }

        currentLoseUI = Instantiate(loseUIPrefab, uiContainer);
        currentLoseUI.transform.SetAsLastSibling();

        if (useUILiteMode)
        {
            EnsureCanvasGroupVisible(currentLoseUI);
        }
        else
        {
            AnimateUIEntry(currentLoseUI);
        }

        EnsureLoadingUIOnTop();
    }

    public void SpawnGetMoreLiveUI()
    {
        if (currentGetMoreLiveUI != null)
        {
            if (!currentGetMoreLiveUI.activeSelf)
            {
                currentGetMoreLiveUI.SetActive(true);
                if (useUILiteMode)
                {
                    EnsureCanvasGroupVisible(currentGetMoreLiveUI);
                }
                else
                {
                    AnimateUIEntry(currentGetMoreLiveUI);
                }
            }

            currentGetMoreLiveUI.transform.SetAsLastSibling();
            EnsureLoadingUIOnTop();
            return;
        }

        if (getMoreLiveUIPrefab == null)
        {
            ;
            ShowPopup(Const.buyMoreLivesPopUp);
            return;
        }

        currentGetMoreLiveUI = Instantiate(getMoreLiveUIPrefab, uiContainer);
        currentGetMoreLiveUI.transform.SetAsLastSibling();

        if (useUILiteMode)
        {
            EnsureCanvasGroupVisible(currentGetMoreLiveUI);
        }
        else
        {
            AnimateUIEntry(currentGetMoreLiveUI);
        }

        EnsureLoadingUIOnTop();
    }

    public void HideWinUI()
    {
        if (currentWinUI == null)
            return;

        if (keepResultUIInstances)
        {
            if (currentWinUI.activeSelf)
            {
                currentWinUI.SetActive(false);
            }
        }
        else
        {
            Destroy(currentWinUI);
            currentWinUI = null;
        }
    }

    public void HideLoseUI()
    {
        if (currentLoseUI == null)
            return;

        if (keepResultUIInstances)
        {
            if (currentLoseUI.activeSelf)
            {
                currentLoseUI.SetActive(false);
            }
        }
        else
        {
            Destroy(currentLoseUI);
            currentLoseUI = null;
        }
    }

    public void HideGetMoreLiveUI()
    {
        if (currentGetMoreLiveUI == null)
            return;

        if (keepResultUIInstances)
        {
            if (currentGetMoreLiveUI.activeSelf)
            {
                currentGetMoreLiveUI.SetActive(false);
            }
        }
        else
        {
            Destroy(currentGetMoreLiveUI);
            currentGetMoreLiveUI = null;
        }
    }

    public void HideInGameUIImmediate()
    {
        if (currentInGameUI == null)
            return;

        if (keepInGameUIInstanceOnMenu)
        {
            currentInGameUI.SetActive(false);
        }
        else
        {
            Destroy(currentInGameUI);
            currentInGameUI = null;
        }
    }

    public void ReturnToMenuAndClearAllUI()
    {
        CancelLoadingUI2AutoHide();

        if (pendingInGameUIIntroRoutine != null)
        {
            StopCoroutine(pendingInGameUIIntroRoutine);
            pendingInGameUIIntroRoutine = null;
        }

        CloseAllPopups();

        HideInGameUIImmediate();

        if (currentLoadingUI != null)
        {
            if (cacheLoadingUIInstancesByPrefab)
            {
                currentLoadingUI.SetActive(false);
            }
            else
            {
                Destroy(currentLoadingUI);
            }
            currentLoadingUI = null;
            currentLoadingPrefab = null;
        }

        HideWinUI();
        HideLoseUI();
        HideGetMoreLiveUI();

        if (currentMenuUI != null)
        {
            Destroy(currentMenuUI);
            currentMenuUI = null;
        }

        currentState = UIState.None;
        LoadMenuUI();
        EnsureLoadingUIOnTop();
    }

    // ========= POPUP MANAGEMENT =========

    public void ShowPopup(string popupName, Action<BasePopUp> onComplete = null, bool ignoreComplete = false)
    {
        if (string.IsNullOrEmpty(popupName))
        {
            return;
        }

        if (!popupPrefabLookup.TryGetValue(popupName, out GameObject prefab) || prefab == null)
        {
            RebuildPopupPrefabLookup();
            popupPrefabLookup.TryGetValue(popupName, out prefab);
        }

        if (prefab == null)
        {
            ;
            return;
        }

        if (openedPopupLookup.TryGetValue(popupName, out BasePopUp openedPopup) && openedPopup != null && openedPopup.gameObject.activeSelf)
        {
            openedPopup.transform.SetAsLastSibling();
            onComplete?.Invoke(openedPopup);
            EnsureLoadingUIOnTop();
            return;
        }

        BasePopUp popup = GetOrCreatePopupInstance(popupName, prefab);
        if (popup == null)
        {
            return;
        }

        popup.transform.SetAsLastSibling();

        // Track opened popup
        if (!openedPopups.Contains(popup))
        {
            openedPopups.Add(popup);
        }

        openedPopupLookup[popupName] = popup;

        // Show popup with animation
        if (ignoreComplete)
        {
            popup.Show(() => onComplete?.Invoke(popup));
        }
        else
        {
            popup.Show();
            onComplete?.Invoke(popup);
        }

        EnsureLoadingUIOnTop();
    }

    public void HidePopup(string popupName, bool destroyAfterHide = false)
    {
        BasePopUp popup = null;
        if (!string.IsNullOrEmpty(popupName))
        {
            openedPopupLookup.TryGetValue(popupName, out popup);
        }

        if (popup == null)
        {
            popup = openedPopups.Find(p => p != null && p.gameObject.name == popupName);
        }

        if (popup == null) return;

        if (cachePopupInstances)
        {
            popup.SetDestroyOnHide(destroyAfterHide);
            if (destroyAfterHide)
            {
                popupInstanceCache.Remove(popupName);
            }
        }

        popup.Hide(() =>
        {
            openedPopups.Remove(popup);
            if (!string.IsNullOrEmpty(popupName) && openedPopupLookup.TryGetValue(popupName, out BasePopUp mapped) && mapped == popup)
            {
                openedPopupLookup.Remove(popupName);
            }
            EnsureLoadingUIOnTop();
        });
    }

    public void CloseAllPopups()
    {
        // Hide (and destroy) all opened popups
        var snapshot = new List<BasePopUp>(openedPopups);
        foreach (var popup in snapshot)
        {
            if (popup != null && popup.gameObject.activeSelf)
            {
                popup.Hide(() =>
                {
                    openedPopups.Remove(popup);
                    if (popup != null)
                    {
                        string popupKey = popup.gameObject.name;
                        if (!string.IsNullOrEmpty(popupKey) && openedPopupLookup.TryGetValue(popupKey, out BasePopUp mapped) && mapped == popup)
                        {
                            openedPopupLookup.Remove(popupKey);
                        }
                    }
                });
            }
        }

        openedPopups.Clear();
        openedPopupLookup.Clear();

        EnsureLoadingUIOnTop();
    }

    public T GetPopup<T>(string popupName) where T : BasePopUp
    {
        BasePopUp popup = null;
        if (!string.IsNullOrEmpty(popupName))
        {
            openedPopupLookup.TryGetValue(popupName, out popup);
        }

        if (popup == null)
        {
            popup = openedPopups.Find(p => p != null && p.gameObject.name == popupName);
        }

        return popup as T;
    }

    // ========= ANIMATION HELPERS =========

    private void AnimateUIEntry(GameObject ui)
    {
        // Simple fade in animation
        CanvasGroup canvasGroup = ui.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = ui.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void EnsureCanvasGroupVisible(GameObject ui)
    {
        if (ui == null)
        {
            return;
        }

        CanvasGroup canvasGroup = ui.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = ui.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private bool ShouldUseLiteMode()
    {
        if (!enableLowEndUILiteMode)
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

    private void RebuildPopupPrefabLookup()
    {
        popupPrefabLookup.Clear();
        for (int i = 0; i < popupPrefabs.Count; i++)
        {
            GameObject popupPrefab = popupPrefabs[i];
            if (popupPrefab == null || string.IsNullOrEmpty(popupPrefab.name))
            {
                continue;
            }

            popupPrefabLookup[popupPrefab.name] = popupPrefab;
        }
    }

    private IEnumerator PlayInGameUIIntroAfterLoadingComplete(InGameUIManager inGameUIManager)
    {
        yield return null;

        if (currentInGameUI == null)
        {
            pendingInGameUIIntroRoutine = null;
            yield break;
        }

        AnimateUIEntry(currentInGameUI);
        inGameUIManager?.PlayHUDIntroAfterLoadingReady();
        pendingInGameUIIntroRoutine = null;
    }

    public bool IsLoadingUIActive()
    {
        return currentLoadingUI != null && currentLoadingUI.activeInHierarchy;
    }

    private void ScheduleLoadingUI2AutoHide(GameObject targetLoadingUI)
    {
        if (targetLoadingUI == null)
        {
            return;
        }

        CancelLoadingUI2AutoHide();
        float delay = Mathf.Max(0f, loadingUI2AutoHideDelay);
        float fadeDuration = Mathf.Max(0.05f, loadingUI2FadeDuration);

        loadingUI2AutoHideTween = DOVirtual.DelayedCall(delay, () =>
        {
            if (targetLoadingUI == null || !targetLoadingUI.activeInHierarchy)
            {
                return;
            }

            CanvasGroup canvasGroup = targetLoadingUI.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = targetLoadingUI.AddComponent<CanvasGroup>();
            }

            canvasGroup.DOKill(false);
            if (canvasGroup.alpha <= 0.001f)
            {
                canvasGroup.alpha = 1f;
            }

            loadingUI2FadeTween = canvasGroup
                .DOFade(0f, fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (targetLoadingUI != null)
                    {
                        targetLoadingUI.SetActive(false);
                    }

                    loadingUI2FadeTween = null;
                });
        }).SetUpdate(true);
    }

    private void CancelLoadingUI2AutoHide()
    {
        if (loadingUI2AutoHideTween != null && loadingUI2AutoHideTween.IsActive())
        {
            loadingUI2AutoHideTween.Kill();
        }

        loadingUI2AutoHideTween = null;

        if (loadingUI2FadeTween != null && loadingUI2FadeTween.IsActive())
        {
            loadingUI2FadeTween.Kill();
        }

        loadingUI2FadeTween = null;

        if (currentLoadingUI != null && currentLoadingUI.activeInHierarchy)
        {
            EnsureLoadingUICanvasVisible(currentLoadingUI);
        }
    }

    // ========= UTILITY =========

    public bool IsPopupActive(string popupName)
    {
        return openedPopups.Exists(p => p != null && p.gameObject.name == popupName && p.gameObject.activeSelf);
    }

    public UIState GetCurrentState()
    {
        return currentState;
    }

    private GameObject GetOrCreateLoadingInstance(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (cacheLoadingUIInstancesByPrefab)
        {
            if (loadingInstanceCache.TryGetValue(prefab, out GameObject cached) && cached != null)
            {
                if (cached.transform.parent != uiContainer)
                {
                    cached.transform.SetParent(uiContainer, false);
                }

                return cached;
            }
        }

        GameObject created = Instantiate(prefab, uiContainer);

        if (cacheLoadingUIInstancesByPrefab)
        {
            loadingInstanceCache[prefab] = created;
        }

        return created;
    }

    private BasePopUp GetOrCreatePopupInstance(string popupName, GameObject prefab)
    {
        if (cachePopupInstances && popupInstanceCache.TryGetValue(popupName, out BasePopUp cachedPopup))
        {
            if (cachedPopup != null)
            {
                if (cachedPopup.transform.parent != popupContainer)
                {
                    cachedPopup.transform.SetParent(popupContainer, false);
                }

                return cachedPopup;
            }

            popupInstanceCache.Remove(popupName);
        }

        GameObject popupObj = Instantiate(prefab, popupContainer);
        popupObj.name = prefab.name;

        BasePopUp popup = popupObj.GetComponent<BasePopUp>();
        if (popup == null)
        {
            Destroy(popupObj);
            return null;
        }

        if (cachePopupInstances)
        {
            popup.SetDestroyOnHide(false);
            popupInstanceCache[popupName] = popup;
        }

        return popup;
    }

    private void DestroyCachedPopupInstances()
    {
        foreach (var entry in popupInstanceCache)
        {
            BasePopUp popup = entry.Value;
            if (popup != null)
            {
                Destroy(popup.gameObject);
            }
        }

        popupInstanceCache.Clear();
    }

    private void DestroyCachedLoadingInstances()
    {
        if (cacheLoadingUIInstancesByPrefab)
        {
            foreach (var entry in loadingInstanceCache)
            {
                GameObject loadingInstance = entry.Value;
                if (loadingInstance != null)
                {
                    Destroy(loadingInstance);
                }
            }

            loadingInstanceCache.Clear();
        }
        else if (currentLoadingUI != null)
        {
            Destroy(currentLoadingUI);
        }
    }
}

// ========= DATA CLASSES =========

public enum UIState
{
    None,
    Menu,
    InGame
}


