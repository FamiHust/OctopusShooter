using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GetMoreLiveUIManager : MonoBehaviour
{
    private const int HeartRegenSeconds = 60;

    [Header("Buttons")]
    [SerializeField] private Button getBtn;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Button refillBtn;
    [SerializeField] private Text timerTxt;

    [Header("Config")]
    [SerializeField] private int getHeartAmount = 1;
    [SerializeField] private int refillHeartAmount = 5;
    [SerializeField] private int refillCoinCost = 900;
    [SerializeField] private GameObject notePrefab;
    [SerializeField] private float noteClickCooldown = 1f;
    private float nextNoteClickTime;

    private Coroutine heartRegenCoroutine;
    private readonly WaitForSecondsRealtime heartTickInterval = new WaitForSecondsRealtime(1f);
    private string cachedTimerText = string.Empty;

    private void OnEnable()
    {
        AudioManager.Instance?.PlaySFX(Const.noSpaceSFX);
        ProcessOfflineHeartRegen();
        UpdateHeartTimerUI();
        StartHeartRegenLoop();
    }

    private void OnDisable()
    {
        StopHeartRegenLoop();
    }

    private void Awake()
    {
        AutoAssignButtonsIfNeeded();
    }

    private void Start()
    {
        SetupButtonAnimations();

        if (getBtn != null)
        {
            getBtn.onClick.AddListener(OnGetClicked);
        }

        if (cancelBtn != null)
        {
            cancelBtn.onClick.AddListener(OnCancelClicked);
        }

        if (refillBtn != null)
        {
            refillBtn.onClick.AddListener(OnRefillClicked);
        }
    }

    private void SetupButtonAnimations()
    {
        if (getBtn != null)
        {
            ButtonAnimHelper.AddScaleAnimation(getBtn);
        }

        if (refillBtn != null)
        {
            ButtonAnimHelper.AddScaleAnimation(refillBtn);
        }
    }

    private void OnDestroy()
    {
        StopHeartRegenLoop();

        if (getBtn != null)
        {
            getBtn.onClick.RemoveListener(OnGetClicked);
        }

        if (cancelBtn != null)
        {
            cancelBtn.onClick.RemoveListener(OnCancelClicked);
        }

        if (refillBtn != null)
        {
            refillBtn.onClick.RemoveListener(OnRefillClicked);
        }
    }

    private void OnValidate()
    {
        AutoAssignButtonsIfNeeded();
    }

    private void OnGetClicked()
    {
        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        AddHearts(getHeartAmount);
        ReturnToMenu();
    }

    private void OnCancelClicked()
    {
        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        ReturnToMenu();
    }

    private void OnRefillClicked()
    {
        int safeCost = Mathf.Max(0, refillCoinCost);
        if (!TrySpendCoins(safeCost))
        {
            AudioManager.Instance?.PlaySFX(Const.popLockSFX);
            ShowNotEnoughCoinNote();
            return;
        }

        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        AddHearts(refillHeartAmount);
        ReturnToMenu();
    }

    public void ShowNotEnoughCoinNote(Transform customParent = null)
    {
        if (notePrefab == null)
        {
            return;
        }

        if (Time.unscaledTime < nextNoteClickTime)
        {
            return;
        }

        nextNoteClickTime = Time.unscaledTime + Mathf.Max(0.05f, noteClickCooldown);

        Transform parentTransform = customParent != null ? customParent : transform;
        GameObject note = Instantiate(notePrefab, parentTransform);
        RectTransform rt = note.GetComponent<RectTransform>();
        CanvasGroup cg = note.GetComponent<CanvasGroup>();

        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);

        if (rt != null)
        {
            sequence.Append(rt.DOAnchorPosY(300f, 2f).SetEase(Ease.Linear))
                    .Join(rt.DOScale(Vector3.one * 1.2f, 1f).SetEase(Ease.OutBack))
                    .SetLoops(1, LoopType.Yoyo);
        }

        if (cg != null)
        {
            sequence.Join(cg.DOFade(0f, 1f).SetEase(Ease.InCubic).SetDelay(1f));
        }

        sequence.OnComplete(() =>
        {
            if (note != null)
            {
                Destroy(note);
            }
        });
    }

    private void ReturnToMenu()
    {
        GameManager.Instance?.CancelPendingUITransitions();

        GamePlayController gamePlayController = GamePlayController.Instance;
        gamePlayController?.CleanupLevel();

        if (FireRangeDetector.Instance != null)
        {
            FireRangeDetector.Instance.targetsInRange.Clear();
        }

        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.Invoke(GameEventType.OnGamePause, false);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReturnToMenuAndClearAllUI();
        }
    }

    private void AddHearts(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            return;
        }

        int maxHearts = Mathf.Max(1, Const.player_default_hearts);
        int currentHearts = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_hearts_key, maxHearts));
        int nextHearts = Mathf.Clamp(currentHearts + safeAmount, 0, maxHearts);
        bool prefsChanged = false;
        prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_hearts_key, nextHearts, maxHearts);

        int currentUnix = (int)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nextHearts >= maxHearts)
        {
            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, 0, 0);
        }
        else if (PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0) <= 0)
        {
            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, currentUnix + 60, 0);
        }

        if (prefsChanged)
        {
            PlayerPrefs.Save();
        }
    }

    private bool TrySpendCoins(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            return true;
        }

        int currentCoins = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_coins_key, 0));
        if (currentCoins < safeAmount)
        {
            return false;
        }

        int nextCoins = currentCoins - safeAmount;
        bool prefsChanged = SetPlayerPrefIntIfDifferent(Const.player_coins_key, nextCoins, 0);
        SyncCoinBalanceToPlayerData(nextCoins);

        if (prefsChanged)
        {
            PlayerPrefs.Save();
        }
        return true;
    }

    private void SyncCoinBalanceToPlayerData(int targetCoin)
    {
        if (PlayerData.Instance == null)
        {
            return;
        }

        int safeCoin = Mathf.Max(0, targetCoin);
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

    private void AutoAssignButtonsIfNeeded()
    {
        if (getBtn == null)
        {
            getBtn = FindButtonByNameToken("GetBtn");
        }

        if (cancelBtn == null)
        {
            cancelBtn = FindButtonByNameToken("CancelBtn");
        }

        if (refillBtn == null)
        {
            refillBtn = FindButtonByNameToken("RefillBtn");
        }

        if (timerTxt == null)
        {
            timerTxt = FindTextByNameToken("HeartTimeTxt");
        }

        if (timerTxt == null)
        {
            timerTxt = FindTextByNameToken("Timer");
        }
    }

    private Button FindButtonByNameToken(string token)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (button.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return button;
            }
        }

        return null;
    }

    private Text FindTextByNameToken(string token)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (text.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }
        }

        return null;
    }

    private void StartHeartRegenLoop()
    {
        if (heartRegenCoroutine != null)
        {
            return;
        }

        heartRegenCoroutine = StartCoroutine(HeartRegenLoop());
    }

    private void StopHeartRegenLoop()
    {
        if (heartRegenCoroutine == null)
        {
            return;
        }

        StopCoroutine(heartRegenCoroutine);
        heartRegenCoroutine = null;
    }

    private IEnumerator HeartRegenLoop()
    {
        while (true)
        {
            TickHeartRegen();
            yield return heartTickInterval;
        }
    }

    private void ProcessOfflineHeartRegen()
    {
        int maxHearts = Mathf.Max(1, Const.player_default_hearts);
        int hearts = ClampAndSaveHearts(PlayerPrefs.GetInt(Const.player_hearts_key, maxHearts));
        int now = GetCurrentUnixTime();
        bool changed = false;

        if (hearts >= maxHearts)
        {
            changed |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, 0, 0);
            if (changed)
            {
                PlayerPrefs.Save();
            }
            return;
        }

        int nextRegenUnix = PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0);
        if (nextRegenUnix <= 0)
        {
            changed |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, now + HeartRegenSeconds, 0);
            if (changed)
            {
                PlayerPrefs.Save();
            }
            return;
        }

        while (hearts < maxHearts && now >= nextRegenUnix)
        {
            hearts++;
            nextRegenUnix += HeartRegenSeconds;
            changed = true;
        }

        if (changed)
        {
            SetPlayerPrefIntIfDifferent(Const.player_hearts_key, hearts, maxHearts);
            SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, hearts >= maxHearts ? 0 : nextRegenUnix, 0);
            PlayerPrefs.Save();
        }
    }

    private void TickHeartRegen()
    {
        int maxHearts = Mathf.Max(1, Const.player_default_hearts);
        int hearts = ClampAndSaveHearts(PlayerPrefs.GetInt(Const.player_hearts_key, maxHearts));
        int now = GetCurrentUnixTime();
        bool changed = false;

        if (hearts >= maxHearts)
        {
            changed |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, 0, 0);
            if (changed)
            {
                PlayerPrefs.Save();
            }

            UpdateHeartTimerUI();
            return;
        }

        int nextRegenUnix = PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0);
        if (nextRegenUnix <= 0)
        {
            nextRegenUnix = now + HeartRegenSeconds;
            changed |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, nextRegenUnix, 0);
            if (changed)
            {
                PlayerPrefs.Save();
            }
        }

        if (now >= nextRegenUnix)
        {
            while (hearts < maxHearts && now >= nextRegenUnix)
            {
                hearts++;
                nextRegenUnix += HeartRegenSeconds;
            }

            changed |= SetPlayerPrefIntIfDifferent(Const.player_hearts_key, hearts, maxHearts);
            changed |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, hearts >= maxHearts ? 0 : nextRegenUnix, 0);
            if (changed)
            {
                PlayerPrefs.Save();
            }
        }

        UpdateHeartTimerUI();
    }

    private void UpdateHeartTimerUI()
    {
        if (timerTxt == null)
        {
            return;
        }

        int maxHearts = Mathf.Max(1, Const.player_default_hearts);
        int hearts = ClampAndSaveHearts(PlayerPrefs.GetInt(Const.player_hearts_key, maxHearts));
        if (hearts >= maxHearts)
        {
            SetTimerTextIfChanged("Full");
            return;
        }

        int now = GetCurrentUnixTime();
        int nextRegenUnix = PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0);
        if (nextRegenUnix <= 0)
        {
            nextRegenUnix = now + HeartRegenSeconds;
            if (SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, nextRegenUnix, 0))
            {
                PlayerPrefs.Save();
            }
        }

        int remaining = Mathf.Max(0, nextRegenUnix - now);
        int minutes = remaining / 60;
        int seconds = remaining % 60;
        SetTimerTextIfChanged($"{minutes:00}:{seconds:00}");
    }

    private int ClampAndSaveHearts(int rawHearts)
    {
        int maxHearts = Mathf.Max(1, Const.player_default_hearts);
        int clamped = Mathf.Clamp(rawHearts, 0, maxHearts);
        if (clamped != rawHearts && SetPlayerPrefIntIfDifferent(Const.player_hearts_key, clamped, maxHearts))
        {
            PlayerPrefs.Save();
        }

        return clamped;
    }

    private int GetCurrentUnixTime()
    {
        return (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void SetTimerTextIfChanged(string timerText)
    {
        if (timerTxt == null || string.Equals(cachedTimerText, timerText, StringComparison.Ordinal))
        {
            return;
        }

        cachedTimerText = timerText;
        timerTxt.text = timerText;
    }

    private static bool SetPlayerPrefIntIfDifferent(string key, int value, int defaultValue)
    {
        int current = PlayerPrefs.GetInt(key, defaultValue);
        if (current == value)
        {
            return false;
        }

        PlayerPrefs.SetInt(key, value);
        return true;
    }
}
