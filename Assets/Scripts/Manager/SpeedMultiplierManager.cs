using UnityEngine;

/// <summary>
/// Quản lý toàn bộ multiplier tốc độ trong trò chơi.
/// Các hệ thống (SplineRoute, BaseShooter, ConveyorArrowSystem, v.v.)
/// sẽ gọi GetCurrentMultiplier() để lấy hệ số tốc độ hiện tại.
/// </summary>
public class SpeedMultiplierManager : MonoBehaviour
{
    public static float CurrentMultiplier { get; private set; } = 1f;
    public static SpeedMultiplierManager Instance;
    public static bool IsGamePausedBySetting { get; private set; }
    private static float cachedTimeScaleBeforePause = 1f;
    private static float targetMultiplier = 1f;

    [SerializeField] private float speedUpMultiplier = 1f;  // Có thể thay đổi trong Inspector
    [SerializeField, Min(0.01f)] private float multiplierLerpSpeed = 8f;

    private float multiplierLerpVelocity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        targetMultiplier = CurrentMultiplier;

        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.AddListener(GameEventType.OnGamePause, OnGamePauseChanged);
        }
    }

    private void Update()
    {
        // Dùng unscaledDeltaTime để quá trình lerp không bị ảnh hưởng bởi Time.timeScale.
        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        if (dt <= 0f)
        {
            return;
        }

        float safeLerpSpeed = Mathf.Max(0.01f, multiplierLerpSpeed);
        float smoothTime = 1f / safeLerpSpeed;
        CurrentMultiplier = Mathf.SmoothDamp(
            CurrentMultiplier,
            targetMultiplier,
            ref multiplierLerpVelocity,
            smoothTime,
            Mathf.Infinity,
            dt
        );

        if (Mathf.Abs(CurrentMultiplier - targetMultiplier) <= 0.001f)
        {
            CurrentMultiplier = targetMultiplier;
            multiplierLerpVelocity = 0f;
        }
    }

    private void OnDestroy()
    {
        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.RemoveListener(GameEventType.OnGamePause, OnGamePauseChanged);
        }

        if (IsGamePausedBySetting)
        {
            Time.timeScale = 1f;
            IsGamePausedBySetting = false;
        }

        IsGamePausedBySetting = false;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Bật/tắt speed up. Nếu đang up thì tắt (về 1x), nếu chưa thì bật.
    /// </summary>
    public void ToggleSpeedUp()
    {
        if (IsGamePausedBySetting)
            return;

        if (targetMultiplier >= speedUpMultiplier - 0.01f)
            ResetSpeed();
        else
            SetMultiplier(speedUpMultiplier);
    }

    public void SetMultiplier(float multiplier)
    {
        float clamped = Mathf.Max(0.1f, multiplier);
        if (Mathf.Approximately(targetMultiplier, clamped))
        {
            return;
        }

        targetMultiplier = clamped;
        NotifySpeedVisualRefresh();
    }

    public void ResetSpeed()
    {
        if (Mathf.Approximately(targetMultiplier, 1f))
        {
            return;
        }

        targetMultiplier = 1f;
        NotifySpeedVisualRefresh();
    }

    public static void ResetSpeedStatic()
    {
        targetMultiplier = 1f;

        // Khi init level mới cần về đúng 1x ngay lập tức để UI/logic không lệch trạng thái.
        if (Instance != null)
        {
            CurrentMultiplier = 1f;
            Instance.multiplierLerpVelocity = 0f;
        }
        else
        {
            CurrentMultiplier = 1f;
        }

        NotifySpeedVisualRefresh();
    }

    public static bool IsSpeedUpActive()
    {
        return targetMultiplier > 1.01f;
    }

    public float GetCurrentMultiplier() => CurrentMultiplier;

    public static float GetBaseMultiplier()
    {
        return CurrentMultiplier;
    }

    private void OnGamePauseChanged(object data)
    {
        bool shouldPause = true;
        if (data is bool boolData)
        {
            shouldPause = boolData;
        }

        if (shouldPause == IsGamePausedBySetting)
        {
            return;
        }

        IsGamePausedBySetting = shouldPause;

        if (shouldPause)
        {
            cachedTimeScaleBeforePause = Mathf.Max(0.0001f, Time.timeScale);
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = cachedTimeScaleBeforePause > 0f ? cachedTimeScaleBeforePause : 1f;
            cachedTimeScaleBeforePause = 1f;
        }

        // Keep audio running even when gameplay is paused by timescale.
        AudioListener.pause = false;
    }

    public static float GetEffectiveMultiplier()
    {
        return CurrentMultiplier;
    }

    private static void NotifySpeedVisualRefresh()
    {
        GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
    }
}
