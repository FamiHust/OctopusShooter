using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Solo.MOST_IN_ONE;

public class SettingPopUp : BasePopUp
{
    [Header("Setting Buttons")]
    [SerializeField] private Button soundBtn;
    [SerializeField] private Button musicBtn;
    [SerializeField] private Button vibrationBtn;
    [SerializeField] private Button homeBtn;
    [SerializeField] private Button restartBtn;

    [Header("Icons Image")]
    [SerializeField] private Image soundIcon;
    [SerializeField] private Image musicIcon;
    [SerializeField] private Image vibrationIcon;
    [Header("Sprites Setting")]
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;
    [SerializeField] private Sprite vibrationOnSprite;
    [SerializeField] private Sprite vibrationOffSprite;

    private bool isInGameSettingPopup;
    private bool keepPauseWhenClosingForRestart;

    protected override void Awake()
    {
        base.Awake();
        ResolveSettingPopupType();
    }

    void Start()
    {
        ResolveSettingPopupType();

        SetButtonToggle();
        SetIcon();
        SetButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (isInGameSettingPopup && GameEventHub.Instance != null)
        {
            GameEventHub.Instance.Invoke(GameEventType.OnGamePause, true);
        }
    }

    protected override void OnDisable()
    {
        if (isInGameSettingPopup && GameEventHub.Instance != null && !keepPauseWhenClosingForRestart)
        {
            GameEventHub.Instance.Invoke(GameEventType.OnGamePause, false);
        }

        base.OnDisable();
    }

    private void ResolveSettingPopupType()
    {
        isInGameSettingPopup = homeBtn != null && restartBtn != null;
    }

    private void SetButtonToggle()
    {
        soundBtn.onClick.AddListener(() =>
        {
            // Toggle sound setting — đọc trạng thái từ AudioManager
            bool isSoundOn = AudioManager.Instance != null
                ? AudioManager.Instance.SfxVolume > 0f
                : true;
            bool newState = !isSoundOn;
            soundIcon.sprite = newState ? soundOnSprite : soundOffSprite;
            AudioManager.Instance?.SetSfxVolume(newState);
        });

        musicBtn.onClick.AddListener(() =>
        {
            // Toggle music setting — đọc trạng thái từ AudioManager
            bool isMusicOn = AudioManager.Instance != null
                ? AudioManager.Instance.BgmVolume > 0f
                : true;
            bool newState = !isMusicOn;
            musicIcon.sprite = newState ? musicOnSprite : musicOffSprite;
            AudioManager.Instance?.SetMusicVolume(newState);
        });

        vibrationBtn.onClick.AddListener(() =>
        {
            // Toggle vibration setting
            bool isVibrationOn = IsVibrationEnabled();
            bool nextState = !isVibrationOn;
            SetVibrationEnabled(nextState);
            vibrationIcon.sprite = nextState ? vibrationOnSprite : vibrationOffSprite;
        });
    }

    private void SetIcon()
    {
        // Đồng bộ icon theo trạng thái thực tế của AudioManager
        bool isSoundOn = AudioManager.Instance != null
            ? AudioManager.Instance.SfxVolume > 0f
            : PlayerPrefs.GetInt(Const.player_sfx_volume_key, 1) == 1;
        soundIcon.sprite = isSoundOn ? soundOnSprite : soundOffSprite;

        bool isMusicOn = AudioManager.Instance != null
            ? AudioManager.Instance.BgmVolume > 0f
            : PlayerPrefs.GetInt(Const.player_bgm_volume_key, 1) == 1;
        musicIcon.sprite = isMusicOn ? musicOnSprite : musicOffSprite;

        bool isVibrationOn = IsVibrationEnabled();
        if (MOST_HapticFeedback.HapticsEnabled != isVibrationOn)
        {
            MOST_HapticFeedback.HapticsEnabled = isVibrationOn;
        }
        vibrationIcon.sprite = isVibrationOn ? vibrationOnSprite : vibrationOffSprite;
    }

    private static bool IsVibrationEnabled()
    {
        int defaultValue = MOST_HapticFeedback.HapticsEnabled ? 1 : 0;
        return PlayerPrefs.GetInt(Const.player_vibration_key, defaultValue) == 1;
    }

    private static void SetVibrationEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(Const.player_vibration_key, enabled ? 1 : 0);
        MOST_HapticFeedback.HapticsEnabled = enabled;
        PlayerPrefs.Save();
    }

    private void SetButton()
    {
        if (homeBtn != null)
        {
            homeBtn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySFX(Const.popUISFX);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.CancelPendingUITransitions();
                }

                GamePlayController gamePlayController = GamePlayController.Instance;
                if (gamePlayController != null)
                {
                    gamePlayController.CleanupLevel();
                }

                if (FireRangeDetector.Instance != null)
                {
                    FireRangeDetector.Instance.targetsInRange.Clear();
                }
                HeartPrefs.DecreaseHeart();

                UIManager uiManager = UIManager.Instance;
                if (uiManager != null)
                {
                    uiManager.ShowLoadingAndRunNextFrame(() =>
                    {
                        uiManager.HideAllUI();
                        uiManager.LoadMenuUI();
                        uiManager.ShowLoadingUI2();
                    });
                }
            });
        }
        if (restartBtn != null)
        {
            restartBtn.onClick.AddListener(() =>
            {
                keepPauseWhenClosingForRestart = true;

                Hide(() =>
                {
                    keepPauseWhenClosingForRestart = false;

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.ShowPopup(Const.restartPopUp);
                    }
                    else if (GameEventHub.Instance != null)
                    {
                        // Fallback: nếu không mở được RestartPopup thì trả game về trạng thái unpause.
                        GameEventHub.Instance.Invoke(GameEventType.OnGamePause, false);
                    }
                });
            });
        }
    }
}

