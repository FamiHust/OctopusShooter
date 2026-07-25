using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Profiling;
using TMPro;

public class LoadingUI : MonoBehaviour
{
    [Header("Text UI")]
    [SerializeField] private Text progressText;

    [Header("Fake Loading")]
    [SerializeField] private float fakeLoadDuration = 2.5f;
    [SerializeField] private float holdAtFullProgressDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.45f;
    [SerializeField] private bool disableAfterFade = true;
    [SerializeField] private float progressTickInterval = 0.033f;
    [SerializeField] private bool gentleMode = true;
    [SerializeField] private float gentleProgressTickInterval = 0.05f;

    [Header("Fake Load Realism")]
    [SerializeField] private bool useRandomStalls = true;
    [SerializeField] private int minStepPercent = 2;
    [SerializeField] private int maxStepPercent = 4;
    [SerializeField] private float minStepInterval = 0.04f;
    [SerializeField] private float maxStepInterval = 0.12f;
    [SerializeField] private float minStallDuration = 0.08f;
    [SerializeField] private float maxStallDuration = 0.22f;
    [SerializeField] private float minStallProgressGap = 0.1f;
    [SerializeField] private float maxStallProgressGap = 0.24f;

    [Header("Wave Images")]
    [SerializeField] private List<RectTransform> waveImages = new List<RectTransform>();
    [SerializeField] private float waveHeight = 16f;
    [SerializeField] private float waveMoveDuration = 0.28f;
    [SerializeField] private float waveDominoDelay = 0.08f;
    [SerializeField] private Ease waveEase = Ease.InOutSine;

    [Header("Performance")]
    [SerializeField] private bool enableLowEndLiteMode = true;
    [SerializeField] private int lowEndSystemMemoryMb = 3000;
    [SerializeField] private int lowEndProcessorCount = 4;
    [SerializeField] private bool skipWaveAnimationOnLowEnd = true;

