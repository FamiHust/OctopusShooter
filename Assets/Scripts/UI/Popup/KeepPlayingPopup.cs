using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KeepPlayingPopup : BasePopUp
{
    [SerializeField] private Button holdToSeeBtn;
    [SerializeField] private Button playOnBtn;
    [SerializeField] private Button watchAdsBtn;

    [SerializeField] private string holdButtonNameToken = "hold";
    [SerializeField] private string playOnButtonNameToken = "play";
    [SerializeField] private string watchAdsButtonNameToken = "watch";

    [SerializeField, Min(0.01f)] private float holdFadeDuration = 0.14f;
    [SerializeField] private Ease holdFadeEase = Ease.OutQuad;
    [SerializeField, Min(0)] private int playOnCoinCost = 900;
    [SerializeField] private AddSlotBoosterConfig keepPlayingAddSlotConfig;

    public event Action Closed;
    public event Action Continued;

    private bool isTemporarilyHidden;
    private bool isHoldingFromButton;
    private bool isProcessingContinue;
    private bool hasContinuedThisPopup;
    private bool pendingAddDeckAfterHide;
    private SlotBar pendingSlotBarForAddDeck;
    private HoldToSeeButtonRelay holdToSeeRelay;

    private Tween holdFadeTween;

    protected override void Awake()
    {
        base.Awake();
        TryAutoAssignButtons();
        BindContinueButtons();
        BindHoldToSeeEvents();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        TryAutoAssignButtons();
        BindContinueButtons();
        BindHoldToSeeEvents();
        ResetContinueFlowState();
        SetContinueButtonsInteractable(true);
        SetTemporarilyHidden(false);
    }

    protected override void OnDisable()
    {
        holdFadeTween?.Kill();
        isHoldingFromButton = false;
        isTemporarilyHidden = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        base.OnDisable();
    }

    private void BindHoldToSeeEvents()
    {
        if (holdToSeeBtn == null)
        {
            return;
        }

        holdToSeeRelay = holdToSeeBtn.GetComponent<HoldToSeeButtonRelay>();
        if (holdToSeeRelay == null)
        {
            holdToSeeRelay = holdToSeeBtn.gameObject.AddComponent<HoldToSeeButtonRelay>();
        }

        holdToSeeRelay.Initialize(OnHoldToSeePointerDown, OnHoldToSeePointerUp);
    }

    private void OnHoldToSeePointerDown()
    {
        if (isProcessingContinue)
        {
            return;
        }

        isHoldingFromButton = true;
        SetTemporarilyHidden(true);
    }

    private void OnHoldToSeePointerUp()
    {
        if (!isHoldingFromButton)
        {
            return;
        }

        isHoldingFromButton = false;
        SetTemporarilyHidden(false);
    }

    private void TryAutoAssignButtons()
    {
        if (holdToSeeBtn != null && playOnBtn != null && watchAdsBtn != null)
        {
            return;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
        {
            return;
        }

        string holdToken = string.IsNullOrWhiteSpace(holdButtonNameToken) ? "hold" : holdButtonNameToken;
        string playToken = string.IsNullOrWhiteSpace(playOnButtonNameToken) ? "play" : playOnButtonNameToken;
        string watchToken = string.IsNullOrWhiteSpace(watchAdsButtonNameToken) ? "watch" : watchAdsButtonNameToken;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null)
            {
                continue;
            }

            if (holdToSeeBtn == null && candidate.name.IndexOf(holdToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                holdToSeeBtn = candidate;
                continue;
            }

            if (playOnBtn == null && candidate.name.IndexOf(playToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                playOnBtn = candidate;
                continue;
            }

            if (watchAdsBtn == null && candidate.name.IndexOf(watchToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                watchAdsBtn = candidate;
            }
        }

        if (holdToSeeBtn == null && buttons.Length == 1)
        {
            holdToSeeBtn = buttons[0];
        }
    }

    private void BindContinueButtons()
    {
        if (playOnBtn != null)
        {
            playOnBtn.onClick.RemoveListener(OnPlayOnClicked);
            playOnBtn.onClick.AddListener(OnPlayOnClicked);
        }

        if (watchAdsBtn != null)
        {
            watchAdsBtn.onClick.RemoveListener(OnWatchAdsClicked);
            watchAdsBtn.onClick.AddListener(OnWatchAdsClicked);
        }
    }

    private void OnPlayOnClicked()
    {
        OnHoldToSeePointerUp();

        if (isProcessingContinue)
        {
            return;
        }

        int cost = Mathf.Max(0, playOnCoinCost);
        if (!TrySpendCoins(cost))
        {
            ;
            return;
        }

        ContinueWithExtraSlot();
    }

    private void OnWatchAdsClicked()
    {
        OnHoldToSeePointerUp();

        if (isProcessingContinue)
        {
            return;
        }

        ContinueWithExtraSlot();
    }

    private bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (PlayerData.Instance != null)
        {
            return PlayerData.Instance.SpendCoins(amount);
        }

        int currentCoins = PlayerPrefs.GetInt(Const.player_coins_key, 0);
        if (currentCoins < amount)
        {
            return false;
        }

        PlayerPrefs.SetInt(Const.player_coins_key, currentCoins - amount);
        PlayerPrefs.Save();
        return true;
    }

    private void ContinueWithExtraSlot()
    {
        isProcessingContinue = true;
        SetContinueButtonsInteractable(false);

        SlotBar slotBar = SlotBar.Instance;
        if (slotBar == null)
        {
            ;
            isProcessingContinue = false;
            SetContinueButtonsInteractable(true);
            return;
        }

        pendingSlotBarForAddDeck = slotBar;
        pendingAddDeckAfterHide = slotBar.GetSlotCount() < 5;
        FinalizeContinueAndClose();
    }

    private void FinalizeContinueAndClose()
    {
        if (hasContinuedThisPopup)
        {
            return;
        }

        hasContinuedThisPopup = true;

        GamePlayController gamePlayController = GamePlayController.Instance;
        gamePlayController?.ContinueAfterKeepPlaying();

        AudioManager.Instance?.PlaySFX(Const.popUISFX);
        Continued?.Invoke();
        Hide();
    }

    private void SetContinueButtonsInteractable(bool interactable)
    {
        if (playOnBtn != null)
        {
            playOnBtn.interactable = interactable;
        }

        if (watchAdsBtn != null)
        {
            watchAdsBtn.interactable = interactable;
        }
    }

    private void SetTemporarilyHidden(bool hidden)
    {
        if (isTemporarilyHidden == hidden)
        {
            return;
        }

        isTemporarilyHidden = hidden;

        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float targetAlpha = hidden ? 0f : 1f;
        holdFadeTween?.Kill();
        holdFadeTween = canvasGroup.DOFade(targetAlpha, holdFadeDuration)
            .SetEase(holdFadeEase)
            .SetUpdate(true);
    }

    protected override void OnHideComplete()
    {
        if (hasContinuedThisPopup)
        {
            if (pendingAddDeckAfterHide && pendingSlotBarForAddDeck != null)
            {
                SlotBar slotBar = pendingSlotBarForAddDeck;
                slotBar.AddSlotWithAnimation(keepPlayingAddSlotConfig, () =>
                {
                    GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
                });
            }
            else
            {
                GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
            }
        }

        ResetContinueFlowState();
        base.OnHideComplete();
        Closed?.Invoke();
    }

    private void ResetContinueFlowState()
    {
        isHoldingFromButton = false;
        isProcessingContinue = false;
        hasContinuedThisPopup = false;
        pendingAddDeckAfterHide = false;
        pendingSlotBarForAddDeck = null;
    }
}

public class HoldToSeeButtonRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
{
    private Action onPointerDown;
    private Action onPointerUp;

    public void Initialize(Action pointerDown, Action pointerUp)
    {
        onPointerDown = pointerDown;
        onPointerUp = pointerUp;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        onPointerDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        onPointerUp?.Invoke();
    }

    public void OnCancel(BaseEventData eventData)
    {
        onPointerUp?.Invoke();
    }
}

