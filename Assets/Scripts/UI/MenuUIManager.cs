using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class MenuUIManager : MonoBehaviour
{
    public static MenuUIManager Instance { get; private set; }

    private const int MaxHearts = 5;
    private const int HeartRegenSeconds = 60;

    [SerializeField] private Button settingButton;
    [SerializeField] private Button buyHeartButton;
    [SerializeField] private Button buyCoinButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Text heartTimeText;
    [SerializeField] private Text coinsText;
    [SerializeField] private Text heartsText;
    [SerializeField] private Text[] levelTexts; // Text hiển thị số level ở mỗi page, sắp xếp theo thứ tự page
    [SerializeField] private GamePlayController gamePlayController;
    private Coroutine heartRegenCoroutine;
    private readonly WaitForSecondsRealtime heartTickInterval = new WaitForSecondsRealtime(1f);
    private int cachedCoinValue = int.MinValue;
    private int cachedHeartValue = int.MinValue;
    private string cachedHeartTimerText = string.Empty;

    void Start()
    {
        Instance = this;

        if (gamePlayController == null)
        {
            gamePlayController = GamePlayController.Instance;
        }

        SetButton();
        ProcessOfflineHeartRegen();
        UpdateCoinsAndHearts();
        StartHeartRegenLoop();
    }

    void OnEnable()
    {
        Instance = this;
        ProcessOfflineHeartRegen();
        UpdateCoinsAndHearts();
        StartHeartRegenLoop();
        UpdateLevel();
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (heartRegenCoroutine != null)
        {
            StopCoroutine(heartRegenCoroutine);
            heartRegenCoroutine = null;
        }
    }

    private void SetButton()
    {
        if (settingButton != null)
        {
            ButtonAnimHelper.AddScaleAnimation(settingButton);
            settingButton.onClick.RemoveListener(OnSettingButtonClicked);
            settingButton.onClick.AddListener(OnSettingButtonClicked);
        }
        if (buyHeartButton != null)
        {
            buyHeartButton.onClick.RemoveListener(OnBuyHeartButtonClicked);
            buyHeartButton.onClick.AddListener(OnBuyHeartButtonClicked);
        }
        if (buyCoinButton != null)
        {
            buyCoinButton.onClick.RemoveListener(OnBuyCoinButtonClicked);
            buyCoinButton.onClick.AddListener(OnBuyCoinButtonClicked);
        }
        if (playButton != null)
        {
            ButtonAnimHelper.AddScaleAnimation(playButton);
            playButton.onClick.RemoveListener(OnPlayButtonClicked);
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (settingButton != null)
        {
            settingButton.onClick.RemoveListener(OnSettingButtonClicked);
        }

        if (buyHeartButton != null)
        {
            buyHeartButton.onClick.RemoveListener(OnBuyHeartButtonClicked);
        }

        if (buyCoinButton != null)
        {
            buyCoinButton.onClick.RemoveListener(OnBuyCoinButtonClicked);
        }

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayButtonClicked);
        }
    }

    private void OnSettingButtonClicked()
    {
        UIManager.Instance?.ShowPopup(Const.settingMenuPopUp);
    }

    private void OnBuyHeartButtonClicked()
    {
        UIManager.Instance?.ShowPopup(Const.buyMoreLivesPopUp);
    }

    private void OnBuyCoinButtonClicked()
    {
        UIManager.Instance?.ShowPopup(Const.buyMoreGoldPopUp);
    }

    private void OnPlayButtonClicked()
    {
        if (playButton != null)
        {
            playButton.interactable = false;
        }

        if (PlayerPrefs.GetInt(Const.player_hearts_key, Const.player_default_hearts) <= 0)
        {
            UIManager.Instance?.ShowPopup(Const.buyMoreLivesPopUp);
            if (playButton != null)
            {
                playButton.interactable = true;
            }
            return;
        }

        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        if (gamePlayController == null)
        {
            gamePlayController = GamePlayController.Instance;
        }

        if (gamePlayController == null)
        {
            if (playButton != null)
            {
                playButton.interactable = true;
            }
            return;
        }

        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLoadingAndRunNextFrame(() =>
            {
                UIManager.Instance.LoadInGameUI();
                UIManager.Instance.ShowLoadingUI2();
                gamePlayController.InitLevel(currentLevel);
            });
        }
    }

    public void UpdateCoinsAndHearts()
    {
        SetCoinTextIfChanged(PlayerPrefs.GetInt(Const.player_coins_key, 0));

        int hearts = ClampAndSaveHearts(PlayerPrefs.GetInt(Const.player_hearts_key, Const.player_default_hearts));
        SetHeartsTextIfChanged(hearts);

        UpdateHeartTimerUI();
    }

    private void UpdateLevel()
    {
        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        for (int i = 0; i < levelTexts.Length; i++)
        {
            if (i == 0)
            {
                int displayLevel = currentLevel; // Hiển thị level dựa trên currentLevel
                levelTexts[i].text = $"Level {displayLevel}";
            }
            else
            {
                levelTexts[i].text = $"{currentLevel + i}";
            }
        }
    }

    private void StartHeartRegenLoop()
    {
        if (heartRegenCoroutine != null)
        {
            return;
        }

        heartRegenCoroutine = StartCoroutine(HeartRegenLoop());
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
        int hearts = ClampAndSaveHearts(PlayerPrefs.GetInt(Const.player_hearts_key, Const.player_default_hearts));
        int now = GetCurrentUnixTime();
        bool prefsChanged = false;

        if (hearts >= MaxHearts)
        {
            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, 0, 0);
            if (prefsChanged)
            {
                PlayerPrefs.Save();
            }
            return;
        }

        int nextRegenUnix = PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0);
        if (nextRegenUnix <= 0)
        {
            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, now + HeartRegenSeconds, 0);
            if (prefsChanged)
            {
                PlayerPrefs.Save();
            }
            return;
        }

        while (hearts < MaxHearts && now >= nextRegenUnix)
        {
            hearts++;
            nextRegenUnix += HeartRegenSeconds;
        }

        prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_hearts_key, hearts, Const.player_default_hearts);
        prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, hearts >= MaxHearts ? 0 : nextRegenUnix, 0);
        if (prefsChanged)
        {
            PlayerPrefs.Save();
        }
    }

    private void TickHeartRegen()
    {
        int hearts = ClampAndSaveHearts(PlayerPrefs.GetInt(Const.player_hearts_key, Const.player_default_hearts));
        int now = GetCurrentUnixTime();
        bool prefsChanged = false;

        if (hearts >= MaxHearts)
        {
            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, 0, 0);
            if (prefsChanged)
            {
                PlayerPrefs.Save();
            }

            SetHeartsTextIfChanged(hearts);
            SetHeartTimerTextIfChanged("Full");
            return;
        }

        int nextRegenUnix = PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0);
        if (nextRegenUnix <= 0)
        {
            nextRegenUnix = now + HeartRegenSeconds;
            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, nextRegenUnix, 0);
            if (prefsChanged)
            {
                PlayerPrefs.Save();
            }
        }

        if (now >= nextRegenUnix)
        {
            while (hearts < MaxHearts && now >= nextRegenUnix)
            {
                hearts++;
                nextRegenUnix += HeartRegenSeconds;
            }

            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_hearts_key, hearts, Const.player_default_hearts);
            prefsChanged |= SetPlayerPrefIntIfDifferent(Const.player_next_heart_regen_unix_key, hearts >= MaxHearts ? 0 : nextRegenUnix, 0);
            if (prefsChanged)
            {
                PlayerPrefs.Save();
            }
            SetHeartsTextIfChanged(hearts);
            UpdateHeartTimerUI();
            return;
        }

        SetHeartsTextIfChanged(hearts);

        UpdateHeartTimerUI();
    }

    private void UpdateHeartTimerUI()
    {
        if (heartTimeText == null)
        {
            return;
        }

        int hearts = ClampAndSaveHearts(PlayerPrefs.GetInt(Const.player_hearts_key, Const.player_default_hearts));
        if (hearts >= MaxHearts)
        {
            SetHeartTimerTextIfChanged("Full");
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
        SetHeartTimerTextIfChanged($"{minutes:00}:{seconds:00}");
    }

    private int ClampAndSaveHearts(int rawHearts)
    {
        int clamped = Mathf.Clamp(rawHearts, 0, MaxHearts);
        if (clamped != rawHearts && SetPlayerPrefIntIfDifferent(Const.player_hearts_key, clamped, Const.player_default_hearts))
        {
            PlayerPrefs.Save();
        }

        return clamped;
    }

    private int GetCurrentUnixTime()
    {
        return (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void SetCoinTextIfChanged(int coin)
    {
        if (coinsText == null || cachedCoinValue == coin)
        {
            return;
        }

        cachedCoinValue = coin;
        coinsText.text = coin.ToString();
    }

    private void SetHeartsTextIfChanged(int hearts)
    {
        if (heartsText == null || cachedHeartValue == hearts)
        {
            return;
        }

        cachedHeartValue = hearts;
        heartsText.text = hearts.ToString();
    }

    private void SetHeartTimerTextIfChanged(string timerText)
    {
        if (heartTimeText == null || string.Equals(cachedHeartTimerText, timerText, StringComparison.Ordinal))
        {
            return;
        }

        cachedHeartTimerText = timerText;
        heartTimeText.text = timerText;
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