    private CanvasGroup canvasGroup;
    private Coroutine loadingRoutine;
    private readonly List<Tween> waveDominoTweens = new List<Tween>();
    private readonly List<Vector2> waveBaseAnchoredPos = new List<Vector2>();
    private int lastDisplayedPercent = -1;
    private bool useLiteMode;
    private WaitForSecondsRealtime progressTickYield;
    private float runtimeProgressTickInterval;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
    }

    private void OnEnable()
    {
        lastDisplayedPercent = -1;
        useLiteMode = ShouldUseLiteMode();
        runtimeProgressTickInterval = Mathf.Max(0.01f, gentleMode ? Mathf.Max(progressTickInterval, gentleProgressTickInterval) : progressTickInterval);
        progressTickYield = new WaitForSecondsRealtime(runtimeProgressTickInterval);
        StartWaveAnimation();

        if (loadingRoutine != null)
        {
            StopCoroutine(loadingRoutine);
        }

        loadingRoutine = StartCoroutine(FakeLoadRoutine());
    }

    private void OnDisable()
    {
        StopWaveAnimation(true);

        if (loadingRoutine != null)
        {
            StopCoroutine(loadingRoutine);
            loadingRoutine = null;
        }
    }

    private void StartWaveAnimation()
    {
        Profiler.BeginSample("LoadingUI.StartWaveAnimation");
        StopWaveAnimation(false);

        if (waveImages == null || waveImages.Count == 0)
        {
            Profiler.EndSample();
            return;
        }

        waveBaseAnchoredPos.Clear();

        for (int i = 0; i < waveImages.Count; i++)
        {
            RectTransform image = waveImages[i];
            waveBaseAnchoredPos.Add(image != null ? image.anchoredPosition : Vector2.zero);
        }

        if (useLiteMode && skipWaveAnimationOnLowEnd)
        {
            Profiler.EndSample();
            return;
        }

        float safeMoveDuration = Mathf.Max(0.05f, waveMoveDuration);
        float safeDominoDelay = Mathf.Max(0f, waveDominoDelay);
        float safeWaveHeight = Mathf.Max(0f, waveHeight);
        int animatedCount = Mathf.Min(3, waveImages.Count);

        if (animatedCount <= 0 || safeWaveHeight <= 0.01f)
        {
            Profiler.EndSample();
            return;
        }

        for (int i = 0; i < animatedCount; i++)
        {
            RectTransform image = waveImages[i];
            if (image == null)
            {
                continue;
            }

            Vector2 basePos = waveBaseAnchoredPos[i];
            Sequence imageSequence = DOTween.Sequence();
            imageSequence.SetUpdate(true);
            imageSequence.Append(image.DOAnchorPosY(basePos.y + safeWaveHeight, safeMoveDuration).SetEase(waveEase));
            imageSequence.Append(image.DOAnchorPosY(basePos.y, safeMoveDuration).SetEase(waveEase));

            float startupDelay = i * safeDominoDelay;
            if (startupDelay > 0f)
            {
                // Offset only at startup so each image keeps a continuous loop afterward.
                imageSequence.SetDelay(startupDelay, false);
            }

            imageSequence.SetLoops(-1, LoopType.Restart);
            waveDominoTweens.Add(imageSequence);
        }

        Profiler.EndSample();
    }

    private void StopWaveAnimation(bool resetPosition)
    {
        for (int i = 0; i < waveDominoTweens.Count; i++)
        {
            Tween tween = waveDominoTweens[i];
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }

        waveDominoTweens.Clear();

        if (resetPosition && waveImages != null)
        {
            int count = Mathf.Min(waveImages.Count, waveBaseAnchoredPos.Count);
            for (int i = 0; i < count; i++)
            {
                if (waveImages[i] != null)
                {
                    waveImages[i].anchoredPosition = waveBaseAnchoredPos[i];
                }
            }
        }
    }

    private IEnumerator FakeLoadRoutine()
    {
        if (gentleMode)
        {
            yield return StartCoroutine(FakeLoadRoutineGentle());
            yield break;
        }

        canvasGroup.alpha = 1f;
        SetProgressUI(0f);

        float duration = Mathf.Max(0.1f, fakeLoadDuration);
        float elapsed = 0f;
        int percent = 0;
        float nextStepTimer = Random.Range(Mathf.Min(minStepInterval, maxStepInterval), Mathf.Max(minStepInterval, maxStepInterval));
        float nextStallProgress = Random.Range(12f, 30f);
        float stallRemaining = 0f;

        while (percent < 100)
        {
            elapsed += Time.unscaledDeltaTime;

            if (useRandomStalls && stallRemaining <= 0f && percent < 92 && percent >= nextStallProgress)
            {
                stallRemaining = Random.Range(Mathf.Max(0f, minStallDuration), Mathf.Max(minStallDuration, maxStallDuration));
                nextStallProgress += Random.Range(Mathf.Max(0.02f, minStallProgressGap), Mathf.Max(minStallProgressGap, maxStallProgressGap)) * 100f;
            }

            if (stallRemaining > 0f)
            {
                stallRemaining -= Time.unscaledDeltaTime;
                SetProgressUI(percent / 100f);
                yield return progressTickYield;
                continue;
            }

            nextStepTimer -= Time.unscaledDeltaTime;
            if (nextStepTimer <= 0f)
            {
                int minStep = Mathf.Max(1, Mathf.Min(minStepPercent, maxStepPercent));
                int maxStep = Mathf.Max(minStep, Mathf.Max(minStepPercent, maxStepPercent));
                int step = Random.Range(minStep, maxStep + 1);
                percent = Mathf.Clamp(percent + step, 0, 100);
                SetProgressUI(percent / 100f);

                nextStepTimer = Random.Range(Mathf.Min(minStepInterval, maxStepInterval), Mathf.Max(minStepInterval, maxStepInterval));
            }

            if (elapsed >= duration)
            {
                percent = 100;
                SetProgressUI(1f);
                break;
            }

            yield return progressTickYield;
        }

        SetProgressUI(1f);
        StopWaveAnimation(false);

        float holdDuration = Mathf.Max(0f, holdAtFullProgressDuration);
        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        yield return StartCoroutine(FadeOutRoutine());

        if (disableAfterFade)
        {
            gameObject.SetActive(false);
        }

        loadingRoutine = null;
    }

    private IEnumerator FakeLoadRoutineGentle()
    {
        canvasGroup.alpha = 1f;
        SetProgressUI(0f);

        float duration = Mathf.Max(0.1f, fakeLoadDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += runtimeProgressTickInterval;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float gentleProgress = Mathf.SmoothStep(0f, 1f, normalized);
            SetProgressUI(gentleProgress);
            yield return progressTickYield;
        }

        SetProgressUI(1f);
        StopWaveAnimation(false);

        float holdDuration = Mathf.Max(0f, holdAtFullProgressDuration);
        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        yield return StartCoroutine(FadeOutRoutine());

        if (disableAfterFade)
        {
            gameObject.SetActive(false);
        }

        loadingRoutine = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        float duration = Mathf.Max(0.05f, fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothed = Mathf.SmoothStep(0f, 1f, t);
            canvasGroup.alpha = Mathf.LerpUnclamped(1f, 0f, smoothed);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    private void SetProgressUI(float normalizedProgress)
    {
        float clamped = Mathf.Clamp01(normalizedProgress);

        if (progressText != null)
        {
            int percent = Mathf.FloorToInt(clamped * 100f);
            if (clamped >= 1f)
            {
                percent = 100;
            }

            if (percent == lastDisplayedPercent)
            {
                return;
            }

            lastDisplayedPercent = percent;

            progressText.text = "Loading " + percent + "%...";
        }
    }

    private bool ShouldUseLiteMode()
    {
        if (!enableLowEndLiteMode)
        {
            return false;
        }

        int memoryMb = SystemInfo.systemMemorySize;
        if (memoryMb > 0 && memoryMb <= Mathf.Max(512, lowEndSystemMemoryMb))
        {
            return true;
        }

        return SystemInfo.processorCount <= Mathf.Max(1, lowEndProcessorCount);
    }
}
