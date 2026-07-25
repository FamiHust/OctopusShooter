using UnityEngine;
using UnityEngine.UI;

public static class FrameRateBootstrap
{
    private const int TargetFps = 60;
    private const bool EnableFpsOverlayInRelease = false;
    private const bool EnableFpsOverlayInDevelopmentBuild = false;
    private const bool EnableFpsOverlayInEditor = false;
    private static bool fpsOverlayCreated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyFrameRatePolicy()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
        EnsureFpsOverlay();
    }

    private static void EnsureFpsOverlay()
    {
        bool isEditor = Application.isEditor;
        bool isDevelopmentRuntime = Debug.isDebugBuild && !isEditor;
        bool shouldShowOverlay =
            (EnableFpsOverlayInEditor && isEditor)
            || (EnableFpsOverlayInDevelopmentBuild && isDevelopmentRuntime)
            || (EnableFpsOverlayInRelease && !Debug.isDebugBuild);

        if (!shouldShowOverlay)
        {
            return;
        }

        if (FpsOverlayDisplay.HasLiveInstance)
        {
            fpsOverlayCreated = true;
            return;
        }

        fpsOverlayCreated = false;

        GameObject fpsOverlayObject = new GameObject("FPSOverlay");
        Object.DontDestroyOnLoad(fpsOverlayObject);
        fpsOverlayObject.AddComponent<FpsOverlayDisplay>();
        fpsOverlayCreated = true;
    }
}

public class FpsOverlayDisplay : MonoBehaviour
{
    public static FpsOverlayDisplay Instance { get; private set; }
    public static bool HasLiveInstance => Instance != null;

    private const float UpdateInterval = 0.25f;
    private const float Padding = 12f;
    private const float OffsetAboveSafeZone = 6f;
    private const float MinTopPadding = 2f;
    private static readonly Color OverlayTextColor = new Color(0.72f, 1f, 0.45f, 1f);
    private const string PrimaryBuiltinFontPath = "LegacyRuntime.ttf";
    private const string FallbackBuiltinFontPath = "Arial.ttf";

    private Coroutine statsRoutine;
    private string statsText = "FPS: 0";
    private Canvas overlayCanvas;
    private RectTransform overlayRect;
    private Text overlayText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureOverlayUI();
    }

    private void OnEnable()
    {
        EnsureOverlayUI();

        if (statsRoutine != null)
        {
            StopCoroutine(statsRoutine);
        }

        statsRoutine = StartCoroutine(StatsUpdateLoop());
    }

    private void OnDisable()
    {
        if (statsRoutine != null)
        {
            StopCoroutine(statsRoutine);
            statsRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private System.Collections.IEnumerator StatsUpdateLoop()
    {
        while (true)
        {
            int frameCount = 0;
            float elapsed = 0f;

            while (elapsed < UpdateInterval)
            {
                frameCount++;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float safeElapsed = Mathf.Max(0.0001f, elapsed);
            float fps = frameCount / safeElapsed;
            statsText = $"FPS: {Mathf.RoundToInt(fps)}";
            RefreshOverlayUI();
        }
    }

    private void EnsureOverlayUI()
    {
        if (overlayCanvas != null && overlayRect != null && overlayText != null)
        {
            return;
        }

        GameObject canvasGO = new GameObject("FPSOverlayCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        overlayCanvas = canvasGO.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textGO = new GameObject("FPSText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGO.transform.SetParent(canvasGO.transform, false);

        overlayRect = textGO.GetComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(1f, 1f);
        overlayRect.anchorMax = new Vector2(1f, 1f);
        overlayRect.pivot = new Vector2(1f, 1f);

        overlayText = textGO.GetComponent<Text>();
        overlayText.raycastTarget = false;
        overlayText.alignment = TextAnchor.UpperRight;
        overlayText.fontStyle = FontStyle.Bold;
        overlayText.horizontalOverflow = HorizontalWrapMode.Overflow;
        overlayText.verticalOverflow = VerticalWrapMode.Overflow;
        overlayText.color = OverlayTextColor;
        overlayText.font = LoadBuiltinFontSafe();

        RefreshOverlayUI();
    }

    private static Font LoadBuiltinFontSafe()
    {
        try
        {
            Font font = Resources.GetBuiltinResource<Font>(PrimaryBuiltinFontPath);
            if (font != null)
            {
                return font;
            }
        }
        catch
        {
            // Ignore and fallback to alternate built-in/runtime fonts.
        }

        try
        {
            Font fallbackFont = Resources.GetBuiltinResource<Font>(FallbackBuiltinFontPath);
            if (fallbackFont != null)
            {
                return fallbackFont;
            }
        }
        catch
        {
            // Ignore and fallback to runtime OS font.
        }

        return Font.CreateDynamicFontFromOSFont("Arial", 14);
    }

    private void RefreshOverlayUI()
    {
        if (overlayText == null || overlayRect == null)
        {
            return;
        }

        overlayText.text = statsText;
        overlayText.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.03f), 16, 40);

        Rect safeArea = Screen.safeArea;
        float topInset = Screen.height - safeArea.yMax;
        float y = Mathf.Max(MinTopPadding, topInset - OffsetAboveSafeZone);
        overlayRect.anchoredPosition = new Vector2(-Padding, -y);
    }
}
