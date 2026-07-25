using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Quáº£n lÃ½ táº¥t cáº£ input trong game (Unity 2022 - Legacy Input)
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask shooterLayer;
    [SerializeField] private LayerMask tutorialClickLayer;
    [SerializeField] private float raycastDistance = 1000f;

    private bool waitingForInput = true;
    private Coroutine unblockInputCoroutine;
    private GridController gridController;
    private SlotBar slotBar;
    private readonly System.Collections.Generic.List<BaseShooter> shooterBuffer = new System.Collections.Generic.List<BaseShooter>(128);

    // Booster mode
    private bool isPickLockedShooterModeActive = false;
    private bool isHeroShooterPickModeActive   = false;

    void Awake()
    {
        Instance = this;

        if (mainCamera == null)
            mainCamera = Camera.main;

        GameEventHub.Instance.AddListener(GameEventType.OnGridControllerInit,  OnGridControllerInit);
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarInit,         OnSlotBarInit);
        GameEventHub.Instance.AddListener(GameEventType.OnBoosterActivated,    OnBoosterActivated);
        GameEventHub.Instance.AddListener(GameEventType.OnBoosterDeactivated,  OnBoosterDeactivated);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.RemoveListener(GameEventType.OnGridControllerInit,  OnGridControllerInit);
            GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarInit,         OnSlotBarInit);
            GameEventHub.Instance.RemoveListener(GameEventType.OnBoosterActivated,    OnBoosterActivated);
            GameEventHub.Instance.RemoveListener(GameEventType.OnBoosterDeactivated,  OnBoosterDeactivated);
        }
    }

    void Update()
    {
        if (!waitingForInput)
            return;

        if (ShouldBlockShooterSelectionByMechanic())
            return;

        if (IsPressed())
        {
            NotifyTutorialGameObjectClickIfAny();

            if (isPickLockedShooterModeActive)
                HandlePickLockedShooterSelection();
            else if (isHeroShooterPickModeActive)
                HandleHeroShooterSelection();
            else
                HandleShooterSelection();
        }
    }

    private bool ShouldBlockShooterSelectionByMechanic()
    {
        if (SideRouteSeedExchangeMechanic.IsAnyExchangeInProgress)
        {
            return true;
        }

        GamePlayController gamePlayController = GamePlayController.Instance;
        return gamePlayController != null && gamePlayController.IsMagicStoneClearRunning();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // INPUT CHECK (Legacy)
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private bool IsPressed()
    {
        bool isPressed = false;

        // Mobile touch
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            isPressed = true;

        // Mouse click
        if (!isPressed && Input.GetMouseButtonDown(0))
            isPressed = true;

        if (!isPressed)
            return false;

        return !IsPointerOverUI();
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        if (Input.touchCount > 0)
        {
            int fingerId = Input.GetTouch(0).fingerId;
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    private Vector2 GetScreenPos()
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(0).position;

        return Input.mousePosition;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Event callbacks
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnGridControllerInit(object data)
    {
        gridController = data as GridController;
    }

    private void OnSlotBarInit(object data)
    {
        slotBar = data as SlotBar;
    }

    private void OnBoosterActivated(object data)
    {
        if (data is BoosterManager.ActiveBoosterMode mode)
        {
            isPickLockedShooterModeActive = mode == BoosterManager.ActiveBoosterMode.PickLockedShooter;
            isHeroShooterPickModeActive   = mode == BoosterManager.ActiveBoosterMode.HeroShooter;
        }
    }

    private void OnBoosterDeactivated(object _)
    {
        isPickLockedShooterModeActive = false;
        isHeroShooterPickModeActive   = false;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Normal selection
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void HandleShooterSelection()
    {
        if (!TryRaycast(out BaseShooter shooter))
            return;

        switch (shooter.GetCurrentState())
        {
            case ShooterState.Lock:
                OnShooterSelectedFailed(shooter);
                break;

            case ShooterState.IdleGrid:
                OnShooterSelectedSuccess(shooter);
                break;

            case ShooterState.Frozen:
                (shooter as IceShooter)?.PlayFrozenShakeAnimation();
                break;
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Booster: Pick Locked
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void HandlePickLockedShooterSelection()
    {
        if (!TryRaycast(out BaseShooter shooter))
            return;

        if (shooter.GetCurrentState() == ShooterState.Lock)
        {
            PlayLockTapSfx();
            BoosterManager.Instance?.OnLockedShooterPicked(shooter);
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Booster: Hero Shooter
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void HandleHeroShooterSelection()
    {
        if (!TryRaycast(out BaseShooter shooter))
            return;

        if (shooter.GetCurrentState() == ShooterState.Idle)
        {
            BoosterManager.Instance?.OnHeroShooterPicked(shooter);
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Raycast helper
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private bool TryRaycast(out BaseShooter shooter)
    {
        shooter = null;

        Vector2 screenPos = GetScreenPos();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        int activeInputLayerMask = GetActiveInputLayerMask();

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, activeInputLayerMask))
        {
            shooter = hit.collider.GetComponent<BaseShooter>();
            return shooter != null;
        }

        return false;
    }

    private void NotifyTutorialGameObjectClickIfAny()
    {
        TutorialManager tutorialManager = TutorialManager.Instance;
        if (tutorialManager == null || mainCamera == null || !tutorialManager.IsTutorialActive)
        {
            return;
        }

        Vector2 screenPos = GetScreenPos();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        int activeInputLayerMask = GetActiveInputLayerMask();
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, activeInputLayerMask))
        {
            tutorialManager.NotifyGameObjectClicked(hit.collider.gameObject);
        }
    }

    private int GetActiveInputLayerMask()
    {
        TutorialManager tutorialManager = TutorialManager.Instance;
        if (tutorialManager == null || !tutorialManager.IsTutorialActive)
        {
            return shooterLayer.value;
        }

        if (tutorialClickLayer.value != 0)
        {
            return tutorialClickLayer.value;
        }

        if (tutorialManager.tutorialCamLayer.value != 0)
        {
            return tutorialManager.tutorialCamLayer.value;
        }

        return shooterLayer.value;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Actions
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnShooterSelectedSuccess(BaseShooter shooter)
    {
        if (shooter == null || ShouldBlockShooterSelectionByMechanic())
        {
            return;
        }

        if (slotBar.AddShooter(shooter))
        {
            AudioManager.Instance?.PlaySFX(Const.popUISFX);

            // Náº¿u Ä‘Ã¢y lÃ  shooter cuá»‘i cÃ¹ng cÃ²n cÃ³ thá»ƒ pick trÃªn grid, tá»± Ä‘á»™ng báº­t x2.
            if (IsLastPickableShooterOnGrid(shooter) &&
                SpeedMultiplierManager.Instance != null &&
                !SpeedMultiplierManager.IsSpeedUpActive())
            {
                SpeedMultiplierManager.Instance.ToggleSpeedUp();
                GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
            }

            GameEventHub.Instance.Invoke(GameEventType.OnShooterJumpStart, shooter);
            GameEventHub.Instance.Invoke(GameEventType.OnShooterSelected, shooter);
            GameEventHub.Instance.Invoke(GameEventType.OnShooterAddedToSlot, null);
        }
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
            // TÃ­nh cáº£ shooter bá»‹ lock/frozen lÃ  "váº«n cÃ²n shooter trÃªn grid"
            // Ä‘á»ƒ khÃ´ng báº­t x2 quÃ¡ sá»›m khi cÃ²n shooter chá» Ä‘Æ°á»£c unlock.
            if (state == ShooterState.IdleGrid ||
                state == ShooterState.Lock ||
                state == ShooterState.Frozen)
            {
                return false;
            }
        }

        return true;
    }

    private void OnShooterSelectedFailed(BaseShooter shooter)
    {
        shooter.PlayTouchLockAnimation();
    }

    private void PlayLockTapSfx()
    {
        AudioManager.Instance?.PlaySFX(Const.popLockSFX);
    }

    public void SetInputActive(bool active)
    {
        // Náº¿u Ä‘ang cÃ³ block coroutine chá» má»Ÿ input láº¡i thÃ¬ há»§y khi set tráº¡ng thÃ¡i thá»§ cÃ´ng.
        if (unblockInputCoroutine != null)
        {
            StopCoroutine(unblockInputCoroutine);
            unblockInputCoroutine = null;
        }

        waitingForInput = active;
    }

    public void BlockInputForSecondsRealtime(float seconds)
    {
        waitingForInput = false;

        if (unblockInputCoroutine != null)
        {
            StopCoroutine(unblockInputCoroutine);
        }

        unblockInputCoroutine = StartCoroutine(ReenableInputAfterDelay(seconds));
    }

    private System.Collections.IEnumerator ReenableInputAfterDelay(float seconds)
    {
        float wait = Mathf.Max(0f, seconds);
        if (wait > 0f)
        {
            yield return new WaitForSecondsRealtime(wait);
        }

        waitingForInput = true;
        unblockInputCoroutine = null;
    }

    public bool IsInputActive()
    {
        return waitingForInput;
    }
}
