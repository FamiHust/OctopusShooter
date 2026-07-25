using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoseUIManager : MonoBehaviour
{
    [SerializeField] private Button retryBtn;
    [SerializeField] private Transform heartHolder;
    [SerializeField] private Sprite normalHeartSprite;
    [SerializeField] private Sprite brokenHeartSprite;

    void OnEnable()
    {
        if (retryBtn != null)
        {
            retryBtn.interactable = true;
        }

        SetHearts();
        BindButtons();
    }

    void OnDisable()
    {
        if (retryBtn != null)
        {
            retryBtn.onClick.RemoveListener(OnTryAgainClicked);
        }
    }

    private void SetHearts()
    {
        if (heartHolder == null)
            return;
        int currentHeart = HeartPrefs.GetCurrentHearts();
        for (int i = 0; i < heartHolder.childCount; i++)
        {
            Image heartImage = heartHolder.GetChild(i).GetComponent<Image>();
            if (heartImage != null)
            {
                heartImage.sprite = i <= currentHeart-1 ? normalHeartSprite : brokenHeartSprite;
            }
        }
    }

    private void BindButtons()
    {
        if (retryBtn == null)
        {
            return;
        }

        retryBtn.onClick.RemoveListener(OnTryAgainClicked);
        retryBtn.onClick.AddListener(OnTryAgainClicked);
    }

    private void OnTryAgainClicked()
    {
        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        int currentHearts = HeartPrefs.GetCurrentHearts();
        if (currentHearts <= 0)
        {
            OpenGetMoreLivesUI();
            return;
        }

        if (retryBtn != null)
        {
            retryBtn.interactable = false;
        }

        GameManager.Instance?.CancelPendingUITransitions();

        GamePlayController gamePlayController = GamePlayController.Instance;
        if (gamePlayController == null)
        {
            ;
            if (retryBtn != null)
            {
                retryBtn.interactable = true;
            }
            return;
        }

        // Lose flow da tru tim o thoi diem thua, khong tru them o Try Again.
        InputManager inputManager = InputManager.Instance;
        inputManager?.SetInputActive(false);

        if (FireRangeDetector.Instance != null)
        {
            FireRangeDetector.Instance.targetsInRange.Clear();
        }

        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);

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
        if (retryBtn != null)
        {
            retryBtn.interactable = false;
        }

        if (UIManager.Instance == null)
        {
            ;
            if (retryBtn != null)
            {
                retryBtn.interactable = true;
            }
            return;
        }

        UIManager.Instance.HideLoseUI();
        UIManager.Instance.SpawnGetMoreLiveUI();
    }
}

