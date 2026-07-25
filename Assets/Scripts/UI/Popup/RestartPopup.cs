using UnityEngine;
using UnityEngine.UI;

public class RestartPopup : BasePopUp
{
    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    private bool keepPauseWhenOpeningGetMoreLives;

    protected override void OnEnable()
    {
        base.OnEnable();

        keepPauseWhenOpeningGetMoreLives = false;

        if (confirmButton != null)
        {
            confirmButton.interactable = true;
        }

        if (cancelButton != null)
        {
            cancelButton.interactable = true;
        }

        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.Invoke(GameEventType.OnGamePause, true);
        }
    }

    protected override void OnDisable()
    {
        if (GameEventHub.Instance != null && !keepPauseWhenOpeningGetMoreLives)
        {
            GameEventHub.Instance.Invoke(GameEventType.OnGamePause, false);
        }

        base.OnDisable();
    }

    void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmRestartClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmRestartClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        }
    }

    private void OnCancelClicked()
    {
        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        Hide();
    }

    private void OnConfirmRestartClicked()
    {
        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        int currentHearts = HeartPrefs.GetCurrentHearts();
        if (currentHearts <= 0)
        {
            OpenGetMoreLivesUI();
            return;
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = false;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CancelPendingUITransitions();
        }

        GamePlayController gamePlayController = GamePlayController.Instance;
        if (gamePlayController == null)
        {
            ;
            if (confirmButton != null)
            {
                confirmButton.interactable = true;
            }
            return;
        }

        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);

        InputManager inputManager = InputManager.Instance;
        if (inputManager != null)
        {
            inputManager.SetInputActive(false);
        }

        HeartPrefs.DecreaseHeart();

        if (FireRangeDetector.Instance != null)
        {
            FireRangeDetector.Instance.targetsInRange.Clear();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLoadingAndRunNextFrame(() =>
            {
                UIManager.Instance.HideAllUI();
                UIManager.Instance.LoadInGameUI();
                UIManager.Instance.ShowLoadingUI2();
                gamePlayController.InitLevel(currentLevel);
            });
        }
    }

    private void OpenGetMoreLivesUI()
    {
        if (UIManager.Instance == null)
        {
            ;
            return;
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = false;
        }

        keepPauseWhenOpeningGetMoreLives = true;
        Hide(() =>
        {
            keepPauseWhenOpeningGetMoreLives = false;
            UIManager.Instance.SpawnGetMoreLiveUI();
        });
    }
}

