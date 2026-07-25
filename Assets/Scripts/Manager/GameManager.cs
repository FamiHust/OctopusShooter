using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// GameManager - Quáº£n lÃ½ game flow vÃ  táº¥t cáº£ há»‡ thá»‘ng chÃ­nh
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private GridController gridController;
    [SerializeField] private SlotBar slotBar;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private GameObject fireRangeObject; // Shared fire range

    private GameEventHub gameEventHub;
    private Coroutine pendingWinUICoroutine;
    
    private static GameManager instance;
    
    public static GameManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        gameEventHub = new GameEventHub();
    }

    void Start()
    {
        InitializeGame();
        SubscribeToEvents();
    }

    /// <summary>
    /// Khá»Ÿi táº¡o game
    /// </summary>
    private void InitializeGame()
    {
        //;
        
        //// Set global fire range object
        //if (fireRangeObject != null)
        //{
        //    BaseShooter.SetFireRangeObject(fireRangeObject);
        //    ;
        //}
        
        //// Ensure all managers are initialized
        //if (gridController != null)
        //{
        //    ;
        //}
        
        //if (slotBar != null)
        //{
        //    ;
        //}
        
        //if (inputManager != null)
        //{
        //    ;
        //}
    }

    /// <summary>
    /// ÄÄƒng kÃ½ cÃ¡c event listeners
    /// </summary>
    private void SubscribeToEvents()
    {
        GameEventHub.Instance.AddListener(GameEventType.OnShooterJumpStart, OnShooterJump); // GameManager.SubscribeToEvents
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarFull, OnSlotBarFull); // GameManager.SubscribeToEvents
        GameEventHub.Instance.AddListener(GameEventType.OnBulletCountChanged, OnBulletCountChanged); // GameManager.SubscribeToEvents
        GameEventHub.Instance.AddListener(GameEventType.OnSlotBarInit, OnSlotBarInit);
    }

    private void OnSlotBarInit(object data)
    {
        SlotBar incomingSlotBar = data as SlotBar;
        if (incomingSlotBar != null)
        {
            slotBar = incomingSlotBar;
        }
    }

    /// <summary>
    /// Callback khi shooter báº¯t Ä‘áº§u jump
    /// </summary>
    private void OnShooterJump(object shooterObj)
    {
        if (shooterObj is BaseShooter shooter)
        {
            ;
        }
    }

    /// <summary>
    /// Callback khi slotbar Ä‘áº§y
    /// </summary>
    private void OnSlotBarFull(object data)
    {
        ;
    }

    /// <summary>
    /// Callback khi bullet count thay Ä‘á»•i
    /// </summary>
    private void OnBulletCountChanged(object bulletCountObj)
    {
        int bulletCount = (int)bulletCountObj;
        //;
    }

    /// <summary>
    /// HÃ m called tá»« bÃªn ngoÃ i Ä‘á»ƒ kiá»ƒm tra xem cÃ³ thá»ƒ chá»n shooter tá»« vá»‹ trÃ­ cá»¥ thá»ƒ
    /// </summary>
    public bool CanSelectShooter(Vector3 gridPosition)
    {
        // CÃ³ thá»ƒ extend logic nÃ y náº¿u cáº§n
        return true;
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch táº¥t cáº£ shooters hiá»‡n cÃ³ trong SlotBar
    /// </summary>
    public List<BaseShooter> GetActiveShooters()
    {
        return slotBar.GetAllShooters();
    }

    /// <summary>
    /// Check SlotBar cÃ³ slot trá»‘ng khÃ´ng
    /// </summary>
    public bool HasEmptySlot()
    {
        return !slotBar.IsFull();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Win/Lose Handlers
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Gá»i khi player tháº¯ng level
    /// - Cá»™ng 1 level
    /// - Cá»™ng coins thÆ°á»Ÿng
    /// - Show win popup (placeholder, sáº½ implement sau)
    /// </summary>
    public void OnLevelWon(float winUiDelaySeconds = 1f)
    {
        if (PlayerData.Instance == null)
        {
            ;
            return;
        }

        // TODO: TÃ­nh toÃ¡n coins thÆ°á»Ÿng dá»±a trÃªn level, stars, etc.
        // Táº¡m dÃ¹ng giÃ¡ trá»‹ cá»©ng 50 coin
        // int rewardCoins = 40;
        // PlayerData.Instance.AddCoins(rewardCoins);
        
        if (UIManager.Instance != null)
        {
            CancelPendingUITransitions();
            pendingWinUICoroutine = StartCoroutine(DelaySpawnWinUI(winUiDelaySeconds));
        }
        
        //;
    }

    private IEnumerator DelaySpawnWinUI(float delaySeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));
        pendingWinUICoroutine = null;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SpawnWinUI();
        }
    }

    public void CancelPendingUITransitions()
    {
        if (pendingWinUICoroutine != null)
        {
            StopCoroutine(pendingWinUICoroutine);
            pendingWinUICoroutine = null;
        }
    }

    /// <summary>
    /// Gá»i khi player thua level
    /// - Trá»« 1 health
    /// - Show lose popup (placeholder, sáº½ implement sau)
    /// </summary>
    public void OnLevelLost()
    {
        int heartsLeft = HeartPrefs.DecreaseHeart();
        ;
        
        if (UIManager.Instance != null)
        {
            int currentSlotCount = GetCurrentSlotCount();
            if (currentSlotCount >= 5)
            {
                UIManager.Instance.SpawnLoseUI();
                return;
            }

            UIManager.Instance.ShowPopup(Const.keepPlayingPopUp, popup =>
            {
                KeepPlayingPopup keepPlayingPopup = popup as KeepPlayingPopup;
                if (keepPlayingPopup == null)
                {
                    UIManager.Instance.SpawnLoseUI();
                    return;
                }

                bool hasContinued = false;
                keepPlayingPopup.Continued += () =>
                {
                    hasContinued = true;
                };

                keepPlayingPopup.Closed += () =>
                {
                    if (!hasContinued && UIManager.Instance != null)
                    {
                        UIManager.Instance.SpawnLoseUI();
                    }
                };
            });
        }
    }

    private int GetCurrentSlotCount()
    {
        if (slotBar == null)
        {
            slotBar = SlotBar.Instance;
        }

        return slotBar != null ? slotBar.GetSlotCount() : 0;
    }

    void OnDestroy()
    {
        GameEventHub.Instance.RemoveListener(GameEventType.OnShooterJumpStart, OnShooterJump);
        GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarFull, OnSlotBarFull);
        GameEventHub.Instance.RemoveListener(GameEventType.OnBulletCountChanged, OnBulletCountChanged);
        GameEventHub.Instance.RemoveListener(GameEventType.OnSlotBarInit, OnSlotBarInit);

        if (instance == this)
        {
            instance = null;
        }
    }
}

